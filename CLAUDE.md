# GrokVideoGenerateTool

Tool sinh video bằng xAI Grok Imagine Video API. Monorepo chứa SDK (Rust + C#) và app clients.

## Cấu trúc dự án

```
GrokVideoGenerateTool/
├── Cargo.toml                    # Rust workspace (members: grok-video-sdk, grok-video-app/src-tauri)
├── GrokVideoGenerateTool.sln     # .NET solution (chứa grok-video-sdk-dotnet)
├── grok-video-sdk/               # Rust SDK - core library
├── grok-video-sdk-dotnet/        # C# SDK - port sang .NET
└── grok-video-app/               # Tauri desktop app (React + Rust backend)
```

## grok-video-sdk (Rust)

SDK gốc viết bằng Rust, dùng `reqwest` + `tokio` + `serde`.

- `src/client.rs` — `GrokClient`: submit_job, check_status, download_video, generate_and_wait
- `src/models.rs` — Request/Response types, enums: AspectRatio, Resolution, JobStatus
- `src/error.rs` — `GrokError` enum (Auth, RateLimit, Api, JobFailed, JobExpired, Timeout, Network, InvalidResponse)

API endpoints:
- POST `{base_url}/v1/videos/generations` — submit job, returns `request_id`
- GET `{base_url}/v1/videos/{request_id}` — check status, returns progress + video URL when done

## grok-video-sdk-dotnet (C#)

Port 1:1 từ Rust SDK sang C#. Target: `net48` + `netstandard2.0`, LangVersion 7.3.

Dependency: `Newtonsoft.Json 13.0.3`

- `GrokClient.cs` — async methods: SubmitJobAsync, CheckStatusAsync, DownloadVideoAsync, GenerateAndWaitAsync, ImageFileToDataUri (static)
- `GrokException.cs` — Exception class với `GrokErrorType` enum + factory methods
- `Models/VideoGenerationRequest.cs` — Fluent builder pattern (WithDuration, WithAspectRatio, WithResolution, WithImageUrl)
- `Models/Responses.cs` — SubmitResponse, StatusResponse, VideoData, ApiErrorBody
- `Models/JobStatus.cs` — Constants: Pending, Processing, Done, Failed, Expired

Lưu ý: Dùng `ConfigureAwait(false)` xuyên suốt, tương thích WinForms/WPF (không deadlock khi gọi từ UI thread).

## grok-video-app (Tauri)

Desktop app dùng Tauri v2 + React 19 + Tailwind CSS 4 + TypeScript.

**Frontend** (`src/`): React components, Vite bundler
**Backend** (`src-tauri/`):
- `state.rs` — AppState (client, jobs HashMap, output_dir), JobInfo struct
- `commands.rs` — Tauri commands: connect_api, create_video_task, get_jobs, get_output_dir
- Flow: submit job → spawn tokio task → poll mỗi 4s → download khi done → emit "job-update" event tới frontend

Output mặc định: `~/Videos/GrokVideos/` hoặc `~/Downloads/GrokVideos/`

## API xAI Grok Video

- Base URL: `https://api.x.ai`
- Auth: Bearer token trong header
- Model mặc định: `grok-imagine-video`
- Options: duration (seconds), aspect_ratio (16:9, 9:16, 1:1, 4:3, 3:4, 3:2, 2:3), resolution (480p, 720p)
- Hỗ trợ image-to-video: truyền base64 data URI qua field `image.url`
- Flow: Submit → Poll (pending/processing) → Done (có video URL) hoặc Failed/Expired

## Commands

```bash
# Rust
cargo build                           # Build workspace
cargo build -p grok-video-sdk         # Build SDK only

# Tauri app
cd grok-video-app && npm run tauri dev

# .NET SDK
dotnet build grok-video-sdk-dotnet/GrokVideoSdk.csproj
```
