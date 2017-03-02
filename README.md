# retryableapiclient

A *sketch* of a retryable HttpClient implementation

The idea is that you would derive from this base class to create an API-specific client. Internally your API-specific methods would use the GetAsync and PostAsync methods to access the API. These methods
internally use a retry framework that detect unauthorized responses
and re-authenticate with credentials. In the event that an exception
is thrown, it simply retries the request after a specifiable amount
of time.

This code is almost certainly wrong. I haven't tested it. I haven't
used it in a real project. It's essentially a translation of
some retry code I have for another use case into an API context.

I hope to write a sample implementation with tests that use this.