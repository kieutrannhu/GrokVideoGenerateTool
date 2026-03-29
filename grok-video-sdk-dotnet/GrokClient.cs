using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GrokVideoSdk.Models;
using Newtonsoft.Json;

namespace GrokVideoSdk
{
    public class GrokClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly int _maxRetries;
        private readonly IGrokLogger _logger;
        private bool _disposed;

        public GrokClient(string apiKey, string baseUrl = "https://api.x.ai")
            : this(apiKey, new GrokClientOptions { BaseUrl = baseUrl })
        {
        }

        /// <summary>
        /// Create a new client with full options (proxy, retries, logging, custom HttpClient).
        /// </summary>
        public GrokClient(string apiKey, GrokClientOptions options)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException(nameof(apiKey));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _baseUrl = (options.BaseUrl ?? "https://api.x.ai").TrimEnd('/');
            _maxRetries = Math.Max(0, options.MaxRetries);
            _logger = options.Logger;

            if (options.HttpClient != null)
            {
                _http = options.HttpClient;
            }
            else
            {
                HttpClientHandler handler = new HttpClientHandler();
                if (options.Proxy != null)
                {
                    handler.Proxy = options.Proxy;
                    handler.UseProxy = true;
                }
                _http = new HttpClient(handler);
            }

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _http.Timeout = options.Timeout > TimeSpan.Zero
                ? options.Timeout
                : TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Submit a video generation job.
        /// </summary>
        public Task<SubmitResponse> SubmitJobAsync(
            VideoGenerationRequest request,
            CancellationToken ct = default(CancellationToken))
        {
            Log("Submitting job: {0}", request.Prompt);
            return ExecuteWithRetryAsync(async token =>
            {
                var url = _baseUrl + "/v1/videos/generations";
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await _http.PostAsync(url, content, token).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    throw GrokException.Network(ex);
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                EnsureSuccess(response, body);

                SubmitResponse result;
                try
                {
                    result = JsonConvert.DeserializeObject<SubmitResponse>(body);
                }
                catch (Exception parseEx)
                {
                    throw new GrokException(GrokErrorType.InvalidResponse,
                        "Invalid response: " + parseEx.Message + " — " + body);
                }

                if (result == null || string.IsNullOrEmpty(result.RequestId))
                    throw new GrokException(GrokErrorType.InvalidResponse, "Invalid response: " + body);

                Log("Job submitted: {0}", result.RequestId);
                return result;
            }, ct);
        }

        /// <summary>
        /// Check the status of a video generation job.
        /// </summary>
        public Task<StatusResponse> CheckStatusAsync(
            string requestId,
            CancellationToken ct = default(CancellationToken))
        {
            return ExecuteWithRetryAsync(async token =>
            {
                var url = _baseUrl + "/v1/videos/" + requestId;

                HttpResponseMessage response;
                try
                {
                    response = await _http.GetAsync(url, token).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    throw GrokException.Network(ex);
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                EnsureSuccess(response, body);

                StatusResponse result;
                try
                {
                    result = JsonConvert.DeserializeObject<StatusResponse>(body);
                }
                catch (Exception parseEx)
                {
                    throw new GrokException(GrokErrorType.InvalidResponse,
                        "Invalid response: " + parseEx.Message + " — " + body);
                }

                if (result == null)
                    throw new GrokException(GrokErrorType.InvalidResponse, "Invalid response: " + body);

                if (result.Status == JobStatus.Failed)
                    throw GrokException.JobFailed(requestId);
                if (result.Status == JobStatus.Expired)
                    throw GrokException.JobExpired(requestId);

                Log("Status {0}: {1} (progress: {2})", requestId, result.Status, result.Progress);
                return result;
            }, ct);
        }

        /// <summary>
        /// Download a completed video to the specified file path.
        /// </summary>
        public async Task DownloadVideoAsync(
            string videoUrl,
            string destPath,
            CancellationToken ct = default(CancellationToken))
        {
            Log("Downloading video to {0}", destPath);

            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw GrokException.Network(ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw GrokException.Api(
                    (int)response.StatusCode,
                    "Failed to download video");
            }

            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var file = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                await stream.CopyToAsync(file, 81920, ct).ConfigureAwait(false);
            }

            Log("Download complete: {0}", destPath);
        }

