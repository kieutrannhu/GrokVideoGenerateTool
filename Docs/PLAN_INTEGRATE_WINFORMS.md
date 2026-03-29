# Plan: Tích hợp Grok AI Video vào EasyClip WinForms

## Context

EasyClip là app WinForms (.NET 4.8) chỉnh sửa video: cắt ghép, thêm caption, TTS lồng tiếng, render video Wide & Short. Cần thêm tính năng **gen AI video từ ảnh + lời thoại** bằng xAI Grok Imagine Video API.

**Repos liên quan**:
- SDK: `/Users/nhukt/Codes/GrokVideoGenerateTool/grok-video-sdk-dotnet/`
- App WinForms: `/Users/nhukt/Codes/EasyClip/EasyClip_Winform_2025/`

**Flow mong muốn**: User chọn ảnh từ máy + nhập lời thoại → tự động ghép vào prompt template → gửi Grok API → poll → download video → video trở thành media của row trong pipeline.

---

## Phase 1: Tham chiếu SDK

- Build `GrokVideoSdk.dll` (target net48) — gồm cả `GrokClientOptions`, `IGrokLogger`
- Copy DLL + `Newtonsoft.Json.dll` vào folder `libs/` trong EasyClip repo
- Thêm `<Reference>` vào `ReviewMovie.csproj` (theo pattern hiện có với SubtitlesParser.dll)
- Verify: Newtonsoft.Json version không conflict với bản EasyClip đang dùng

**SDK features đã sẵn sàng**: auto-retry rate limit (max 3), progress callback, proxy support, logging interface.

**Files**: `ReviewMovie.csproj`

---

## Phase 2: Data Layer

- Thêm vào `ConfigModel.cs`:
  - `GrokEnabled` (bool, default false)
  - `GrokApiKey` (string)
  - `GrokDuration` (int, default 5) — duration video gen (seconds)
  - `GrokAspectRatio` (string, default "16:9")
  - `GrokPromptTemplate` (string, default template)
- Thêm save/load trong `ConfigDataService.cs`
- Tạo `GrokVideoContextModel.cs` — context model chứa:
  - `RowIndex`, `ImagePath`, `DialogueText`, `PromptTemplate`
  - `GrokApiKey`, `OutputVideoPath`
  - Callbacks: `UpdateCellCallback`, `SetStatusCallback`, `OnProgressUpdate`

**Files**:
- `ReviewMovie/Infrastructure/Config/ConfigModel.cs` (sửa)
- `ReviewMovie/Infrastructure/Config/ConfigDataService.cs` (sửa)
- `ReviewMovie/Model/GrokVideoContextModel.cs` (mới)

---

## Phase 3: Service Layer

Tạo `GrokVideoService.cs` theo pattern của `AudioConvertService`.

### Khởi tạo GrokClient

```csharp
var options = new GrokClientOptions
{
    MaxRetries = 3,              // auto-retry khi rate limit
    Timeout = TimeSpan.FromSeconds(60),
    Logger = new GrokConsoleLogger()  // implement IGrokLogger, ghi vào debug/log file
};
var client = new GrokClient(apiKey, options);
```

- `GrokClient` là `IDisposable` → **dispose khi batch xong** hoặc khi app đóng
- Tạo 1 instance dùng chung cho cả batch, dispose sau khi tất cả job hoàn tất

### Method 1: `GenerateSingleAsync` (xử lý 1 row)

1. Validate inputs (ảnh tồn tại, text không rỗng)
2. **Kiểm tra kích thước ảnh** — nếu > 5MB, resize xuống trước khi convert base64 (tránh tốn RAM + API reject)
3. Convert ảnh → base64 data URI qua `GrokClient.ImageFileToDataUri()`
4. Ghép lời thoại vào prompt template:
   ```
   "Make this character in the image say the following dialogue naturally,
    with appropriate lip movements and facial expressions: \"{0}\""
   ```
