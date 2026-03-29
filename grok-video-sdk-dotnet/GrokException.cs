using System;

namespace GrokVideoSdk
{
    public class GrokException : Exception
    {
        public GrokErrorType ErrorType { get; private set; }
        public int StatusCode { get; private set; }
        public long RetryAfterSeconds { get; private set; }

        public GrokException(GrokErrorType errorType, string message, int statusCode = 0)
            : base(message)
        {
            ErrorType = errorType;
            StatusCode = statusCode;
        }

        public GrokException(GrokErrorType errorType, string message, Exception inner)
            : base(message, inner)
        {
            ErrorType = errorType;
        }

        public static GrokException Auth()
        {
            return new GrokException(GrokErrorType.Auth, "Authentication failed: invalid or missing API key", 401);
        }

        public static GrokException RateLimit(long retryAfter)
        {
            var ex = new GrokException(GrokErrorType.RateLimit, "Rate limit exceeded, retry after " + retryAfter + "s", 429);
            ex.RetryAfterSeconds = retryAfter;
            return ex;
        }

        public static GrokException Api(int statusCode, string message)
        {
            return new GrokException(GrokErrorType.Api, message, statusCode);
        }

        public static GrokException JobFailed(string requestId)
        {
            return new GrokException(GrokErrorType.JobFailed, "Job failed: " + requestId);
        }

        public static GrokException JobExpired(string requestId)
        {
            return new GrokException(GrokErrorType.JobExpired, "Job expired: " + requestId);
        }

        public static GrokException Timeout(long seconds)
        {
            return new GrokException(GrokErrorType.Timeout, "Timeout: polling exceeded " + seconds + "s");
        }

        public static GrokException Network(Exception inner)
        {
            return new GrokException(GrokErrorType.Network, "Network error: " + inner.Message, inner);
        }
    }

    public enum GrokErrorType
    {
        Auth,
        RateLimit,
        Api,
        JobFailed,
        JobExpired,
        Timeout,
        Network,
        InvalidResponse
    }
}
