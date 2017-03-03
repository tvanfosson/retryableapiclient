using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Retryable.Net
{
    public class AuthorizationHandler : IAuthorizationHandler
    {
        private readonly HttpClient _client;
        private string _token;
        private readonly string _authenticationUri;
        private readonly string _username;
        private readonly string _password;

        protected AuthorizationHandler(HttpClient client, string authenticationUri,  string username, string password)
        {
            _client = client;
            _authenticationUri = authenticationUri;
            _username = username;
            _password = password;
        }

        public async Task<string> Authorize(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var credentials = new { username = _username, password = _password };
            using (var content = new StringContent(JsonConvert.SerializeObject(credentials)))
            {
                _client.DefaultRequestHeaders.Authorization = null; // clear any auth headers for this request
                var response = await _client.PostAsync(_authenticationUri, content, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }

                return null;
            }
        }
    }
}
