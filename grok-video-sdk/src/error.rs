use thiserror::Error;

pub type Result<T> = std::result::Result<T, GrokError>;

#[derive(Debug, Error)]
pub enum GrokError {
    #[error("HTTP request failed: {0}")]
    Http(#[from] reqwest::Error),

    #[error("API error (status {status}): {message}")]
    Api { status: u16, message: String },

    #[error("Authentication failed: invalid or missing API key")]
    Auth,

    #[error("Rate limit exceeded, retry after {retry_after_secs}s")]
    RateLimit { retry_after_secs: u64 },

    #[error("Job failed: {request_id}")]
    JobFailed { request_id: String },

    #[error("Job expired: {request_id}")]
    JobExpired { request_id: String },

    #[error("Invalid response: {0}")]
    InvalidResponse(String),

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("Timeout: polling exceeded {0}s")]
    Timeout(u64),
}