5. Tạo `VideoGenerationRequest` với prompt + `WithImageUrl(dataUri)` + duration + aspectRatio
6. `SubmitJobAsync` → lấy requestId (auto-retry nếu bị rate limit)
7. Poll `CheckStatusAsync` mỗi 5s, report progress qua callback row
8. Khi Done → `DownloadVideoAsync` về project media folder
9. Cập nhật row qua callbacks

### Method 2: `GenerateBatchAsync` (xử lý nhiều rows)

Flow khi user chọn N hàng → click "Generate AI Video (Grok)":

```
User chọn N hàng (checkbox) → Click "Generate AI Video (Grok)"
    │
    ├─ 1. Kiểm tra Grok API key (1 lần duy nhất)
    │
    ├─ 2. Validate tất cả hàng đã chọn:
    │      - Hàng nào thiếu ảnh → skip, đánh dấu lỗi "No image"
    │      - Hàng nào thiếu text → skip, đánh dấu lỗi "No text"
    │      - Còn lại = danh sách hàng hợp lệ
    │
    ├─ 3. Tạo 1 GrokClient duy nhất (share cho tất cả job)
    │
    ├─ 4. Chạy parallel với SemaphoreSlim(2) — giới hạn 2 concurrent
    │      ┌─ Row 1: Submit → Poll → Download → Cập nhật MediaPath
    │      ├─ Row 2: Submit → Poll → Download → Cập nhật MediaPath
    │      ├─ Row 3: (chờ semaphore) → Submit → Poll → ...
    │      └─ Row N: ...
    │
    ├─ 5. Progress:
    │      - lblstatus: "Grok: 3/10 completed"
    │      - Mỗi row: Column_renderstatus riêng (Submitting/Processing 45%/Done/Failed)
    │
    └─ 6. Khi tất cả xong → tổng kết: "Grok: 8 done, 2 failed"
```

**Giới hạn concurrent = 2** vì Grok API rate limit chặt hơn TTS.

**Cancellation**: 1 `CancellationTokenSource` cho cả batch — cancel dừng tất cả job đang chờ + đang poll.

**Có thể dùng `GenerateAndWaitAsync` với `onProgress` callback** (SDK đã hỗ trợ), hoặc poll thủ công để kiểm soát chi tiết hơn. Khuyến nghị: dùng poll thủ công trong service để có thể cập nhật UI từng bước.

**Files**: `ReviewMovie/Services/GrokVideoService.cs` (mới)

---

## Phase 4: Localization

Thêm LangKeys cho Grok:

| Key | Vi | En |
|-----|----|----|
| `Grok_GenerateVideo` | Tạo Video AI (Grok) | Generate AI Video (Grok) |
| `Grok_EnableFeature` | Bật Grok AI Video | Enable Grok AI Video |
| `Grok_Submitting` | Đang gửi yêu cầu... | Submitting to Grok... |
| `Grok_Processing` | Grok: Đang xử lý... ({0}%) | Grok: Processing... ({0}%) |
| `Grok_Downloading` | Grok: Đang tải video... | Grok: Downloading video... |
| `Grok_Complete` | Grok: Hoàn thành | Grok: Video generated |
| `Grok_BatchProgress` | Grok: {0}/{1} hoàn thành | Grok: {0}/{1} completed |
| `Grok_BatchDone` | Grok: {0} xong, {1} lỗi | Grok: {0} done, {1} failed |
| `Grok_Failed` | Grok: Tạo video thất bại | Grok: Generation failed |
| `Grok_Expired` | Grok: Job hết hạn | Grok: Job expired |
| `Grok_AuthError` | Grok: API key không hợp lệ | Grok: Invalid API key |
| `Grok_RateLimit` | Grok: Giới hạn, thử lại sau {0}s | Grok: Rate limit, retry after {0}s |
| `Grok_NetworkError` | Grok: Lỗi mạng | Grok: Network error |
| `Grok_NoImage` | Chưa chọn ảnh cho hàng này | No image selected for this row |
| `Grok_NoText` | Chưa nhập lời thoại | No dialogue text for this row |
| `Grok_EnterApiKey` | Nhập API key Grok (xAI) | Enter your Grok (xAI) API key |
| `Grok_Timeout` | Grok: Quá thời gian chờ | Grok: Generation timed out |

