# GrokVideoSdk for .NET

C# SDK for the [xAI Grok Imagine Video API](https://docs.x.ai). Generate AI videos from text prompts or images.

**Targets**: .NET Framework 4.8 | .NET Standard 2.0
**Dependencies**: Newtonsoft.Json 13.0.3
**Language**: C# 7.3

---

## Installation

Add `GrokVideoSdk.dll` as a reference to your project, or include the project directly in your solution.

```xml
<!-- .csproj reference -->
<Reference Include="GrokVideoSdk">
  <HintPath>..\libs\GrokVideoSdk.dll</HintPath>
</Reference>
```

---

## Quick Start

```csharp
using GrokVideoSdk;
using GrokVideoSdk.Models;

// 1. Create client
using (var client = new GrokClient("xai-your-api-key"))
{
    // 2. Build request
    var request = new VideoGenerationRequest("A cat playing piano")
        .WithDuration(5)
        .WithAspectRatio("16:9")
        .WithResolution("720p");

    // 3. Generate and wait
    var result = await client.GenerateAndWaitAsync(
        request,
        pollInterval: TimeSpan.FromSeconds(5),
        timeout: TimeSpan.FromMinutes(10));

    // 4. Download video
    await client.DownloadVideoAsync(result.Video.Url, @"C:\output\video.mp4");
}
```

---

## API Reference

### GrokClient

Main client class. Implements `IDisposable`.

#### Constructors

```csharp
// Simple — API key only
var client = new GrokClient("xai-your-api-key");

// With custom base URL
var client = new GrokClient("xai-your-api-key", "https://custom-api.example.com");

// Full options
var client = new GrokClient("xai-your-api-key", new GrokClientOptions
{
    BaseUrl = "https://api.x.ai",
    Timeout = TimeSpan.FromSeconds(60),
    MaxRetries = 3,
    Proxy = new WebProxy("http://proxy:8080"),
    Logger = new MyLogger()
});
```

#### Methods

| Method | Description | Returns |
|--------|-------------|---------|
| `SubmitJobAsync(request, ct)` | Submit a video generation job | `Task<SubmitResponse>` |
| `CheckStatusAsync(requestId, ct)` | Check job status | `Task<StatusResponse>` |
| `DownloadVideoAsync(videoUrl, destPath, ct)` | Download completed video to file | `Task` |
| `GenerateAndWaitAsync(request, pollInterval, timeout, ct)` | All-in-one: submit, poll, return result | `Task<StatusResponse>` |
| `GenerateAndWaitAsync(request, pollInterval, timeout, onProgress, ct)` | Same as above with progress callback | `Task<StatusResponse>` |
| `ImageFileToDataUri(filePath)` | Convert local image to base64 data URI (static) | `string` |

All async methods accept an optional `CancellationToken`.

---

### VideoGenerationRequest

Fluent builder for video generation parameters.

```csharp
var request = new VideoGenerationRequest("Your prompt text")
    .WithDuration(5)           // Video duration in seconds
    .WithAspectRatio("16:9")   // 16:9, 9:16, 1:1, 4:3, 3:4, 3:2, 2:3
    .WithResolution("720p")    // 480p or 720p
    .WithImageUrl(dataUri);    // Image-to-video (base64 data URI or URL)
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Model` | `string` | Auto | Default: `"grok-imagine-video"` |
| `Prompt` | `string` | Yes | Text prompt describing the video |
| `Duration` | `int?` | No | Video duration in seconds |
| `AspectRatio` | `string?` | No | Video aspect ratio |
| `Resolution` | `string?` | No | `"480p"` or `"720p"` |
| `Image` | `ImageInput?` | No | Image input for image-to-video |

---

### StatusResponse

Returned by `CheckStatusAsync` and `GenerateAndWaitAsync`.

| Property | Type | Description |
|----------|------|-------------|
| `Status` | `string` | Job status: `"pending"`, `"processing"`, `"done"`, `"failed"`, `"expired"` |
| `Video` | `VideoData` | Video data (available when status is `"done"`) |
| `Model` | `string` | Model used |
| `Progress` | `int?` | Progress percentage (0-100) |

### VideoData

| Property | Type | Description |
|----------|------|-------------|
| `Url` | `string` | Download URL for the generated video |
| `Duration` | `float?` | Video duration in seconds |

### SubmitResponse

| Property | Type | Description |
|----------|------|-------------|
| `RequestId` | `string` | Unique ID to track the job |

### JobStatus

String constants for comparing status values.

```csharp
if (response.Status == JobStatus.Done) { /* video ready */ }
if (response.Status == JobStatus.Processing) { /* still working */ }
```

