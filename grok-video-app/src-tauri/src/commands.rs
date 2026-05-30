use std::path::PathBuf;
use std::sync::Arc;
use std::time::Duration;

use grok_video_sdk::{
    AspectRatio, GrokClient, ImageEditRequest, ImageGenerationRequest, JobStatus, Resolution,
    VideoGenerationRequest,
};
use serde::Deserialize;
use base64::Engine;
use tauri::{AppHandle, Emitter, State};
use uuid::Uuid;

use crate::state::{AppState, JobInfo};

#[derive(Debug, Deserialize)]
pub struct ConnectPayload {
    pub api_key: String,
}

#[derive(Debug, Deserialize)]
pub struct GeneratePayload {
    pub prompt: String,
    pub duration: Option<u8>,
    pub aspect_ratio: Option<String>,
    pub resolution: Option<String>,
    pub image_path: Option<String>,
}

#[derive(Debug, Deserialize)]
pub struct ImagePayload {
    pub prompt: String,
    pub count: Option<u8>,
    pub aspect_ratio: Option<String>,
    pub resolution: Option<String>,
    pub image_path: Option<String>,
}

// ── Commands ──

#[tauri::command]
pub async fn connect_api(
    payload: ConnectPayload,
    state: State<'_, Arc<AppState>>,
) -> Result<String, String> {
    let client = GrokClient::new(&payload.api_key).map_err(|e| e.to_string())?;
    *state.client.write().await = Some(client);
    Ok("Connected".to_string())
}

#[tauri::command]
pub async fn create_video_task(
    app: AppHandle,
    payload: GeneratePayload,
    state: State<'_, Arc<AppState>>,
) -> Result<JobInfo, String> {
    let client: GrokClient = {
        let guard = state.client.read().await;
        guard.clone().ok_or("API key not configured".to_string())?
    };

    let job_id = Uuid::new_v4().to_string();
    let job = JobInfo::new(job_id.clone(), payload.prompt.clone(), "video");

    // Save job immediately so it shows in queue right away
    state.jobs.write().await.insert(job_id.clone(), job.clone());

    // Build request in background
    let state_inner = state.inner().clone();
    let app_handle = app.clone();

    tokio::spawn(async move {
        // Build the video generation request
        let mut request = VideoGenerationRequest::new(&payload.prompt);

        if let Some(d) = payload.duration {
            request = request.duration(d);
        }

        if let Some(ref ar) = payload.aspect_ratio {
            request.aspect_ratio = parse_aspect_ratio(ar);
        }

        if let Some(ref res) = payload.resolution {
            request.resolution = parse_resolution(res);
        }

        if let Some(ref img_path) = payload.image_path {
            match tokio::fs::read(img_path).await {
                Ok(data) => {
                    let ext = PathBuf::from(img_path)
                        .extension()
                        .and_then(|e| e.to_str())
                        .unwrap_or("png")
                        .to_string();
                    let mime = match ext.as_str() {
                        "jpg" | "jpeg" => "image/jpeg",
                        "png" => "image/png",
                        "webp" => "image/webp",
                        _ => "image/png",
                    };
                    let b64 = base64::engine::general_purpose::STANDARD.encode(&data);
                    request = request.image_url(format!("data:{mime};base64,{b64}"));
                }
                Err(e) => {
                    let mut jobs = state_inner.jobs.write().await;
                    if let Some(job) = jobs.get_mut(&job_id) {
                        job.status = "failed".to_string();
                        job.error = Some(format!("Failed to read image: {e}"));
                        let _ = app_handle.emit("job-update", &*job);
                    }
                    return;
                }
            }
        }

        // Submit to API
        match client.submit_job(&request).await {
            Ok(submit_resp) => {
                {
                    let mut jobs = state_inner.jobs.write().await;
                    if let Some(job) = jobs.get_mut(&job_id) {
                        job.request_id = Some(submit_resp.request_id.clone());
                        job.status = "processing".to_string();
                        let _ = app_handle.emit("job-update", &*job);
                    }
                }
                // Start polling
                poll_job(
                    app_handle,
                    state_inner,
                    client,
                    job_id,
                    submit_resp.request_id,
                )
                .await;
            }
            Err(e) => {
                let mut jobs = state_inner.jobs.write().await;
                if let Some(job) = jobs.get_mut(&job_id) {
                    job.status = "failed".to_string();
                    job.error = Some(format!("Submit failed: {e}"));
                    let _ = app_handle.emit("job-update", &*job);
                }
            }
        }
    });

    Ok(job)
}