**Files**:
- `ReviewMovie/Localization/LangKeys.cs` (sửa)
- `ReviewMovie/Localization/Resources/Lang_vi.cs` (sửa)
- `ReviewMovie/Localization/Resources/Lang_en.cs` (sửa)

---

## Phase 5: UI Integration

### 5a. Panel Settings — GroupBox "Grok AI Video"

Thêm `grbConfigGrok` vào **panel settings bên PHẢI** (`scSetting`), bên dưới `grbConfigRender`:

```
┌─ Grok AI Video ──────────────────────┐
│ [✓] Bật Grok AI Video                │
│                                       │
│ API Key: [••••••••••••••••] [Save]    │
│ Duration:  [5s ▾]                     │
│ Tỷ lệ:    [16:9 ▾]                   │
│ Prompt:    [Default template ▾]       │
└───────────────────────────────────────┘
```

- **CheckBox `chkEnableGrok`** — toggle bật/tắt toàn bộ tính năng
- **UiTextBox `txtGrokApiKey`** (password char) + **UiButton `btnSaveGrokKey`**
- **ComboBox `cbGrokDuration`** — lựa chọn: 5s (default)
- **ComboBox `cbGrokAspectRatio`** — 16:9, 9:16, 1:1, 4:3, 3:4
- **ComboBox `cbGrokPromptTemplate`** — Default / Custom (edit prompt template)
- Khi tắt (`chkEnableGrok.Checked = false`):
  - Disable tất cả controls bên trong
  - Ẩn context menu items Grok trên DataGridView
- Khi bật: hiện đầy đủ
- Trạng thái lưu vào `ConfigModel` (GrokEnabled, GrokApiKey, GrokDuration, GrokAspectRatio), load khi mở app

### 5b. Context Menu trên DataGridView

Thêm vào `ctMenu` (theo pattern "Convert All/Selected" hiện có):

- `MenuItemGrokVideoAll` — "Tạo Video AI (Grok) - Chọn Hết"
- `MenuItemGrokVideoSelected` — "Tạo Video AI (Grok) - Chọn Nhóm"

Theo đúng naming convention hiện có ("Chọn Hết" / "Chọn Nhóm"). Chỉ hiện khi `chkEnableGrok.Checked = true`.

### 5c. Click Handler Logic

1. Kiểm tra `GrokEnabled` + API key không rỗng
2. Thu thập danh sách rows (all hoặc checked)
3. Mỗi row: lấy ảnh từ `MediaPath`/`MediaFilePath`, lời thoại từ `Column_inputtext`
4. Gọi `GrokVideoService.GenerateBatchAsync`
5. Khi từng row xong: cập nhật `InfoRenderVd.MediaPath` → video trở thành media của row
6. Cập nhật grid cells + project DB

### 5d. Progress

- `lblstatus`: progress tổng batch "Grok: 3/10 completed"
- `Column_renderstatus` mỗi row: trạng thái riêng

### 5e. Cancellation

- Thêm `_grokCTS` field (CancellationTokenSource)
- Đăng ký vào `CheckAndCancelAllRunningTasksAsync`

### 5f. Error Handling

Map `GrokErrorType` → localized messages:

```csharp
switch (gex.ErrorType)
{
    case GrokErrorType.Auth:      → LangKeys.Grok_AuthError
    case GrokErrorType.RateLimit: → LangKeys.Grok_RateLimit
    case GrokErrorType.JobFailed: → LangKeys.Grok_Failed
    case GrokErrorType.JobExpired:→ LangKeys.Grok_Expired
    case GrokErrorType.Timeout:   → LangKeys.Grok_Timeout
    case GrokErrorType.Network:   → LangKeys.Grok_NetworkError
}
```

