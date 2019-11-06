using Microsoft.AspNetCore.Connections;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceScaleManager: IServiceScaleManager
    {
        private IServiceProvider _serviceProvider;

        public Task ScaleCompletionTask;

        public ServiceScaleManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task AddServiceEndpoint(ServiceEndpoint endpoint)
        {
            // Set endpoint offline before connection completed.
            endpoint.Online = false;
            var serviceRouteBuilder = _serviceProvider.GetService<ServiceRouteBuilder>();
            var hubs = serviceRouteBuilder.Hubs;
            foreach (var hub in hubs)
            {
                ConnectionDelegate app = connectionContext => Task.CompletedTask;
                var type = typeof(ServiceHubDispatcher<>).MakeGenericType(hub.HubType);
                var startMethod = type.GetMethod("Start", new Type[] { typeof(ConnectionDelegate), typeof(Action<HttpContext>) });
                object dispatcher = _serviceProvider.GetRequiredService(type);

                startMethod.Invoke(dispatcher, new object[] { app, null });
            }
            return Task.CompletedTask;
        }

        public void RemoveServiceEndpoint(ServiceEndpoint endpoint)
        {
            throw new NotImplementedException();
        }
    }
}
