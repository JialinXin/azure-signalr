using System;
using System.Linq;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Azure.SignalR.Startup
{
    public class AzureSignalRHostedService
    {
#if NETCOREAPP3_0
        private readonly EndpointDataSource _dataSource;
#endif
        private readonly IServiceProvider _serviceProvider;

        public AzureSignalRHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

#if NETCOREAPP3_0
        public AzureSignalRHostedService(EndpointDataSource dataSource, IServiceProvider serviceProvider)
        {
            _dataSource = dataSource;
            _serviceProvider = serviceProvider;
        }
#endif

        public void Start()
        {
#if NETCOREAPP3_0
            // Get a list of all registered hubs
            var hubTypes = _dataSource.Endpoints.Select(e => e.Metadata.GetMetadata<HubMetadata>()?.HubType)
                                   .Where(hubType => hubType != null)
                                   .Distinct()
                                   .ToList();

            // Make late bound version of the hub dispatcher
            var dispatcher = new ServiceHubDispatcher(_serviceProvider);

            foreach (var hubType in hubTypes)
            {
                // Start the application for each of the hub types
                var app = new ConnectionBuilder(_serviceProvider)
                                .UseHub(hubType)
                                .Build();

                dispatcher.Start(hubType, app);
            }
#endif
        }
    }
}
