using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceScaleManager : IServiceScaleManager
    {
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private bool _inited = false;

        private IReadOnlyList<ServiceEndpoint> _store = new List<ServiceEndpoint>();

        public ServiceScaleManager(IServiceEndpointManager serviceEndpointManager, 
            IClientConnectionManager clientConnectionManager,
            ILoggerFactory loggerFactory, 
            IOptionsMonitor<ServiceOptions> optionsMonitor)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)); ;
            _logger = loggerFactory?.CreateLogger<ServiceScaleManager>();
            _serviceEndpointManager = serviceEndpointManager;
            _clientConnectionManager = clientConnectionManager;

            OnChange(optionsMonitor.CurrentValue);
            optionsMonitor.OnChange(OnChange);

            _store = optionsMonitor.CurrentValue.Endpoints;
            _inited = true;
        }

        public async Task<IReadOnlyList<ServiceEndpoint>> AddServiceEndpoint(ServiceEndpoint serviceEndpoint)
        {
            if (!_serviceEndpointManager.Endpoints.Contains(serviceEndpoint))
            {
                // Add new endpoint to MultiServiceEndpoint container to enable server routing
                var result = AddServiceEndpointToContainer(serviceEndpoint);

                // Add the new endpoint to ServiceEndpointManager to enable client negotiation 
                // only when all routing are successfully added.
                while (!result.All(x => x) || !IsEndpointStable())
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
                _serviceEndpointManager.AddServiceEndpoint(serviceEndpoint);
            }

            return _serviceEndpointManager.GetEndpoints(_serviceEndpointManager.GetHubs().FirstOrDefault());
        }

        public IReadOnlyList<ServiceEndpoint> RemoveServiceEndpoint(ServiceEndpoint serviceEndpoint)
        {
            if (!_serviceEndpointManager.Endpoints.Contains(serviceEndpoint))
            {
                // Remove from server negotiage first for new clients.
                _serviceEndpointManager.RemoveServiceEndpoint(serviceEndpoint);

                // Primary endpoint need to notice runtime to close clients
                if (serviceEndpoint.EndpointType == EndpointType.Primary)
                {
                    var result = ShutdownServiceEndpoint(serviceEndpoint);
                    if (result.All(x => x))
                    {
                        // add log
                    }
                }
                // close server connection

            }

            return _serviceEndpointManager.GetEndpoints(_serviceEndpointManager.GetHubs().FirstOrDefault());
        }

        private void OnChange(ServiceOptions options)
        {
            if (options.ConfigurationScale && _inited)
            {
                var endpoints = GetChangedEndpoints(_store, options.Endpoints);

                // Do add then remove
                OnAdd(endpoints.AddedEndpoints);

                OnRemove(endpoints.RemovedEndpoints);

                _store = options.Endpoints;
            }
        }

        private void OnAdd(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            endpoints.ToList().ForEach(e => AddServiceEndpoint(e));
        }

        private void OnRemove(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            //endpoints.ToList().ForEach(e => RemoveServiceEndpoint(e));
        }

        private (IReadOnlyList<ServiceEndpoint> AddedEndpoints, IReadOnlyList<ServiceEndpoint> RemovedEndpoints) 
            GetChangedEndpoints(IReadOnlyList<ServiceEndpoint> cachedEndpoints, IReadOnlyList<ServiceEndpoint> newEndpoints)
        {
            // Compare by ConnectionString, update endpoint is not supported
            var cachedIds = cachedEndpoints.Select(e => e.ConnectionString).ToList();
            var newIds = newEndpoints.Select(e => e.ConnectionString).ToList();

            var addedIds = newIds.Except(cachedIds).ToList();
            var removedIds = cachedIds.Except(newIds).ToList();

            var addedEndpoints = newEndpoints.Where(e => addedIds.Contains(e.ConnectionString)).ToList();
            var removedEndpoints = cachedEndpoints.Where(e => removedIds.Contains(e.ConnectionString)).ToList();

            return (AddedEndpoints: addedEndpoints, RemovedEndpoints: removedEndpoints);
        }

        private bool[] AddServiceEndpointToContainer(ServiceEndpoint serviceEndpoint)
        {
            return _serviceEndpointManager.GetHubs().Select(h => StartServiceConnectionAsync(h, serviceEndpoint)).ToArray();
        }

        private bool StartServiceConnectionAsync(string hub, ServiceEndpoint serviceEndpoint)
        {
            var container = _serviceEndpointManager.GetServiceConnectionContainer(hub);
            if (container == null)
            {
                Log.MultiEndpointContainerNotFound(_logger, hub);
                return false;
            }
            else
            {
                var hubEndpoint = _serviceEndpointManager.GenerateHubServiceEndpoint(hub, serviceEndpoint);
                return container.AddServiceEndpoint(hubEndpoint, _loggerFactory);
            }
        }
        
        private bool IsEndpointStable()
        {
            var status = new List<bool>();
            var hubs = _serviceEndpointManager.GetHubs();
            foreach (var hub in hubs)
            {
                var container = _serviceEndpointManager.GetServiceConnectionContainer(hub);
                status.Add(container.IsStable);
            }
            return status.All(x => x);
        }

        private bool[] ShutdownServiceEndpoint(ServiceEndpoint serviceEndpoint)
        {
            var result = new List<bool>();
            var hubs = _serviceEndpointManager.GetHubs();
            foreach (var hub in hubs)
            {
                var container = _serviceEndpointManager.GetServiceConnectionContainer(hub);
                result.Add(container.WriteAckableMessageAsync(ShutdownEndpointPingMessage.GetPingMessage()).Result);
            }
            return result.ToArray();
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _startAddingServiceEndpoint =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, "StartAddingServiceEndpoint"), "Start to add ServiceEndpoint: {name}");

            private static readonly Action<ILogger, string, Exception> _multiEndpointsContainerNotFound =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, "MultiEndpointsContainerNotFound"), "MultiEndpointContainer for hub '{hub}' not found");


            private static readonly Action<ILogger, string, Exception> _failedAddingServiceEndpoint =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "FailedAddingServiceEndpoint"), "Fail to add ServiceEndpoint: {name}");


            public static void StartAddingServiceEndpoint(ILogger logger, string name)
            {
                _startAddingServiceEndpoint(logger, name, null);
            }

            public static void MultiEndpointContainerNotFound(ILogger logger, string hub)
            {
                _multiEndpointsContainerNotFound(logger, hub, null);
            }

            public static void FailedAddingServiceEndpoint(ILogger logger, string name)
            {
                _failedAddingServiceEndpoint(logger, name, null);
            }
        }
    }
}
