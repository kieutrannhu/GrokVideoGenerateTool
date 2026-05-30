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
    pub task_type: String, // "video" or "image"
    pub video_path: Option<String>,
    pub image_path: Option<String>,
    pub error: Option<String>,
}

impl JobInfo {
    pub fn new(id: String, prompt: String, task_type: &str) -> Self {
        Self {
            id,
            request_id: None,
            prompt,
            status: "pending".to_string(),
            progress: 0,
            task_type: task_type.to_string(),
            video_path: None,
            image_path: None,
            error: None,
        }
    }
}

pub struct AppState {
    pub client: RwLock<Option<GrokClient>>,
    pub jobs: RwLock<HashMap<String, JobInfo>>,
    pub output_dir: RwLock<String>,
    pub image_output_dir: RwLock<String>,
}

impl AppState {
    pub fn new() -> Self {
        let output_dir = dirs::video_dir()
            .or_else(|| dirs::download_dir())
            .unwrap_or_else(|| std::path::PathBuf::from("."))
            .join("GrokVideos")
            .to_string_lossy()
            .to_string();

        let image_output_dir = dirs::picture_dir()
            .or_else(|| dirs::download_dir())
            .unwrap_or_else(|| std::path::PathBuf::from("."))
            .join("GrokImages")
            .to_string_lossy()
            .to_string();

        Self {
            client: RwLock::new(None),
            jobs: RwLock::new(HashMap::new()),
            output_dir: RwLock::new(output_dir),
            image_output_dir: RwLock::new(image_output_dir),
        }
    }
}
