use grok_video_sdk::GrokClient;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use tokio::sync::RwLock;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct JobInfo {
    pub id: String,
    pub request_id: Option<String>,
    pub prompt: String,
    pub status: String,
    pub progress: u8,
    pub video_path: Option<String>,
    pub error: Option<String>,
}

impl JobInfo {
    pub fn new(id: String, prompt: String) -> Self {
        Self {
            id,
            request_id: None,
            prompt,
            status: "pending".to_string(),
            progress: 0,
            video_path: None,
            error: None,
        }
    }
}

pub struct AppState {
    pub client: RwLock<Option<GrokClient>>,
    pub jobs: RwLock<HashMap<String, JobInfo>>,
    pub output_dir: RwLock<String>,
}

impl AppState {
    pub fn new() -> Self {
        let output_dir = dirs::video_dir()
            .or_else(|| dirs::download_dir())
            .unwrap_or_else(|| std::path::PathBuf::from("."))
            .join("GrokVideos")
            .to_string_lossy()
            .to_string();

        Self {
            client: RwLock::new(None),
            jobs: RwLock::new(HashMap::new()),
            output_dir: RwLock::new(output_dir),
        }
    }
}
