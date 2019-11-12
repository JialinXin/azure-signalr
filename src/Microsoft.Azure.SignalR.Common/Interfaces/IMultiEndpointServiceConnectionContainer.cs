using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    interface IMultiEndpointServiceConnectionContainer : IServiceConnectionContainer
    {
        Task AddServiceEndpoint(HubServiceEndpoint endpoint, ILoggerFactory loggerFactory);
    }
}
