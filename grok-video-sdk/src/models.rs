use serde::{Deserialize, Serialize};

// ── Request types ──

#[derive(Debug, Clone, Serialize)]
pub struct VideoGenerationRequest {
    pub model: String,
    pub prompt: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub duration: Option<u8>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub aspect_ratio: Option<AspectRatio>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub resolution: Option<Resolution>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub image: Option<ImageInput>,
}

impl VideoGenerationRequest {
    pub fn new(prompt: impl Into<String>) -> Self {
        Self {
            model: "grok-imagine-video".to_string(),
            prompt: prompt.into(),
            duration: None,
            aspect_ratio: None,
            resolution: None,
            image: None,
        }
    }

    pub fn duration(mut self, seconds: u8) -> Self {
        self.duration = Some(seconds);
        self
    }

    pub fn aspect_ratio(mut self, ratio: AspectRatio) -> Self {
        self.aspect_ratio = Some(ratio);
        self
    }

    pub fn resolution(mut self, res: Resolution) -> Self {
        self.resolution = Some(res);
        self
    }

    pub fn image_url(mut self, url: impl Into<String>) -> Self {
        self.image = Some(ImageInput { url: url.into() });
        self
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum AspectRatio {
    #[serde(rename = "16:9")]
    W16H9,
    #[serde(rename = "9:16")]
    W9H16,
    #[serde(rename = "1:1")]
    Square,
    #[serde(rename = "4:3")]
    W4H3,
    #[serde(rename = "3:4")]
    W3H4,
    #[serde(rename = "3:2")]
    W3H2,
    #[serde(rename = "2:3")]
    W2H3,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum Resolution {
    #[serde(rename = "480p")]
    P480,
    #[serde(rename = "720p")]
    P720,
}

#[derive(Debug, Clone, Serialize)]
pub struct ImageInput {
    pub url: String,
}

// ── Response types ──

#[derive(Debug, Clone, Deserialize)]
pub struct SubmitResponse {
    pub request_id: String,
}

#[derive(Debug, Clone, Deserialize)]
pub struct StatusResponse {
    pub status: JobStatus,
    #[serde(default)]
    pub video: Option<VideoData>,
    #[serde(default)]
    pub model: Option<String>,
    #[serde(default)]
    pub progress: Option<u8>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum JobStatus {
    Pending,
    Processing,
    Done,
    Failed,
    Expired,
}

#[derive(Debug, Clone, Deserialize)]
pub struct VideoData {
    pub url: String,
    #[serde(default)]
    pub duration: Option<f32>,
}

// ── Image generation types ──

#[derive(Debug, Clone, Serialize)]
pub struct ImageGenerationRequest {
    pub model: String,
    pub prompt: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub n: Option<u8>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub response_format: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub aspect_ratio: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub resolution: Option<String>,
}

impl ImageGenerationRequest {
    pub fn new(prompt: impl Into<String>) -> Self {
        Self {
            model: "grok-imagine-image-quality".to_string(),
            prompt: prompt.into(),
            n: None,
            response_format: Some("url".to_string()),
            aspect_ratio: None,
            resolution: None,
        }
    }

    pub fn count(mut self, n: u8) -> Self {
        self.n = Some(n);
        self
    }

    pub fn aspect_ratio(mut self, ratio: impl Into<String>) -> Self {
        self.aspect_ratio = Some(ratio.into());
        self
    }

    pub fn resolution(mut self, res: impl Into<String>) -> Self {
        self.resolution = Some(res.into());
        self
    }
}

/// Request for image editing (image-to-image via /v1/images/edits).
/// Sent as multipart/form-data.
pub struct ImageEditRequest {
    pub prompt: String,
    pub image_data: Vec<u8>,
    pub image_filename: String,
    pub n: Option<u8>,
    pub aspect_ratio: Option<String>,
    pub resolution: Option<String>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct ImageGenerationResponse {
    pub data: Vec<GeneratedImage>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct GeneratedImage {
    #[serde(default)]
    pub url: Option<String>,
    #[serde(default)]
    pub b64_json: Option<String>,
}

/// API error body returned by xAI
#[derive(Debug, Deserialize)]
pub(crate) struct ApiErrorBody {
    #[serde(default)]
    pub error: Option<ApiErrorDetail>,
}

#[derive(Debug, Deserialize)]
pub(crate) struct ApiErrorDetail {
    #[serde(default)]
    pub message: Option<String>,
}
