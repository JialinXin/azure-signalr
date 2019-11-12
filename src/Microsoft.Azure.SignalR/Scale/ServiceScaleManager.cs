using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceScaleManager : IServiceScaleManager
    {
        private IServiceProvider _serviceProvider;
        private IServiceEndpointManager _serviceEndpointManager;
        private ILoggerFactory _loggerFactory;

        public ServiceScaleManager(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory;
            _serviceEndpointManager = _serviceProvider.GetRequiredService<IServiceEndpointManager>();
        }

        public Task AddServiceEndpoint(ServiceEndpoint serviceEndpoint)
        {
            // Add new endpoint to MultiServiceEndpoint container to enable server routing
            AddEndpointToContainer(serviceEndpoint);

            // Add new endpoint to ServiceEndpointManager to enable client negotiation
            _serviceEndpointManager.AddServiceEndpoint(serviceEndpoint);

            return Task.CompletedTask;
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

        public void RemoveServiceEndpoint(ServiceEndpoint endpoint)
        {
            throw new NotImplementedException();
        }
    }
}