**Files**:
- `ReviewMovie/FormMain.Designer.cs` (sửa)
- `ReviewMovie/FormMain.cs` (sửa)

---

## Phase 6: Output Integration

Video gen xong → gán vào `InfoRenderVd.MediaPath` → trở thành media input cho pipeline render thông thường:

```
[Grok AI Video] → MediaPath của row
                      ↓
[TTS lời thoại] → Audio file
                      ↓
[FFmpeg Render] → Combine video + audio + effects
                      ↓
[Merge segments] → Final MP4
```

---

## Tóm tắt files thay đổi

| Action | File | Mô tả |
|--------|------|-------|
| **Mới** | `ReviewMovie/Services/GrokVideoService.cs` | Service xử lý single + batch generation |
| **Mới** | `ReviewMovie/Model/GrokVideoContextModel.cs` | Context model với callbacks |
| Sửa | `ReviewMovie/Infrastructure/Config/ConfigModel.cs` | Thêm `GrokApiKey`, `GrokEnabled` |
| Sửa | `ReviewMovie/Infrastructure/Config/ConfigDataService.cs` | Save/load Grok config |
| Sửa | `ReviewMovie/Localization/LangKeys.cs` | Thêm 17 Grok lang keys |
| Sửa | `ReviewMovie/Localization/Resources/Lang_vi.cs` | Bản dịch tiếng Việt |
| Sửa | `ReviewMovie/Localization/Resources/Lang_en.cs` | Bản dịch tiếng Anh |
| Sửa | `ReviewMovie/FormMain.Designer.cs` | GroupBox + controls + context menu |
| Sửa | `ReviewMovie/FormMain.cs` | Handlers, CTS, logic tích hợp |
| Sửa | `ReviewMovie/ReviewMovie.csproj` | Reference GrokVideoSdk.dll |

---

## Lưu ý kỹ thuật

- **Dispose**: `GrokClient` implement `IDisposable`. Tạo instance khi bắt đầu batch, dispose khi batch xong (dùng `using` hoặc try/finally). Không giữ instance lâu dài.
- **Image size**: Ảnh lớn > 5MB khi convert base64 sẽ tốn ~7MB RAM + payload lớn. Nên resize ảnh xuống max 1920px trước khi convert. Dùng `Funcion.GetVideoSize()` có sẵn trong EasyClip để đọc kích thước, và FFmpeg để resize nếu cần.
- **Thread safety**: Callbacks từ service chạy trên background thread → phải `Invoke()` khi cập nhật UI (dgvMainView, lblstatus). Dùng `UIThreadHelper` pattern có sẵn.
- **Auto-retry**: SDK đã tự retry 3 lần khi rate limit 429 (chờ theo `Retry-After` header). Service không cần xử lý retry riêng.
- **Logging**: Implement `IGrokLogger` để ghi log ra file/debug output, giúp debug khi gặp vấn đề với API.

---

## Verification Checklist

- [ ] Build solution — không lỗi compile
- [ ] Mở app → panel trái có GroupBox "Grok AI Video" với checkbox + API key
- [ ] Tắt checkbox → context menu Grok ẩn, bật → hiện
- [ ] Right-click row → thấy "Generate AI Video (Grok) - All/Selected"
- [ ] Test single: 1 row có ảnh + text → gen video → MediaPath cập nhật
- [ ] Test batch: chọn 5 rows → gen parallel → progress "3/5 completed"
- [ ] Test cancel giữa chừng → các job dừng lại
- [ ] Test lỗi: API key sai → "Invalid API key", network fail → "Network error"
- [ ] Test full pipeline: Grok video → TTS audio → FFmpeg render → merge → final MP4
