// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Common.ServiceConnections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class MultiEndpointServiceConnectionContainer : IMultiEndpointServiceConnectionContainer
    {
        private readonly IMessageRouter _router;
        private readonly ILogger _logger;
        private readonly IServiceConnectionContainer _inner;
        private readonly IServiceConnectionFactory _serviceConnectionFactory;
        private readonly int _connectionCount;

        public IReadOnlyList<HubServiceEndpoint> HubEndpoints { get; internal set; }

        public Dictionary<ServiceEndpoint, IServiceConnectionContainer> Connections { get; internal set; } = new Dictionary<ServiceEndpoint, IServiceConnectionContainer>();

        private bool _needRouter => HubEndpoints.Count > 1;

        public MultiEndpointServiceConnectionContainer(string hub,
                                                       Func<HubServiceEndpoint, IServiceConnectionContainer> generator,
                                                       IServiceEndpointManager endpointManager,
                                                       IMessageRouter router,
                                                       ILoggerFactory loggerFactory)
        {
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            _logger = loggerFactory?.CreateLogger<MultiEndpointServiceConnectionContainer>() ?? throw new ArgumentNullException(nameof(loggerFactory));

            // provides a copy to the endpoint per container
            HubEndpoints = endpointManager.GetEndpoints(hub);

            if (_needRouter)
            {
                // router is required when endpoints > 1
                _router = router ?? throw new ArgumentNullException(nameof(router));
                Connections = HubEndpoints.ToDictionary(s => (ServiceEndpoint)s, s => generator(s));
            }
            else
            {
                _inner = generator(HubEndpoints[0]);
                Connections.Add(HubEndpoints[0], _inner);
            }

            // save current container to provide scale required information
            endpointManager.AddServiceConnectionContainer(hub, this);
        }

        public MultiEndpointServiceConnectionContainer(
            IServiceConnectionFactory serviceConnectionFactory,
            string hub,
            int count,
            IServiceEndpointManager endpointManager,
            IMessageRouter router,
            IServerNameProvider nameProvider,
            ILoggerFactory loggerFactory
            ) : this(
                hub,
                endpoint => CreateContainer(serviceConnectionFactory, endpoint, count, loggerFactory),
                endpointManager,
                router,
                loggerFactory
                )
        {
            _serviceConnectionFactory = serviceConnectionFactory;
            _connectionCount = count;
            // Always add default router for potential scale needed.
            _router = router;
        }

        public IEnumerable<ServiceEndpoint> GetOnlineEndpoints()
        {
            return Connections.Keys.Where(s => s.Online);
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

        public bool AddServiceEndpoint(HubServiceEndpoint endpoint, ILoggerFactory loggerFactory)
        {
            // Router is required when endpoints > 1
            if (_router == null)
            {
                throw new ArgumentNullException(nameof(_router));
            }

            try
            {
                // Create service connections for the new endpoint
                var connectionContainer = CreateContainer(_serviceConnectionFactory, endpoint, _connectionCount, loggerFactory);

                // Add endpoint to current container and Connection to enable routing
                var endpoints = HubEndpoints.ToList();
                endpoints.Add(endpoint);
                HubEndpoints = endpoints.AsReadOnly();
                Connections.Add(endpoint, connectionContainer);

                // Start service connection
                _ = connectionContainer.StartAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public ServiceConnectionStatus Status => throw new NotSupportedException();

        public HubServiceEndpointStatus ServiceStatus => throw new NotSupportedException();

        public Task ConnectionInitializedTask
        {
            get
            {
                if (!_needRouter)
                {
                    return _inner.ConnectionInitializedTask;
                }

                return Task.WhenAll(from connection in Connections
                                    select connection.Value.ConnectionInitializedTask);
            }
        }

        public bool IsStable => CheckHubServiceEndpoints();

        string IServiceConnectionContainer.ServerList => throw new NotImplementedException();

        private bool CheckHubServiceEndpoints()
        {
            var endpointsStatus = new List<string>();
            foreach (var item in Connections)
            {
                endpointsStatus.Add(item.Value.ServerList);
            }
            return endpointsStatus.Distinct().Count() == 1;
        }

        public Task StartAsync()
        {
            if (!_needRouter)
            {
                return _inner.StartAsync();
            }

            return Task.WhenAll(Connections.Select(s =>
            {
                Log.StartingConnection(_logger, s.Key.Endpoint);
                return s.Value.StartAsync();
            }));
        }

        public Task StopAsync()
        {
            if (!_needRouter)
            {
                return _inner.StopAsync();
            }

            return Task.WhenAll(Connections.Select(s =>
            {
                Log.StoppingConnection(_logger, s.Key.Endpoint);
                return s.Value.StopAsync();
            }));
        }

        public Task OfflineAsync()
        {
            if (_inner != null)
            {
                return _inner.OfflineAsync();
            }
            else
            {
                return Task.WhenAll(Connections.Select(c => c.Value.OfflineAsync()));
            }
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (!_needRouter)
            {
                return _inner.WriteAsync(serviceMessage);
            }
            return WriteMultiEndpointMessageAsync(serviceMessage, connection => connection.WriteAsync(serviceMessage));
        }

        public async Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
        {
            if (!_needRouter)
            {
                return await _inner.WriteAckableMessageAsync(serviceMessage, cancellationToken);
            }

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

        internal IEnumerable<ServiceEndpoint> GetRoutedEndpoints(ServiceMessage message)
        {
            var endpoints = HubEndpoints;
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
                    if (Connections.TryGetValue(endpoint, out var connection))
                    {
                        return (e: endpoint, c: connection);
                    }

                    Log.EndpointNotExists(_logger, endpoint.ToString());
                    return (e: endpoint, c: null);
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

            private static readonly Action<ILogger, string, Exception> _closingConnection =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(6, "ClosingConnection"), "Closing connections for endpoint {endpoint}.");

            public static void StartingConnection(ILogger logger, string endpoint)
            {
                _startingConnection(logger, endpoint, null);
            }

            public static void StoppingConnection(ILogger logger, string endpoint)
            {
                _stoppingConnection(logger, endpoint, null);
            }

            public static void ClosingConnection(ILogger logger, string endpoint)
            {
                _closingConnection(logger, endpoint, null);
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
        }
    }
}
