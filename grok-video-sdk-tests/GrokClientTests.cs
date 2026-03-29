using System.Net;
using GrokVideoSdk;
using GrokVideoSdk.Models;

namespace GrokVideoSdk.Tests;

public class GrokClientTests
{
    private static GrokClient CreateClient(MockHttpHandler handler, int maxRetries = 0)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.x.ai")
        };
        return new GrokClient("test-key", new GrokClientOptions
        {
            HttpClient = httpClient,
            MaxRetries = maxRetries,
            Timeout = TimeSpan.FromSeconds(10)
        });
    }

    // ==================== Happy Path ====================

    [Fact]
    public async Task SubmitJobAsync_ReturnsRequestId()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"request_id\": \"req-123\"}");

        using var client = CreateClient(handler);
        var request = new VideoGenerationRequest("A cat");

        var result = await client.SubmitJobAsync(request);

        Assert.Equal("req-123", result.RequestId);
        Assert.Single(handler.SentRequests);
        Assert.Equal(HttpMethod.Post, handler.SentRequests[0].Method);
        Assert.Contains("/v1/videos/generations", handler.SentRequests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task CheckStatusAsync_ReturnsDone()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"status\": \"done\", \"progress\": 100, \"video\": {\"url\": \"https://cdn.x.ai/video.mp4\", \"duration\": 5.0}}");

        using var client = CreateClient(handler);
        var result = await client.CheckStatusAsync("req-123");

        Assert.Equal(JobStatus.Done, result.Status);
        Assert.Equal(100, result.Progress);
        Assert.NotNull(result.Video);
        Assert.Equal("https://cdn.x.ai/video.mp4", result.Video.Url);
    }

    [Fact]
    public async Task CheckStatusAsync_ReturnsProcessing()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"status\": \"processing\", \"progress\": 45}");

        using var client = CreateClient(handler);
        var result = await client.CheckStatusAsync("req-123");

        Assert.Equal(JobStatus.Processing, result.Status);
        Assert.Equal(45, result.Progress);
    }

    [Fact]
    public async Task GenerateAndWaitAsync_HappyPath()
    {
        var handler = new MockHttpHandler();
        // Submit
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"request_id\": \"req-456\"}");
        // Poll 1: processing
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"status\": \"processing\", \"progress\": 50}");
        // Poll 2: done
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"status\": \"done\", \"progress\": 100, \"video\": {\"url\": \"https://cdn.x.ai/out.mp4\"}}");

        using var client = CreateClient(handler);
        var progressUpdates = new List<StatusResponse>();

        var result = await client.GenerateAndWaitAsync(
            new VideoGenerationRequest("Test"),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromSeconds(30),
            onProgress: s => progressUpdates.Add(s));

        Assert.Equal(JobStatus.Done, result.Status);
        Assert.Equal(2, progressUpdates.Count);
        Assert.Equal(JobStatus.Processing, progressUpdates[0].Status);
        Assert.Equal(JobStatus.Done, progressUpdates[1].Status);
    }

    [Fact]
    public async Task DownloadVideoAsync_SavesFile()
    {
        var handler = new MockHttpHandler();
        var videoBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(videoBytes)
        });

        var tempFile = Path.GetTempFileName();
        try
        {
            using var client = CreateClient(handler);
            await client.DownloadVideoAsync("https://cdn.x.ai/video.mp4", tempFile);

            var savedBytes = await File.ReadAllBytesAsync(tempFile);
            Assert.Equal(videoBytes, savedBytes);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ImageFileToDataUri_Jpg()
    {
        var tempFile = Path.GetTempFileName();
        var jpgFile = Path.ChangeExtension(tempFile, ".jpg");
        File.Move(tempFile, jpgFile);
        try
        {
            File.WriteAllBytes(jpgFile, new byte[] { 0xFF, 0xD8 });
            var result = GrokClient.ImageFileToDataUri(jpgFile);
            Assert.StartsWith("data:image/jpeg;base64,", result);
        }
        finally
        {
            File.Delete(jpgFile);
        }
    }

    [Fact]
    public void ImageFileToDataUri_Png()
    {
        var tempFile = Path.GetTempFileName();
        var pngFile = Path.ChangeExtension(tempFile, ".png");
        File.Move(tempFile, pngFile);
        try
        {
            File.WriteAllBytes(pngFile, new byte[] { 0x89, 0x50 });
            var result = GrokClient.ImageFileToDataUri(pngFile);
            Assert.StartsWith("data:image/png;base64,", result);
        }
        finally
        {
            File.Delete(pngFile);
        }
    }

    // ==================== Error Cases ====================

    [Fact]
    public async Task SubmitJobAsync_Auth401_ThrowsAuth()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.Unauthorized, "{}");

        using var client = CreateClient(handler);
        var ex = await Assert.ThrowsAsync<GrokException>(
            () => client.SubmitJobAsync(new VideoGenerationRequest("test")));

        Assert.Equal(GrokErrorType.Auth, ex.ErrorType);
    }

    [Fact]
    public async Task SubmitJobAsync_Auth403_ThrowsAuth()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.Forbidden, "{}");

        using var client = CreateClient(handler);
        var ex = await Assert.ThrowsAsync<GrokException>(
            () => client.SubmitJobAsync(new VideoGenerationRequest("test")));

        Assert.Equal(GrokErrorType.Auth, ex.ErrorType);
    }

    [Fact]
    public async Task SubmitJobAsync_RateLimit429_ThrowsRateLimit()
    {
        var handler = new MockHttpHandler();
        var response = new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("{}")
        };
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        handler.EnqueueResponse(response);

        using var client = CreateClient(handler, maxRetries: 0);
        var ex = await Assert.ThrowsAsync<GrokException>(
            () => client.SubmitJobAsync(new VideoGenerationRequest("test")));

        Assert.Equal(GrokErrorType.RateLimit, ex.ErrorType);
        Assert.Equal(30, ex.RetryAfterSeconds);
    }

    [Fact]
    public async Task SubmitJobAsync_ServerError500_ThrowsApi()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.InternalServerError,
            "{\"error\": {\"message\": \"Internal server error\"}}");

        using var client = CreateClient(handler);
        var ex = await Assert.ThrowsAsync<GrokException>(
            () => client.SubmitJobAsync(new VideoGenerationRequest("test")));

        Assert.Equal(GrokErrorType.Api, ex.ErrorType);
        Assert.Equal(500, ex.StatusCode);
        Assert.Contains("Internal server error", ex.Message);
    }

    [Fact]
    public async Task SubmitJobAsync_InvalidJson_ThrowsInvalidResponse()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "not json at all");

        using var client = CreateClient(handler);
        var ex = await Assert.ThrowsAsync<GrokException>(
            () => client.SubmitJobAsync(new VideoGenerationRequest("test")));

        Assert.Equal(GrokErrorType.InvalidResponse, ex.ErrorType);
    }

    [Fact]
    public async Task CheckStatusAsync_JobFailed_ThrowsJobFailed()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"status\": \"failed\"}");

        using var client = CreateClient(handler);
        var ex = await Assert.ThrowsAsync<GrokException>(
            () => client.CheckStatusAsync("req-123"));

        Assert.Equal(GrokErrorType.JobFailed, ex.ErrorType);
    }

    [Fact]
    public async Task CheckStatusAsync_JobExpired_ThrowsJobExpired()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"status\": \"expired\"}");

        using var client = CreateClient(handler);
        var ex = await Assert.ThrowsAsync<GrokException>(
            () => client.CheckStatusAsync("req-123"));

        Assert.Equal(GrokErrorType.JobExpired, ex.ErrorType);
    }

    [Fact]
    public async Task DownloadVideoAsync_404_ThrowsApi()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.NotFound, "");

        var tempFile = Path.GetTempFileName();
        try
        {
            using var client = CreateClient(handler);
            var ex = await Assert.ThrowsAsync<GrokException>(
                () => client.DownloadVideoAsync("https://cdn.x.ai/missing.mp4", tempFile));

            Assert.Equal(GrokErrorType.Api, ex.ErrorType);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GenerateAndWaitAsync_Timeout_ThrowsTimeout()
    {
        var handler = new MockHttpHandler();
        // Submit OK
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"request_id\": \"req-timeout\"}");
        // Always processing — will never be done
        for (int i = 0; i < 50; i++)
        {
            handler.EnqueueResponse(HttpStatusCode.OK,
                "{\"status\": \"processing\", \"progress\": 10}");
        }

        using var client = CreateClient(handler);
        var ex = await Assert.ThrowsAsync<GrokException>(
            () => client.GenerateAndWaitAsync(
                new VideoGenerationRequest("test"),
                TimeSpan.FromMilliseconds(10),
                TimeSpan.FromMilliseconds(200)));

        Assert.Equal(GrokErrorType.Timeout, ex.ErrorType);
    }

    // ==================== Retry ====================

    [Fact]
    public async Task SubmitJobAsync_RetryOnRateLimit_SucceedsAfterRetry()
    {
        var handler = new MockHttpHandler();
        // First: 429
        var rateLimitResp = new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("{}")
        };
        rateLimitResp.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
        handler.EnqueueResponse(rateLimitResp);
        // Second: OK
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"request_id\": \"req-retry\"}");

        using var client = CreateClient(handler, maxRetries: 2);
        var result = await client.SubmitJobAsync(new VideoGenerationRequest("retry test"));

        Assert.Equal("req-retry", result.RequestId);
        Assert.Equal(2, handler.SentRequests.Count); // 1 failed + 1 success
    }

    [Fact]
    public async Task SubmitJobAsync_RetryExhausted_ThrowsRateLimit()
    {
        var handler = new MockHttpHandler();
        // 3 rate limits = exhaust maxRetries=2 (initial + 2 retries = 3 attempts)
        for (int i = 0; i < 3; i++)
        {
            var resp = new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("{}")
            };
            resp.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(50));
            handler.EnqueueResponse(resp);
        }

        using var client = CreateClient(handler, maxRetries: 2);
        var ex = await Assert.ThrowsAsync<GrokException>(
            () => client.SubmitJobAsync(new VideoGenerationRequest("exhaust")));

        Assert.Equal(GrokErrorType.RateLimit, ex.ErrorType);
        Assert.Equal(3, handler.SentRequests.Count);
    }

    // ==================== Cancellation ====================

    [Fact]
    public async Task SubmitJobAsync_Cancelled_ThrowsOperationCancelled()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"request_id\": \"req-cancel\"}");

        using var client = CreateClient(handler);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SubmitJobAsync(new VideoGenerationRequest("cancel"), cts.Token));
    }

    [Fact]
    public async Task GenerateAndWaitAsync_CancelDuringPoll_ThrowsOperationCancelled()
    {
        var handler = new MockHttpHandler();
        // Submit OK
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"request_id\": \"req-poll-cancel\"}");
        // Processing responses (enough for a few polls)
        for (int i = 0; i < 10; i++)
        {
            handler.EnqueueResponse(HttpStatusCode.OK,
                "{\"status\": \"processing\", \"progress\": 30}");
        }

        using var client = CreateClient(handler);
        var cts = new CancellationTokenSource();

        // Cancel after a short delay to allow submit + 1 poll
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GenerateAndWaitAsync(
                new VideoGenerationRequest("poll cancel"),
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromSeconds(60),
                ct: cts.Token));
    }

    // ==================== Logging ====================

    [Fact]
    public async Task Logger_ReceivesMessages()
    {
        var handler = new MockHttpHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"request_id\": \"req-log\"}");

        var logs = new List<string>();
        var logger = new TestLogger(logs);

        var httpClient = new HttpClient(handler);
        using var client = new GrokClient("test-key", new GrokClientOptions
        {
            HttpClient = httpClient,
            Logger = logger,
            MaxRetries = 0
        });

        await client.SubmitJobAsync(new VideoGenerationRequest("log test"));

        Assert.True(logs.Count >= 2); // "Submitting job" + "Job submitted"
        Assert.Contains(logs, l => l.Contains("Submitting job"));
        Assert.Contains(logs, l => l.Contains("Job submitted"));
    }

    // ==================== Constructor Validation ====================

    [Fact]
    public void Constructor_NullApiKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GrokClient(null!));
    }

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GrokClient("  "));
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GrokClient("key", (GrokClientOptions)null!));
    }

    // ==================== Request Building ====================

    [Fact]
    public void VideoGenerationRequest_FluentBuilder()
    {
        var request = new VideoGenerationRequest("my prompt")
            .WithDuration(10)
            .WithAspectRatio("9:16")
            .WithResolution("720p")
            .WithImageUrl("data:image/png;base64,abc");

        Assert.Equal("my prompt", request.Prompt);
        Assert.Equal(10, request.Duration);
        Assert.Equal("9:16", request.AspectRatio);
        Assert.Equal("720p", request.Resolution);
        Assert.NotNull(request.Image);
        Assert.Equal("data:image/png;base64,abc", request.Image.Url);
        Assert.Equal("grok-imagine-video", request.Model);
    }

    // ==================== Helpers ====================

    private class TestLogger : IGrokLogger
    {
        private readonly List<string> _logs;
        public TestLogger(List<string> logs) => _logs = logs;
        public void Log(string message) => _logs.Add(message);
    }
}
