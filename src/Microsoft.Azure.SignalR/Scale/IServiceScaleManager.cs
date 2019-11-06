using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    public interface IServiceScaleManager
    {
        Task AddServiceEndpoint(ServiceEndpoint endpoint);

        void RemoveServiceEndpoint(ServiceEndpoint endpoint);
    }
}