#[tauri::command]
pub async fn create_image_task(
    app: AppHandle,
    payload: ImagePayload,
    state: State<'_, Arc<AppState>>,
) -> Result<JobInfo, String> {
    let client: GrokClient = {
        let guard = state.client.read().await;
        guard.clone().ok_or("API key not configured".to_string())?
    };

    let job_id = Uuid::new_v4().to_string();
    let job = JobInfo::new(job_id.clone(), payload.prompt.clone(), "image");

    state.jobs.write().await.insert(job_id.clone(), job.clone());

    let state_inner = state.inner().clone();
    let app_handle = app.clone();

    tokio::spawn(async move {
        // Choose endpoint: edits (img2img) when reference image provided, else generations
        let api_result = if let Some(ref img_path) = payload.image_path {
            match tokio::fs::read(img_path).await {
                Ok(data) => {
                    let filename = PathBuf::from(img_path)
                        .file_name()
                        .and_then(|n| n.to_str())
                        .unwrap_or("image.png")
                        .to_string();
                    let edit_req = ImageEditRequest {
                        prompt: payload.prompt.clone(),
                        image_data: data,
                        image_filename: filename,
                        n: payload.count,
                        aspect_ratio: payload.aspect_ratio.clone(),
                        resolution: payload.resolution.clone(),
                    };
                    client.edit_image(edit_req).await
                }
                Err(e) => {
                    let mut jobs = state_inner.jobs.write().await;
                    if let Some(job) = jobs.get_mut(&job_id) {
                        job.status = "failed".to_string();
                        job.error = Some(format!("Failed to read image: {e}"));
                        let _ = app_handle.emit("job-update", &*job);
                    }
                    return;
                }
            }
        } else {
            let mut request = ImageGenerationRequest::new(&payload.prompt);
            if let Some(n) = payload.count {
                request = request.count(n);
            }
            if let Some(ref ar) = payload.aspect_ratio {
                request = request.aspect_ratio(ar);
            }
            if let Some(ref res) = payload.resolution {
                request = request.resolution(res);
            }
            client.generate_image(&request).await
        };

        match api_result {
            Ok(resp) => {
                // Update status to downloading
                {
                    let mut jobs = state_inner.jobs.write().await;
                    if let Some(job) = jobs.get_mut(&job_id) {
                        job.status = "downloading".to_string();
                        job.progress = 50;
                        let _ = app_handle.emit("job-update", &*job);
                    }
                }

                let image_output_dir = state_inner.image_output_dir.read().await.clone();
                let dir = PathBuf::from(&image_output_dir);
                let _ = tokio::fs::create_dir_all(&dir).await;

                // Download first image (or all if count > 1)
                let first_url = resp.data.into_iter().find_map(|img| img.url);
                match first_url {
                    Some(url) => {
                        let filename = format!("{}.png", &job_id[..8]);
                        let dest = dir.join(&filename);
                        match client.download_image(&url, &dest).await {
                            Ok(()) => {
                                let mut jobs = state_inner.jobs.write().await;
                                if let Some(job) = jobs.get_mut(&job_id) {
                                    job.status = "done".to_string();
                                    job.progress = 100;
                                    job.image_path = Some(dest.to_string_lossy().to_string());
                                    let _ = app_handle.emit("job-update", &*job);
                                }
                            }
                            Err(e) => {
                                let mut jobs = state_inner.jobs.write().await;
                                if let Some(job) = jobs.get_mut(&job_id) {
                                    job.status = "failed".to_string();
                                    job.error = Some(format!("Download failed: {e}"));
                                    let _ = app_handle.emit("job-update", &*job);
                                }
                            }
                        }
                    }
                    None => {
                        let mut jobs = state_inner.jobs.write().await;
                        if let Some(job) = jobs.get_mut(&job_id) {
                            job.status = "failed".to_string();
                            job.error = Some("No image URL in response".to_string());
                            let _ = app_handle.emit("job-update", &*job);
                        }
                    }
                }
            }
            Err(e) => {
                let mut jobs = state_inner.jobs.write().await;
                if let Some(job) = jobs.get_mut(&job_id) {
                    job.status = "failed".to_string();
                    job.error = Some(format!("Generation failed: {e}"));
                    let _ = app_handle.emit("job-update", &*job);
                }
            }
        }
    });

    Ok(job)
}

