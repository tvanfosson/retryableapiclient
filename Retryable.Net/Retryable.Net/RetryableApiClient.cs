using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
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
        private readonly IAuthorizationHandler _authorizationHandler;
        private string _token;
        private readonly int _maxAttempts;
        private readonly int _exceptionRetryDelayMs;

        protected RetryableApiClient(HttpClient client,
            IAuthorizationHandler authorizationHandler,
            int maxAttempts = DEFAULT_MAX_RETRIES,
            int exceptionRetryDelayMs = DEFAULT_RETRY_MS)
        {
            _client = client;
            _authorizationHandler = authorizationHandler;
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
                    _token = await _authorizationHandler.Authorize(cancellationToken);
                    if (_token == null)
                    {
                        throw new AuthenticationException("Authentication failed");
                    }
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
    }
}
