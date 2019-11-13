using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    public interface IServiceScaleManager
    {
        Task AddServiceEndpoint(ServiceEndpoint endpoint);

        void RemoveServiceEndpoint(ServiceEndpoint endpoint);
    }
}
