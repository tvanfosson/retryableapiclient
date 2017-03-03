using System.Threading;
using System.Threading.Tasks;

namespace Retryable.Net
{
    public interface IAuthorizationHandler
    {
        Task<string> Authorize(CancellationToken cancellationToken);
    }
}