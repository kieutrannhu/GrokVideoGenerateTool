use std::path::Path;
use std::time::Duration;

use base64::Engine;
use futures_util::StreamExt;
use reqwest::header::{HeaderMap, HeaderValue, AUTHORIZATION};
use serde::Serialize;
use tokio::io::AsyncWriteExt;

use crate::error::{GrokError, Result};
use crate::models::*;

const DEFAULT_BASE_URL: &str = "https://api.x.ai";

/// Client for the xAI Grok Video Generation API.
#[derive(Clone)]
pub struct GrokClient {
    http: reqwest::Client,
    base_url: String,
}

impl GrokClient {
    /// Create a new client with the given API key.
    pub fn new(api_key: &str) -> Result<Self> {
        Self::with_base_url(api_key, DEFAULT_BASE_URL)
    }

    /// Create a new client with a custom base URL (useful for testing).
    pub fn with_base_url(api_key: &str, base_url: &str) -> Result<Self> {
        let mut headers = HeaderMap::new();

        let auth_value = format!("Bearer {api_key}");
        let mut auth_header =
            HeaderValue::from_str(&auth_value).map_err(|_| GrokError::Auth)?;
        auth_header.set_sensitive(true);
        headers.insert(AUTHORIZATION, auth_header);

        let http = reqwest::Client::builder()
            .default_headers(headers)
            .timeout(Duration::from_secs(30))
            .build()?;

        Ok(Self {
            http,
            base_url: base_url.trim_end_matches('/').to_string(),
        })
    }

    /// Submit a video generation job. Returns the request ID.
    pub async fn submit_job(&self, request: &VideoGenerationRequest) -> Result<SubmitResponse> {
        let url = format!("{}/v1/videos/generations", self.base_url);
        let response = self.http.post(&url).json(request).send().await?;
        let body = self.parse_response(response).await?;

        serde_json::from_str::<SubmitResponse>(&body)
            .map_err(|e| GrokError::InvalidResponse(format!("{e}: {body}")))
    }

    /// Check the status of a video generation job.
    pub async fn check_status(&self, request_id: &str) -> Result<StatusResponse> {
        let url = format!("{}/v1/videos/{}", self.base_url, request_id);
        let response = self.http.get(&url).send().await?;
        let body = self.parse_response(response).await?;

        let status_resp = serde_json::from_str::<StatusResponse>(&body)
            .map_err(|e| GrokError::InvalidResponse(format!("{e}: {body}")))?;

        match status_resp.status {
            JobStatus::Failed => Err(GrokError::JobFailed {
                request_id: request_id.to_string(),
            }),
            JobStatus::Expired => Err(GrokError::JobExpired {
                request_id: request_id.to_string(),
            }),
            _ => Ok(status_resp),
        }
    }

    /// Download a completed video to the specified path.
    /// Uses streaming to avoid holding the entire file in memory.
    pub async fn download_video(&self, video_url: &str, dest: &Path) -> Result<()> {
        let response = reqwest::get(video_url).await?;

        if !response.status().is_success() {
            return Err(GrokError::Api {
                status: response.status().as_u16(),
                message: "Failed to download video".to_string(),
            });
        }

        let mut file = tokio::fs::File::create(dest).await?;
        let mut stream = response.bytes_stream();

        while let Some(chunk) = stream.next().await {
            let bytes = chunk?;
            file.write_all(&bytes).await?;
        }

        file.flush().await?;
        Ok(())
    }

    /// Generate an image synchronously (text-to-image). Returns the API response with image URLs.
    pub async fn generate_image(
        &self,
        request: &ImageGenerationRequest,
    ) -> Result<ImageGenerationResponse> {
        let url = format!("{}/v1/images/generations", self.base_url);
        let response = self.http.post(&url).json(request).send().await?;
        let body = self.parse_response(response).await?;

        serde_json::from_str::<ImageGenerationResponse>(&body)
            .map_err(|e| GrokError::InvalidResponse(format!("{e}: {body}")))
    }

