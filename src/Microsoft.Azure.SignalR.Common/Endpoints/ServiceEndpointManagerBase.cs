﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceEndpointManagerBase : IServiceEndpointManager
    {
        // endpoints ready for negotiate
        private readonly ConcurrentDictionary<string, IReadOnlyList<HubServiceEndpoint>> _endpointsPerHub = new ConcurrentDictionary<string, IReadOnlyList<HubServiceEndpoint>>();
        // endpoints ready for route
        private readonly ConcurrentDictionary<string, IMultiEndpointServiceConnectionContainer> _hubContainers = new ConcurrentDictionary<string, IMultiEndpointServiceConnectionContainer>();


        private readonly ILogger _logger;

        public ServiceEndpoint[] Endpoints { get; internal set; }

        protected ServiceEndpointManagerBase(IServiceEndpointOptions options, ILogger logger) 
            : this(GetEndpoints(options), logger)
        {
        }

        // for test purpose
        internal ServiceEndpointManagerBase(IEnumerable<ServiceEndpoint> endpoints, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // select the most valuable endpoint with the same endpoint address
            var groupedEndpoints = endpoints.Distinct().GroupBy(s => s.Endpoint).Select(s =>
            {
                var items = s.ToList();
                if (items.Count == 1)
                {
                    return items[0];
                }

                // By default pick up the primary endpoint, otherwise the first one
                var item = items.FirstOrDefault(i => i.EndpointType == EndpointType.Primary) ?? items.FirstOrDefault();
                Log.DuplicateEndpointFound(_logger, items.Count, item?.Endpoint, item?.ToString());
                return item;
            });

            Endpoints = groupedEndpoints.ToArray();

            if (Endpoints.Length > 0 && Endpoints.All(s => s.EndpointType != EndpointType.Primary))
            {
                // Only throws when endpoint count > 0
                throw new AzureSignalRNoPrimaryEndpointException();
            }
        }

        public abstract IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint);

        public IReadOnlyList<HubServiceEndpoint> GetEndpoints(string hub)
        {
            return _endpointsPerHub.GetOrAdd(hub, s => Endpoints.Select(e =>
            {
                var provider = GetEndpointProvider(e);
                return new HubServiceEndpoint(hub, provider, e);
            }).ToArray());
        }

        public void AddServiceEndpoint(ServiceEndpoint endpoint)
        {
            foreach (var hubEndpoints in _endpointsPerHub)
            {
                var provider = GetEndpointProvider(endpoint);
                var hubServiceEndpoint = new HubServiceEndpoint(hubEndpoints.Key, provider, endpoint);
                var updatedHubEndpoints = hubEndpoints.Value.Append(hubServiceEndpoint).ToArray();
                _endpointsPerHub.TryUpdate(hubEndpoints.Key, updatedHubEndpoints, hubEndpoints.Value);
            }
        }

        public void RemoveServiceEndpoint(ServiceEndpoint endpoint)
        {
            foreach (var hubEndpoints in _endpointsPerHub)
            {
                // Make ConnectionString the key to match existing endpoints
                var updatedHubEndpoints = hubEndpoints.Value.Where(e => e.ConnectionString != endpoint.ConnectionString).ToArray();
                _endpointsPerHub.TryUpdate(hubEndpoints.Key, updatedHubEndpoints, hubEndpoints.Value);
            }
        }

        public HubServiceEndpoint GenerateHubServiceEndpoint(string hub, ServiceEndpoint endpoint)
        {
            var provider = GetEndpointProvider(endpoint);
            return new HubServiceEndpoint(hub, provider, endpoint);
        }

        public IMultiEndpointServiceConnectionContainer GetServiceConnectionContainer(string hub)
        {
            if (_hubContainers.TryGetValue(hub, out var container))
            {
                return container;
            }

            Log.MultiEndpointContainerNotFound(_logger, hub);
            return null;
        }

        public void AddServiceConnectionContainer(string hub, IMultiEndpointServiceConnectionContainer container)
        {
            if (!_hubContainers.TryAdd(hub, container))
            {
                Log.MultiEndpointContainerAlreadyExists(_logger, hub);
            }
        }

        public IEnumerable<string> GetHubs()
        {
            return _hubContainers.Select(h => h.Key).ToList();
        }

        private static IEnumerable<ServiceEndpoint> GetEndpoints(IServiceEndpointOptions options)
        {
            if (options == null)
            {
                yield break;
            }

            var endpoints = options.Endpoints;
            var connectionString = options.ConnectionString;

            if (!string.IsNullOrEmpty(connectionString))
            {
                yield return new ServiceEndpoint(options.ConnectionString);
            }

            // ConnectionString can be set by custom Configure
            // Return both the one from ConnectionString and from Endpoints
            if (endpoints != null)
            {
                foreach (var endpoint in endpoints)
                {
                    yield return endpoint;
                }
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, int, string, string, Exception> _duplicateEndpointFound =
                LoggerMessage.Define<int, string, string>(LogLevel.Warning, new EventId(1, "DuplicateEndpointFound"), "{count} endpoint configurations to '{endpoint}' found, use '{name}'.");

            private static readonly Action<ILogger, string, Exception> _multiEndpointsContainerAlreadyExists =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "MultiEndpointsContainerAlreadyExists"), "MultiEndpointContainer for hub '{hub}' alreay exists.");

            private static readonly Action<ILogger, string, Exception> _multiEndpointsContainerNotFound =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "MultiEndpointsContainerNotFound"), "MultiEndpointContainer for hub '{hub}' not found");

            public static void DuplicateEndpointFound(ILogger logger, int count, string endpoint, string name)
            {
                _duplicateEndpointFound(logger, count, endpoint, name, null);
            }

            public static void MultiEndpointContainerAlreadyExists(ILogger logger, string hub)
            {
                _multiEndpointsContainerAlreadyExists(logger, hub, null);
            }

            public static void MultiEndpointContainerNotFound(ILogger logger, string hub)
            {
                _multiEndpointsContainerNotFound(logger, hub, null);
            }
        }
    }
}
