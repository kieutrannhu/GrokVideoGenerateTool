mod commands;
mod state;

use std::sync::Arc;

use commands::{connect_api, create_video_task, get_jobs, get_output_dir};
use state::AppState;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let app_state = Arc::new(AppState::new());

    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_fs::init())
        .plugin(tauri_plugin_notification::init())
        .manage(app_state)
        .invoke_handler(tauri::generate_handler![
            connect_api,
            create_video_task,
            get_jobs,
            get_output_dir,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
