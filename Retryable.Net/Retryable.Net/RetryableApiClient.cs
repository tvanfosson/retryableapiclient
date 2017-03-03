using System;
using System.Net;
using System.Net.Http;
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
            return await Try(() => _client.GetAsync(requestUri), new CancellationToken());
        }

        protected async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken)
        {
            return await Try(() => _client.GetAsync(requestUri, cancellationToken), cancellationToken);
        }

        protected async Task<HttpResponseMessage> PostAsync<T>(string requestUri, T content)
        {
            return await Try(() =>
            {
                using (var serializedContent = new StringContent(JsonConvert.SerializeObject(content)))
                {
                    return _client.PostAsync(requestUri, serializedContent);
                }
            }, new CancellationToken());
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
            var attempt = 0;

            while (true)
            {
                if (attempt > _maxAttempts)
                {
                    throw new RetriesExceededException();
                }

                if (_token == null)
                {
                    _token = await Authorize(cancellationToken);
                }

                try
                {
                    var response = await makeRequest();
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                            _token = null;
                            ++attempt;
                            break;
                        default:
                            return response;
                    }
                }
                catch
                {
                    // on exceptions delay before retrying
                    ++attempt;
                    await Task.Delay(_exceptionRetryDelayMs, cancellationToken);
                }
            }
        }

        private async Task<string> Authorize(CancellationToken cancellationToken)
        {
            var credentials = new { username = _username, password = _password };
            using (var content = new StringContent(JsonConvert.SerializeObject(credentials)))
            {
                var response = await _client.PostAsync(_authenticationUri, content, cancellationToken);
                return _tokenExtractor(response);
            }
        }
    }
}
