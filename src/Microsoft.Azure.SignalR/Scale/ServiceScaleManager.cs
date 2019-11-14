using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceScaleManager : IServiceScaleManager
    {
        private IServiceEndpointManager _serviceEndpointManager;
        private ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private bool _readyForClient { get; set; } = false;

        public ServiceScaleManager(IServiceEndpointManager serviceEndpointManager, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)); ;
            _logger = loggerFactory?.CreateLogger<ServiceScaleManager>();
            _serviceEndpointManager = serviceEndpointManager;
        }

        // TODO: ability to configure route policy when add service endpoint
        public IReadOnlyList<ServiceEndpoint> AddServiceEndpoint(ServiceEndpoint serviceEndpoint)
        {
            if (!_serviceEndpointManager.Endpoints.Contains(serviceEndpoint))
            {
                // Add new endpoint to MultiServiceEndpoint container to enable server routing
                var result = AddServiceEndpointToContainer(serviceEndpoint);

                // Add the new endpoint to ServiceEndpointManager to enable client negotiation only when routing successfully added.
                if (result.All(x => x))
                {
                    _serviceEndpointManager.AddServiceEndpoint(serviceEndpoint);
                }
                else
                {
                    Log.FailedAddingServiceEndpoint(_logger, serviceEndpoint.Name);
                }
            }

            return _serviceEndpointManager.GetEndpoints(_serviceEndpointManager.GetHubs().FirstOrDefault());
        }

        public IReadOnlyList<ServiceEndpoint> RemoveServiceEndpoint(ServiceEndpoint endpoint)
        {
            throw new NotImplementedException();
        }

        private bool[] AddServiceEndpointToContainer(ServiceEndpoint serviceEndpoint)
        {
            return _serviceEndpointManager.GetHubs().Select(h => StartServiceConnectionAsync(h, serviceEndpoint)).ToArray();
        }

        private bool StartServiceConnectionAsync(string hub, ServiceEndpoint serviceEndpoint)
        {
            var container = _serviceEndpointManager.GetServiceConnectionContainer(hub);
            var hubEndpoint = _serviceEndpointManager.GenerateHubServiceEndpoint(hub, serviceEndpoint);
            if (container == null)
            {
                Log.MultiEndpointContainerNotFound(_logger, hub);
                return false;
            }
            else
            {
                return container.AddServiceEndpoint(hubEndpoint, _loggerFactory);
            }
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
