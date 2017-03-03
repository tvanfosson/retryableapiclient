using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Retryable.Net
{
    public abstract class RetryableApiClient
    {
        // ReSharper disable InconsistentNaming
        private const int DEFAULT_MAX_RETRIES = 5;
        private const int DEFAULT_RETRY_MS = 200;
        private const string BearerAuth = "Bearer";
        // ReSharper restore InconsistentNaming

        private readonly HttpClient _client;
        private string _token;
        private readonly string _authenticationUri;
        private readonly string _username;
        private readonly string _password;
        private readonly Func<HttpResponseMessage, string> _tokenExtractor;
        private readonly int _maxAttempts;
        private readonly int _exceptionRetryDelayMs;

        protected RetryableApiClient(HttpClient client,
            string authenticationUri,
            string username,
            string password,
            Func<HttpResponseMessage, string> tokenExtractor,
            int maxAttempts = DEFAULT_MAX_RETRIES,
            int exceptionRetryDelayMs = DEFAULT_RETRY_MS)
        {
            _client = client;
            _authenticationUri = authenticationUri;
            _username = username;
            _password = password;
            _tokenExtractor = tokenExtractor;
            _maxAttempts = maxAttempts;
            _exceptionRetryDelayMs = exceptionRetryDelayMs;
        }

        protected async Task<HttpResponseMessage> GetAsync(string requestUri)
        {
            var tokenSource = new CancellationTokenSource();
            return await Try(() => _client.GetAsync(requestUri, tokenSource.Token), tokenSource.Token);
        }

        protected async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken)
        {
            return await Try(() => _client.GetAsync(requestUri, cancellationToken), cancellationToken);
        }

        protected async Task<HttpResponseMessage> PostAsync<T>(string requestUri, T content)
        {
            var tokenSource = new CancellationTokenSource();

            return await Try(() =>
            {
                using (var serializedContent = new StringContent(JsonConvert.SerializeObject(content)))
                {
                    return _client.PostAsync(requestUri, serializedContent, tokenSource.Token);
                }
            }, tokenSource.Token);
        }

        protected async Task<HttpResponseMessage> PostAsync<T>(string requestUri, T content, CancellationToken cancellationToken)
        {
            return await Try(() =>
            {
                using (var serializedContent = new StringContent(JsonConvert.SerializeObject(content)))
                {
                    return _client.PostAsync(requestUri, serializedContent, cancellationToken);
                }
            }, cancellationToken);
        }


        private async Task<HttpResponseMessage> Try(Func<Task<HttpResponseMessage>> makeRequest, CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < _maxAttempts; ++attempt)
            {
                if (_token == null)
                {
                    _token = await Authorize(cancellationToken);
                }

                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(BearerAuth, _token);

                    var response = await makeRequest();
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _token = null;
                    }
                    else
                    {
                        return response;
                    }
                }
                catch
                {
                    // on exceptions delay before retrying
                    await Task.Delay(_exceptionRetryDelayMs, cancellationToken);
                }
            }

            throw new RetriesExceededException();

        }

        private async Task<string> Authorize(CancellationToken cancellationToken)
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
                return _tokenExtractor(response);
            }
        }
    }
}