| Constant | Value |
|----------|-------|
| `JobStatus.Pending` | `"pending"` |
| `JobStatus.Processing` | `"processing"` |
| `JobStatus.Done` | `"done"` |
| `JobStatus.Failed` | `"failed"` |
| `JobStatus.Expired` | `"expired"` |

---

### GrokClientOptions

Configuration for `GrokClient`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BaseUrl` | `string` | `"https://api.x.ai"` | API base URL |
| `Timeout` | `TimeSpan` | 30s | HTTP request timeout |
| `MaxRetries` | `int` | 3 | Max retries on rate limit (429). Set 0 to disable |
| `Proxy` | `IWebProxy` | null | HTTP proxy |
| `HttpClient` | `HttpClient` | null | Custom HttpClient (overrides Proxy) |
| `Logger` | `IGrokLogger` | null | Logger for diagnostic messages |

---

### IGrokLogger

Implement this interface to capture SDK log messages.

```csharp
public class ConsoleLogger : IGrokLogger
{
    public void Log(string message)
    {
        Console.WriteLine("[GrokSDK] " + message);
    }
}
```

**Log points**: submit, job submitted, status check, download start/complete, rate limit retry.

---

### GrokException

All SDK errors throw `GrokException` with an `ErrorType` for programmatic handling.

```csharp
try
{
    var result = await client.GenerateAndWaitAsync(request, pollInterval, timeout);
}
catch (GrokException ex)
{
    switch (ex.ErrorType)
    {
        case GrokErrorType.Auth:
            // Invalid or missing API key (401/403)
            break;
        case GrokErrorType.RateLimit:
            // Rate limit exceeded (429). Check ex.RetryAfterSeconds
            break;
        case GrokErrorType.Api:
            // Other API error. Check ex.StatusCode and ex.Message
            break;
        case GrokErrorType.JobFailed:
            // Video generation failed on server side
            break;
        case GrokErrorType.JobExpired:
            // Job expired before completion
            break;
        case GrokErrorType.Timeout:
            // Polling exceeded timeout
            break;
        case GrokErrorType.Network:
            // Network connectivity issue. Check ex.InnerException
            break;
        case GrokErrorType.InvalidResponse:
            // Could not parse API response
            break;
    }
}
```

| ErrorType | When | Extra Properties |
|-----------|------|------------------|
| `Auth` | 401/403 response | — |
| `RateLimit` | 429 response | `RetryAfterSeconds` |
| `Api` | Other HTTP errors | `StatusCode` |
| `JobFailed` | Server reports job failed | — |
| `JobExpired` | Job expired | — |
| `Timeout` | Poll exceeded timeout | — |
| `Network` | Connection/DNS failure | `InnerException` |
| `InvalidResponse` | Unparseable response | — |

---

## Usage Examples

### Text-to-Video

```csharp
using (var client = new GrokClient("xai-key"))
{
    var request = new VideoGenerationRequest("A sunset over the ocean with waves crashing")
        .WithDuration(5)
        .WithAspectRatio("16:9");

    var result = await client.GenerateAndWaitAsync(
        request,
        TimeSpan.FromSeconds(5),
        TimeSpan.FromMinutes(10));

    await client.DownloadVideoAsync(result.Video.Url, "sunset.mp4");
}
```

### Image-to-Video

```csharp
using (var client = new GrokClient("xai-key"))
{
    // Convert local image to base64 data URI
    string dataUri = GrokClient.ImageFileToDataUri(@"C:\photos\person.jpg");

    var request = new VideoGenerationRequest(
            "Make this person wave and smile at the camera")
        .WithImageUrl(dataUri)
        .WithDuration(5);

    var result = await client.GenerateAndWaitAsync(
        request,
        TimeSpan.FromSeconds(5),
        TimeSpan.FromMinutes(10));

    await client.DownloadVideoAsync(result.Video.Url, "person_waving.mp4");
}
```

### Progress Tracking

```csharp
using (var client = new GrokClient("xai-key"))
{
    var request = new VideoGenerationRequest("A dancing robot");

    var result = await client.GenerateAndWaitAsync(
        request,
        TimeSpan.FromSeconds(5),
        TimeSpan.FromMinutes(10),
        onProgress: status =>
        {
            Console.WriteLine("Status: {0}, Progress: {1}%",
                status.Status, status.Progress ?? 0);
        });

    Console.WriteLine("Video URL: " + result.Video.Url);
}
```

### Manual Polling (Advanced)

For full control over the polling loop (e.g., updating UI per-step):

```csharp
using (var client = new GrokClient("xai-key"))
{
    var request = new VideoGenerationRequest("A flying eagle");
    var submitResp = await client.SubmitJobAsync(request);

    Console.WriteLine("Job submitted: " + submitResp.RequestId);

    while (true)
    {
        await Task.Delay(TimeSpan.FromSeconds(5));

        var status = await client.CheckStatusAsync(submitResp.RequestId);
        Console.WriteLine("Progress: " + (status.Progress ?? 0) + "%");

        if (status.Status == JobStatus.Done)
        {
            await client.DownloadVideoAsync(status.Video.Url, "eagle.mp4");
            break;
        }
    }
}
```

### With Proxy and Logging

```csharp
var options = new GrokClientOptions
{
    Timeout = TimeSpan.FromSeconds(60),
    MaxRetries = 5,
    Proxy = new WebProxy("http://proxy.company.com:8080"),
    Logger = new ConsoleLogger()
};

