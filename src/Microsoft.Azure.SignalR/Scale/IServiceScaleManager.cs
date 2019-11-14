using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    public interface IServiceScaleManager
    {
        IReadOnlyList<ServiceEndpoint> AddServiceEndpoint(ServiceEndpoint endpoint);

        IReadOnlyList<ServiceEndpoint> RemoveServiceEndpoint(ServiceEndpoint endpoint);
    }
}