    /// Edit an image using a reference image (image-to-image via /v1/images/edits).
    /// Sends as JSON with image encoded as base64 data URI.
    pub async fn edit_image(
        &self,
        request: ImageEditRequest,
    ) -> Result<ImageGenerationResponse> {
        let url = format!("{}/v1/images/edits", self.base_url);

        let mime = if request.image_filename.ends_with(".jpg")
            || request.image_filename.ends_with(".jpeg")
        {
            "image/jpeg"
        } else if request.image_filename.ends_with(".webp") {
            "image/webp"
        } else {
            "image/png"
        };

        let b64 = base64::engine::general_purpose::STANDARD.encode(&request.image_data);
        let data_uri = format!("data:{mime};base64,{b64}");

        #[derive(Serialize)]
        struct EditBody {
            model: &'static str,
            prompt: String,
            image: ImageInput,
            #[serde(skip_serializing_if = "Option::is_none")]
            n: Option<u8>,
            #[serde(skip_serializing_if = "Option::is_none")]
            aspect_ratio: Option<String>,
            #[serde(skip_serializing_if = "Option::is_none")]
            resolution: Option<String>,
        }

        let body = EditBody {
            model: "grok-imagine-image-quality",
            prompt: request.prompt,
            image: ImageInput { url: data_uri },
            n: request.n,
            aspect_ratio: request.aspect_ratio,
            resolution: request.resolution,
        };

        let response = self.http.post(&url).json(&body).send().await?;
        let body_str = self.parse_response(response).await?;

        serde_json::from_str::<ImageGenerationResponse>(&body_str)
            .map_err(|e| GrokError::InvalidResponse(format!("{e}: {body_str}")))
    }

    /// Download an image from a URL to the specified path.
    pub async fn download_image(&self, image_url: &str, dest: &Path) -> Result<()> {
        let response = reqwest::get(image_url).await?;

        if !response.status().is_success() {
            return Err(GrokError::Api {
                status: response.status().as_u16(),
                message: "Failed to download image".to_string(),
            });
        }

        let mut file = tokio::fs::File::create(dest).await?;
        let mut stream = response.bytes_stream();

        while let Some(chunk) = stream.next().await {
            let bytes = chunk?;
            file.write_all(&bytes).await?;
        }

        file.flush().await?;
        Ok(())
    }

    /// Convenience: submit a job, poll until completion, and return the final status.
    pub async fn generate_and_wait(
        &self,
        request: &VideoGenerationRequest,
        poll_interval: Duration,
        timeout: Duration,
    ) -> Result<StatusResponse> {
        let submit_resp = self.submit_job(request).await?;
        let request_id = &submit_resp.request_id;

        let start = tokio::time::Instant::now();

        loop {
            if start.elapsed() > timeout {
                return Err(GrokError::Timeout(timeout.as_secs()));
            }

            tokio::time::sleep(poll_interval).await;

            let status = self.check_status(request_id).await?;

            if status.status == JobStatus::Done {
                return Ok(status);
            }
        }
    }

    /// Consume a response: on success return body text, on error map to GrokError.
    async fn parse_response(&self, response: reqwest::Response) -> Result<String> {
        let status = response.status();

        if status.is_success() {
            return Ok(response.text().await?);
        }

        if status.as_u16() == 401 || status.as_u16() == 403 {
            return Err(GrokError::Auth);
        }

        if status.as_u16() == 429 {
            let retry_after = response
                .headers()
                .get("retry-after")
                .and_then(|v| v.to_str().ok())
                .and_then(|v| v.parse::<u64>().ok())
                .unwrap_or(60);
            return Err(GrokError::RateLimit {
                retry_after_secs: retry_after,
            });
        }

        let status_code = status.as_u16();
        let body = response.text().await.unwrap_or_default();
        let message = serde_json::from_str::<ApiErrorBody>(&body)
            .ok()
            .and_then(|b| b.error)
            .and_then(|e| e.message)
            .unwrap_or_else(|| {
                if body.is_empty() {
                    format!("API returned status {status_code}")
                } else {
                    format!("API returned status {status_code}: {body}")
                }
            });

        Err(GrokError::Api {
            status: status_code,
            message,
        })
    }
}
