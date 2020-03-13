﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class MultiEndpointServiceConnectionContainer : IMultiEndpointServiceConnectionContainer
    {
        private readonly string _hubName;
        private readonly IMessageRouter _router;
        private readonly ILogger _logger;
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly TimeSpan _scaleTimeout;
        private readonly Func<HubServiceEndpoint, IServiceConnectionContainer> _generator;
        private readonly int _scaleWaitIntervalInSeconds = 5;
        private readonly object _lock = new object();

        private (bool needRouter, IReadOnlyList<HubServiceEndpoint> endpoints) _routerEndpoints;

        internal MultiEndpointServiceConnectionContainer(
            string hub,
            Func<HubServiceEndpoint, IServiceConnectionContainer> generator,
            IServiceEndpointManager endpointManager,
            IMessageRouter router,
            ILoggerFactory loggerFactory,
            TimeSpan? scaleTimeout = null)
        {
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            _hubName = hub;
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _logger = loggerFactory?.CreateLogger<MultiEndpointServiceConnectionContainer>() ?? throw new ArgumentNullException(nameof(loggerFactory));
            _serviceEndpointManager = endpointManager;
            _scaleTimeout = scaleTimeout ?? TimeSpan.FromSeconds(Constants.DefaultScaleTimeoutInSeconds);

            // Reserve generator for potential scale use.
            _generator = generator;

            // provides a copy to the endpoint per container
            var endpoints = endpointManager.GetEndpoints(hub);
            // router will be used when there's customized MessageRouter or multiple endpoints
            var needRouter = endpoints.Count > 1 || !(_router is DefaultMessageRouter);

            _routerEndpoints = (needRouter, endpoints);

            foreach (var endpoint in endpoints)
            {
                endpoint.ConnectionContainer = generator(endpoint);
            }

            _serviceEndpointManager.OnAdd += OnAdd;
            _serviceEndpointManager.OnRemove += OnRemove;
        }

        public MultiEndpointServiceConnectionContainer(
            IServiceConnectionFactory serviceConnectionFactory,
            string hub,
            int count,
            IServiceEndpointManager endpointManager,
            IMessageRouter router,
            ILoggerFactory loggerFactory,
            TimeSpan? scaleTimeout = null
            ) : this(
                hub,
                endpoint => CreateContainer(serviceConnectionFactory, endpoint, count, loggerFactory),
                endpointManager,
                router,
                loggerFactory,
                scaleTimeout)
        {
        }

        // for tests
        public IEnumerable<HubServiceEndpoint> GetOnlineEndpoints()
        {
            return _routerEndpoints.endpoints.Where(s => s.Online);
        }

        private static IServiceConnectionContainer CreateContainer(IServiceConnectionFactory serviceConnectionFactory, HubServiceEndpoint endpoint, int count, ILoggerFactory loggerFactory)
        {
            if (endpoint.EndpointType == EndpointType.Primary)
            {
                return new StrongServiceConnectionContainer(serviceConnectionFactory, count, endpoint, loggerFactory.CreateLogger<StrongServiceConnectionContainer>());
            }
            else
            {
                return new WeakServiceConnectionContainer(serviceConnectionFactory, count, endpoint, loggerFactory.CreateLogger<WeakServiceConnectionContainer>());
            }
        }

        public ServiceConnectionStatus Status => throw new NotSupportedException();

        public Task ConnectionInitializedTask
        {
            get
            {
                return Task.WhenAll(from connection in _routerEndpoints.endpoints
                                    select connection.ConnectionContainer.ConnectionInitializedTask);
            }
        }

        public HashSet<string> GlobalServerIds => throw new NotSupportedException();

        public bool HasClients => throw new NotSupportedException();

        public Task StartAsync()
        {
            return Task.WhenAll(_routerEndpoints.endpoints.Select(s =>
            {
                Log.StartingConnection(_logger, s.Endpoint);
                return s.ConnectionContainer.StartAsync();
            }));
        }

        public Task StopAsync()
        {
            return Task.WhenAll(_routerEndpoints.endpoints.Select(s =>
            {
                Log.StoppingConnection(_logger, s.Endpoint);
                return s.ConnectionContainer.StopAsync();
            }));
        }

        public Task OfflineAsync(bool migratable)
        {
            return Task.WhenAll(_routerEndpoints.endpoints.Select(c => c.ConnectionContainer.OfflineAsync(migratable)));
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            return WriteMultiEndpointMessageAsync(serviceMessage, connection => connection.WriteAsync(serviceMessage));
        }

        public async Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
        {
            // If we have multiple endpoints, we should wait to one of the following conditions hit
            // 1. One endpoint responses "OK" state
            // 2. All the endpoints response failed state including "NotFound", "Timeout" and waiting response to timeout
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var writeMessageTask = WriteMultiEndpointMessageAsync(serviceMessage, async connection =>
            {
                var succeeded = await connection.WriteAckableMessageAsync(serviceMessage, cancellationToken);
                if (succeeded)
                {
                    tcs.TrySetResult(true);
                }
            });

            // If tcs.Task completes, one Endpoint responses "OK" state.
            var task = await Task.WhenAny(tcs.Task, writeMessageTask);

            // This will throw exceptions in tasks if exceptions exist
            await task;

            return tcs.Task.IsCompleted;
        }

        public Task StartGetServersPing()
        {
            return Task.WhenAll(_routerEndpoints.endpoints.Select(c => c.ConnectionContainer.StartGetServersPing()));
        }

        public Task StopGetServersPing()
        {
            return Task.WhenAll(_routerEndpoints.endpoints.Select(c => c.ConnectionContainer.StopGetServersPing()));
        }

        internal IEnumerable<ServiceEndpoint> GetRoutedEndpoints(ServiceMessage message)
        {
            if (!_routerEndpoints.needRouter)
            {
                return _routerEndpoints.endpoints;
            }
            var endpoints = _routerEndpoints.endpoints;
            switch (message)
            {
                case BroadcastDataMessage bdm:
                    return _router.GetEndpointsForBroadcast(endpoints);
                case GroupBroadcastDataMessage gbdm:
                    return _router.GetEndpointsForGroup(gbdm.GroupName, endpoints);
                case JoinGroupWithAckMessage jgm:
                    return _router.GetEndpointsForGroup(jgm.GroupName, endpoints);
                case LeaveGroupWithAckMessage lgm:
                    return _router.GetEndpointsForGroup(lgm.GroupName, endpoints);
                case MultiGroupBroadcastDataMessage mgbdm:
                    return mgbdm.GroupList.SelectMany(g => _router.GetEndpointsForGroup(g, endpoints)).Distinct();
                case ConnectionDataMessage cdm:
                    return _router.GetEndpointsForConnection(cdm.ConnectionId, endpoints);
                case MultiConnectionDataMessage mcd:
                    return mcd.ConnectionList.SelectMany(c => _router.GetEndpointsForConnection(c, endpoints)).Distinct();
                case UserDataMessage udm:
                    return _router.GetEndpointsForUser(udm.UserId, endpoints);
                case MultiUserDataMessage mudm:
                    return mudm.UserList.SelectMany(g => _router.GetEndpointsForUser(g, endpoints)).Distinct();
                default:
                    throw new NotSupportedException(message.GetType().Name);
            }
        }

        private Task WriteMultiEndpointMessageAsync(ServiceMessage serviceMessage, Func<IServiceConnectionContainer, Task> inner)
        {
            var routed = GetRoutedEndpoints(serviceMessage)?
                .Select(endpoint =>
                {
                    var connection = (endpoint as HubServiceEndpoint)?.ConnectionContainer;
                    if (connection == null)
                    {
                        Log.EndpointNotExists(_logger, endpoint.ToString());
                    }
                    return (e: endpoint, c: connection);
                })
                .Where(c => c.c != null)
                .Select(async s =>
                {
                    try
                    {
                        await inner(s.c);
                    }
                    catch (ServiceConnectionNotActiveException)
                    {
                        // log and don't stop other endpoints
                        Log.FailedWritingMessageToEndpoint(_logger, serviceMessage.GetType().Name, s.e.ToString());
                    }
                }).ToArray();

            if (routed == null || routed.Length == 0)
            {
                // check if the router returns any endpoint
                Log.NoEndpointRouted(_logger, serviceMessage.GetType().Name);
                return Task.CompletedTask;
            }

            if (routed.Length == 1)
            {
                return routed[0];
            }

            return Task.WhenAll(routed);
        }

        private void OnAdd(HubServiceEndpoint endpoint)
        {
            if (!endpoint.Hub.Equals(_hubName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            _ = AddHubServiceEndpointAsync(endpoint);
        }

        private async Task AddHubServiceEndpointAsync(HubServiceEndpoint endpoint)
        {
            var container = _generator(endpoint);
            endpoint.ConnectionContainer = container;

            try
            {
                _ = container.StartAsync();

                // Update local store directly after start connection 
                // to get a uniformed action on trigger servers ping
                UpdateEndpointsStore(endpoint, ScaleOperation.Add);

                await StartGetServersPing();
                await WaitForServerStable(container, endpoint);
            }
            catch (Exception ex)
            {
                Log.FailedStartingConnectionForNewEndpoint(_logger, endpoint.ToString(), ex);
            }
            finally
            {
                _ = StopGetServersPing();
                endpoint.CompleteScale();
            }
        }

        private void OnRemove(HubServiceEndpoint endpoint)
        {
            if (!endpoint.Hub.Equals(_hubName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            _ = RemoveHubServiceEndpointAsync(endpoint);
        }

        private Task RemoveHubServiceEndpointAsync(HubServiceEndpoint endpoint)
        {
            // TODO: trigger offline ping and wait to remove container.

            // finally set task complete when timeout
            endpoint.CompleteScale();
            return Task.CompletedTask;
        }

        private void UpdateEndpointsStore(HubServiceEndpoint newEndpoint, ScaleOperation operation)
        {
            // Use lock to ensure store update safety as parallel changes triggered in container side. 
            lock (_lock)
            {
                switch (operation)
                {
                    case ScaleOperation.Add:
                        var newEndpoints = _routerEndpoints.endpoints.ToList();
                        newEndpoints.Add(newEndpoint);
                        var needRouter = newEndpoints.Count > 1;
                        _routerEndpoints = (needRouter, newEndpoints);
                        break;
                    default:
                        break;
                }
            }
        }

        private async Task WaitForServerStable(IServiceConnectionContainer container, HubServiceEndpoint endpoint)
        {
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < _scaleTimeout)
            {
                if (IsServerReady(container))
                {
                    return;
                }
                // status ping interval is 5 seconds, delay to do next check
                await Task.Delay(_scaleWaitIntervalInSeconds * 1000);
            }
            Log.TimeoutWaitingForAddingEndpoint(_logger, endpoint.ToString(), _scaleTimeout.Seconds);
        }

        private bool IsServerReady(IServiceConnectionContainer container)
        {
            var serversOnNew = container.GlobalServerIds;
            var allMatch = serversOnNew?.Count > 0;
            if (!allMatch)
            {
                // return directly if local server list is not set yet.
                return false;
            }

            // ensure strong consistency of server Ids for new endpoint towards exists
            foreach (var endpoint in _routerEndpoints.endpoints)
            {
                allMatch = endpoint.ConnectionContainer.GlobalServerIds != null 
                    && serversOnNew.SetEquals(endpoint.ConnectionContainer.GlobalServerIds) 
                    && allMatch;
                if (!allMatch)
                {
                    return false;
                }
            }
            return allMatch;
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _startingConnection =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "StartingConnection"), "Staring connections for endpoint {endpoint}.");

            private static readonly Action<ILogger, string, Exception> _stoppingConnection =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "StoppingConnection"), "Stopping connections for endpoint {endpoint}.");

            private static readonly Action<ILogger, string, Exception> _endpointNotExists =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "EndpointNotExists"), "Endpoint {endpoint} from the router does not exists.");

            private static readonly Action<ILogger, string, Exception> _noEndpointRouted =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "NoEndpointRouted"), "Message {messageType} is not sent because no endpoint is returned from the endpoint router.");

            private static readonly Action<ILogger, string, string, Exception> _failedWritingMessageToEndpoint =
                LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(5, "FailedWritingMessageToEndpoint"), "Message {messageType} is not sent to endpoint {endpoint} because all connections to this endpoint are offline.");

            private static readonly Action<ILogger, string, Exception> _failedStartingConnectionForNewEndpoint =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(7, "FailedStartingConnectionForNewEndpoint"), "Fail to create and start server connection for new endpoint {endpoint}.");

            private static readonly Action<ILogger, string, int, Exception> _timeoutWaitingForAddingEndpoint =
                LoggerMessage.Define<string, int>(LogLevel.Error, new EventId(8, "TimeoutWaitingForAddingEndpoint"), "Timeout waiting for add a new endpoint {endpoint} in {timeoutSecond} seconds. Check if app configurations are consistant and restart app server.");

            public static void StartingConnection(ILogger logger, string endpoint)
            {
                _startingConnection(logger, endpoint, null);
            }

            public static void StoppingConnection(ILogger logger, string endpoint)
            {
                _stoppingConnection(logger, endpoint, null);
            }

            public static void EndpointNotExists(ILogger logger, string endpoint)
            {
                _endpointNotExists(logger, endpoint, null);
            }

            public static void NoEndpointRouted(ILogger logger, string messageType)
            {
                _noEndpointRouted(logger, messageType, null);
            }

            public static void FailedWritingMessageToEndpoint(ILogger logger, string messageType, string endpoint)
            {
                _failedWritingMessageToEndpoint(logger, messageType, endpoint, null);
            }

            public static void FailedStartingConnectionForNewEndpoint(ILogger logger, string endpoint, Exception ex)
            {
                _failedStartingConnectionForNewEndpoint(logger, endpoint, ex);
            }

            public static void TimeoutWaitingForAddingEndpoint(ILogger logger, string endpoint, int timeoutSecond)
            {
                _timeoutWaitingForAddingEndpoint(logger, endpoint, timeoutSecond, null);
            }
        }
    }
}
