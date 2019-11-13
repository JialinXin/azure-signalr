using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceScaleManager : IServiceScaleManager
    {
        private IServiceEndpointManager _serviceEndpointManager;
        private ILoggerFactory _loggerFactory;

        public ServiceScaleManager(IServiceEndpointManager serviceEndpointManager, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _serviceEndpointManager = serviceEndpointManager;
        }

        // TODO: ability to configure route policy when add service endpoint
        public Task AddServiceEndpoint(ServiceEndpoint serviceEndpoint)
        {
            if (!IsExist(serviceEndpoint))
            {
                // Add new endpoint to MultiServiceEndpoint container to enable server routing
                AddEndpointToContainer(serviceEndpoint);

                // Add the new endpoint to ServiceEndpointManager to enable client negotiation
                _serviceEndpointManager.AddServiceEndpoint(serviceEndpoint);
            }

            return Task.CompletedTask;
        }

        public void RemoveServiceEndpoint(ServiceEndpoint endpoint)
        {
            throw new NotImplementedException();
        }

        private Task AddEndpointToContainer(ServiceEndpoint serviceEndpoint)
        {
            return Task.WhenAll(_serviceEndpointManager.GetHubs().Select(h => StartServiceConnectionAsync(h, serviceEndpoint)));
        }

        private async Task StartServiceConnectionAsync(string hub, ServiceEndpoint serviceEndpoint)
        {
            var container = _serviceEndpointManager.GetServiceConnectionContainer(hub);
            var hubEndpoint = _serviceEndpointManager.GenerateHubServiceEndpoint(hub, serviceEndpoint);
            await container.AddServiceEndpoint(hubEndpoint, _loggerFactory);
        }

        private bool IsExist(ServiceEndpoint serviceEndpoint)
        {
            // Check if endpoint already exists
            return _serviceEndpointManager.Endpoints.Contains(serviceEndpoint);
        }
    }
}
