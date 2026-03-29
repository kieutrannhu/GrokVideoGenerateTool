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
