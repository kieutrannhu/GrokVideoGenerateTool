using System;
using System.Net;
using System.Net.Http;

namespace GrokVideoSdk
{
    /// <summary>
    /// Configuration options for GrokClient.
    /// </summary>
    public class GrokClientOptions
    {
        /// <summary>
        /// Base URL for the xAI API. Default: "https://api.x.ai".
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.x.ai";

        /// <summary>
        /// HTTP request timeout. Default: 30 seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Max retries on rate limit (429) errors. Default: 3.
        /// Set to 0 to disable auto-retry.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Optional proxy for HTTP requests.
        /// </summary>
        public IWebProxy Proxy { get; set; }

        /// <summary>
        /// Optional custom HttpClient. When provided, Proxy setting is ignored.
        /// The caller is responsible for the HttpClient's lifetime.
        /// </summary>
        public HttpClient HttpClient { get; set; }

        /// <summary>
        /// Optional logger for SDK diagnostic messages.
        /// </summary>
        public IGrokLogger Logger { get; set; }
    }
}