        /// <summary>
        /// Submit a job, poll until completion, and return the final status.
        /// </summary>
        public Task<StatusResponse> GenerateAndWaitAsync(
            VideoGenerationRequest request,
            TimeSpan pollInterval,
            TimeSpan timeout,
            CancellationToken ct = default(CancellationToken))
        {
            return GenerateAndWaitAsync(request, pollInterval, timeout, null, ct);
        }

        /// <summary>
        /// Submit a job, poll until completion with progress callback, and return the final status.
        /// </summary>
        /// <param name="onProgress">Called on each poll with the current StatusResponse (includes Progress and Status).</param>
        public async Task<StatusResponse> GenerateAndWaitAsync(
            VideoGenerationRequest request,
            TimeSpan pollInterval,
            TimeSpan timeout,
            Action<StatusResponse> onProgress,
            CancellationToken ct = default(CancellationToken))
        {
            var submitResp = await SubmitJobAsync(request, ct).ConfigureAwait(false);
            var requestId = submitResp.RequestId;
            var deadline = DateTime.UtcNow + timeout;

            while (true)
            {
                if (DateTime.UtcNow > deadline)
                    throw GrokException.Timeout((long)timeout.TotalSeconds);

                await Task.Delay(pollInterval, ct).ConfigureAwait(false);

                var status = await CheckStatusAsync(requestId, ct).ConfigureAwait(false);

                if (onProgress != null)
                    onProgress(status);

                if (status.Status == JobStatus.Done)
                    return status;
            }
        }

        /// <summary>
        /// Convert a local image file to a base64 data URI for image-to-video.
        /// </summary>
        public static string ImageFileToDataUri(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string mime;
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    mime = "image/jpeg";
                    break;
                case ".png":
                    mime = "image/png";
                    break;
                case ".webp":
                    mime = "image/webp";
                    break;
                default:
                    mime = "image/png";
                    break;
            }

            var bytes = File.ReadAllBytes(filePath);
            var b64 = Convert.ToBase64String(bytes);
            return "data:" + mime + ";base64," + b64;
        }

        private async Task<T> ExecuteWithRetryAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken ct)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    return await action(ct).ConfigureAwait(false);
                }
                catch (GrokException ex) when (
                    ex.ErrorType == GrokErrorType.RateLimit && attempt < _maxRetries)
                {
                    attempt++;
                    var delaySec = ex.RetryAfterSeconds > 0 ? ex.RetryAfterSeconds : 5;
                    Log("Rate limited, retry {0}/{1} after {2}s", attempt, _maxRetries, delaySec);
                    await Task.Delay(TimeSpan.FromSeconds(delaySec), ct).ConfigureAwait(false);
                }
            }
        }

        private void Log(string format, params object[] args)
        {
            if (_logger != null)
                _logger.Log(string.Format(format, args));
        }

        private void EnsureSuccess(HttpResponseMessage response, string body)
        {
            if (response.IsSuccessStatusCode)
                return;

            var code = (int)response.StatusCode;

            if (code == 401 || code == 403)
                throw GrokException.Auth();

            if (code == 429)
            {
                long retryAfter = 60;
                if (response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue)
                    retryAfter = (long)response.Headers.RetryAfter.Delta.Value.TotalSeconds;

                throw GrokException.RateLimit(retryAfter);
            }

            // Try to parse API error body
            var message = "API returned status " + code;
            try
            {
                var errorBody = JsonConvert.DeserializeObject<ApiErrorBody>(body);
                if (errorBody != null && errorBody.Error != null && !string.IsNullOrEmpty(errorBody.Error.Message))
                    message = errorBody.Error.Message;
                else if (!string.IsNullOrEmpty(body))
                    message = "API returned status " + code + ": " + body;
            }
            catch
            {
                if (!string.IsNullOrEmpty(body))
                    message = "API returned status " + code + ": " + body;
            }

            throw GrokException.Api(code, message);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _http.Dispose();
                _disposed = true;
            }
        }
    }
}
