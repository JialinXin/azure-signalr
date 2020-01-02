using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    interface IMultiEndpointServiceConnectionContainer : IServiceConnectionContainer
    {
        bool AddServiceEndpoint(HubServiceEndpoint endpoint, ILoggerFactory loggerFactory);

        IReadOnlyList<HubServiceEndpoint> HubEndpoints { get; }

        bool IsStable { get; }
    }
}