#[tauri::command]
pub async fn get_jobs(state: State<'_, Arc<AppState>>) -> Result<Vec<JobInfo>, String> {
    let jobs = state.jobs.read().await;
    Ok(jobs.values().cloned().collect())
}

#[tauri::command]
pub async fn get_output_dir(state: State<'_, Arc<AppState>>) -> Result<String, String> {
    let dir = state.output_dir.read().await.clone();
    // Ensure the directory exists before returning
    tokio::fs::create_dir_all(&dir)
        .await
        .map_err(|e| e.to_string())?;
    Ok(dir)
}

// ── Polling ──

async fn poll_job(
    app: AppHandle,
    state: Arc<AppState>,
    client: GrokClient,
    job_id: String,
    request_id: String,
) {
    let mut retry_count = 0u32;
    let max_retries = 3u32;

    loop {
        tokio::time::sleep(Duration::from_secs(4)).await;

        match client.check_status(&request_id).await {
            Ok(status_resp) => {
                retry_count = 0;
                let mut jobs = state.jobs.write().await;
                if let Some(job) = jobs.get_mut(&job_id) {
                    job.progress = status_resp.progress.unwrap_or(job.progress);

                    match status_resp.status {
                        JobStatus::Done => {
                            job.status = "downloading".to_string();
                            job.progress = 95;
                            let _ = app.emit("job-update", &*job);
                            drop(jobs);

                            // Download video
                            if let Some(video) = status_resp.video {
                                let output_dir = state.output_dir.read().await.clone();
                                let dir = PathBuf::from(&output_dir);
                                let _ = tokio::fs::create_dir_all(&dir).await;

                                let filename = format!("{}.mp4", &job_id[..8]);
                                let dest = dir.join(&filename);

                                match client.download_video(&video.url, &dest).await {
                                    Ok(()) => {
                                        let mut jobs = state.jobs.write().await;
                                        if let Some(job) = jobs.get_mut(&job_id) {
                                            job.status = "done".to_string();
                                            job.progress = 100;
                                            job.video_path =
                                                Some(dest.to_string_lossy().to_string());
                                            let _ = app.emit("job-update", &*job);
                                        }
                                    }
                                    Err(e) => {
                                        let mut jobs = state.jobs.write().await;
                                        if let Some(job) = jobs.get_mut(&job_id) {
                                            job.status = "failed".to_string();
                                            job.error = Some(format!("Download failed: {e}"));
                                            let _ = app.emit("job-update", &*job);
                                        }
                                    }
                                }
                            }
                            return;
                        }
                        JobStatus::Pending | JobStatus::Processing => {
                            job.status = "processing".to_string();
                            let _ = app.emit("job-update", &*job);
                        }
                        _ => {}
                    }
                }
            }
            Err(e) => {
                retry_count += 1;
                if retry_count >= max_retries {
                    let mut jobs = state.jobs.write().await;
                    if let Some(job) = jobs.get_mut(&job_id) {
                        job.status = "failed".to_string();
                        job.error = Some(format!("Polling failed after retries: {e}"));
                        let _ = app.emit("job-update", &*job);
                    }
                    return;
                }
                // Wait longer before retry
                tokio::time::sleep(Duration::from_secs(5)).await;
            }
        }
    }
}

// ── Helpers ──

fn parse_aspect_ratio(s: &str) -> Option<AspectRatio> {
    match s {
        "16:9" => Some(AspectRatio::W16H9),
        "9:16" => Some(AspectRatio::W9H16),
        "1:1" => Some(AspectRatio::Square),
        "4:3" => Some(AspectRatio::W4H3),
        "3:4" => Some(AspectRatio::W3H4),
        _ => None,
    }
}

fn parse_resolution(s: &str) -> Option<Resolution> {
    match s {
        "480p" => Some(Resolution::P480),
        "720p" => Some(Resolution::P720),
        _ => None,
    }
}
