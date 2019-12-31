using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    public interface IServiceScaleManager
    {
        Task AddServiceEndpoint(ServiceEndpoint endpoint);

        Task RemoveServiceEndpoint(ServiceEndpoint endpoint);
    }
}