using (var client = new GrokClient("xai-key", options))
{
    // All requests go through proxy, with logging and 5 retries on rate limit
    var request = new VideoGenerationRequest("Hello world");
    var result = await client.GenerateAndWaitAsync(
        request, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(10));
}
```

### With Custom HttpClient

```csharp
// Useful for dependency injection or when reusing an HttpClient instance
var httpClient = new HttpClient(new HttpClientHandler
{
    Proxy = new WebProxy("http://proxy:8080"),
    AutomaticDecompression = DecompressionMethods.GZip
});

var options = new GrokClientOptions { HttpClient = httpClient };

using (var client = new GrokClient("xai-key", options))
{
    // Uses your custom HttpClient
    var request = new VideoGenerationRequest("Custom HTTP client demo");
    var result = await client.GenerateAndWaitAsync(
        request, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(10));
}

// Remember: you manage httpClient lifetime when using custom HttpClient
httpClient.Dispose();
```

### WinForms Integration (async without deadlock)

```csharp
// In a WinForms button click handler
private async void btnGenerate_Click(object sender, EventArgs e)
{
    btnGenerate.Enabled = false;
    lblStatus.Text = "Generating...";

    try
    {
        using (var client = new GrokClient(txtApiKey.Text))
        {
            string dataUri = GrokClient.ImageFileToDataUri(txtImagePath.Text);

            var request = new VideoGenerationRequest(txtPrompt.Text)
                .WithImageUrl(dataUri)
                .WithDuration(5);

            var result = await client.GenerateAndWaitAsync(
                request,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMinutes(10),
                onProgress: status =>
                {
                    // Safe to update UI — ConfigureAwait(false) is used internally,
                    // but the callback runs on a thread pool thread.
                    // Use Invoke for UI updates:
                    this.Invoke((Action)(() =>
                    {
                        lblStatus.Text = string.Format("Processing... {0}%",
                            status.Progress ?? 0);
                    }));
                });

            await client.DownloadVideoAsync(result.Video.Url, @"C:\output\video.mp4");
            lblStatus.Text = "Done!";
        }
    }
    catch (GrokException ex)
    {
        lblStatus.Text = "Error: " + ex.Message;
    }
    finally
    {
        btnGenerate.Enabled = true;
    }
}
```

---

## File Structure

```
grok-video-sdk-dotnet/
├── GrokVideoSdk.csproj          # Project file (net48 + netstandard2.0)
├── GrokClient.cs                # Main client — submit, poll, download
├── GrokClientOptions.cs         # Configuration (timeout, retries, proxy, logger)
├── GrokException.cs             # Exception types with GrokErrorType enum
├── IGrokLogger.cs               # Logging interface
└── Models/
    ├── VideoGenerationRequest.cs # Request builder (fluent API)
    ├── Responses.cs             # SubmitResponse, StatusResponse, VideoData
    └── JobStatus.cs             # Status constants (Pending, Processing, Done, ...)
```

---

## API Flow

```
                    ┌──────────────┐
                    │ GrokClient   │
                    └──────┬───────┘
                           │
            SubmitJobAsync │  POST /v1/videos/generations
                           ▼
                    ┌──────────────┐
                    │  xAI Server  │──→ Returns request_id
                    └──────┬───────┘
                           │
          CheckStatusAsync │  GET /v1/videos/{request_id}
           (poll every 5s) │
                           ▼
                 ┌───────────────────┐
                 │ pending/processing │──→ Keep polling
                 │ done              │──→ Video URL ready
                 │ failed/expired    │──→ Throw GrokException
                 └───────────────────┘
                           │
        DownloadVideoAsync │  GET {video_url}
                           ▼
                    ┌──────────────┐
                    │  video.mp4   │
                    └──────────────┘
```

---

## Auto-Retry Behavior

When the API returns HTTP 429 (rate limit), the SDK automatically:

1. Reads the `Retry-After` header (defaults to 5 seconds if missing)
2. Waits for the specified duration
3. Retries the request
4. Repeats up to `MaxRetries` times (default: 3)

If all retries are exhausted, throws `GrokException` with `ErrorType = RateLimit`.

Disable auto-retry:
```csharp
var options = new GrokClientOptions { MaxRetries = 0 };
```
