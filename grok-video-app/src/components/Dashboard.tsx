import { useEffect, useState } from "react";
import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import { open } from "@tauri-apps/plugin-dialog";
import { openPath } from "@tauri-apps/plugin-opener";
import type { JobInfo, GeneratePayload } from "../types";
import JobCard from "./JobCard";

export default function Dashboard() {
  const [prompt, setPrompt] = useState("");
  const [duration, setDuration] = useState(5);
  const [aspectRatio, setAspectRatio] = useState("16:9");
  const [resolution, setResolution] = useState("720p");
  const [imagePath, setImagePath] = useState<string | null>(null);
  const [jobs, setJobs] = useState<JobInfo[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  // Load existing jobs on mount
  useEffect(() => {
    invoke<JobInfo[]>("get_jobs").then(setJobs).catch(console.error);
  }, []);

  // Listen for job updates from Rust backend
  useEffect(() => {
    const unlisten = listen<JobInfo>("job-update", (event) => {
      setJobs((prev) => {
        const idx = prev.findIndex((j) => j.id === event.payload.id);
        if (idx >= 0) {
          const updated = [...prev];
          updated[idx] = event.payload;
          return updated;
        }
        return [event.payload, ...prev];
      });
    });

    return () => {
      unlisten.then((fn) => fn());
    };
  }, []);

  const handleSelectImage = async () => {
    const file = await open({
      multiple: false,
      filters: [{ name: "Images", extensions: ["png", "jpg", "jpeg", "webp"] }],
    });
    if (file) {
      setImagePath(file);
    }
  };

  const handleGenerate = async () => {
    if (!prompt.trim()) return;
    setSubmitting(true);

    const payload: GeneratePayload = {
      prompt: prompt.trim(),
      duration,
      aspect_ratio: aspectRatio,
      resolution,
    };

    if (imagePath) {
      payload.image_path = imagePath;
    }

    setError("");
    try {
      const job = await invoke<JobInfo>("create_video_task", { payload });
      setJobs((prev) => [job, ...prev]);
      setPrompt("");
      setImagePath(null);
    } catch (e) {
      setError(String(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen bg-gray-950 text-white">
      {/* Header */}
      <header className="border-b border-gray-800 px-6 py-4">
        <h1 className="text-xl font-semibold">Grok Video Generator</h1>
      </header>

      <div className="flex h-[calc(100vh-65px)]">
        {/* Left: Input Section */}
        <div className="w-[420px] border-r border-gray-800 p-6 flex flex-col gap-5 overflow-y-auto">
          {/* Prompt */}
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-1.5">
              Prompt
            </label>
            <textarea
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              rows={4}
              placeholder="Describe the video you want to generate..."
              className="w-full px-4 py-3 bg-gray-800 border border-gray-700 rounded-xl text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none transition"
            />
          </div>

          {/* Duration slider */}
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-1.5">
              Duration: {duration}s
            </label>
            <input
              type="range"
              min={5}
              max={15}
              step={5}
              value={duration}
              onChange={(e) => setDuration(Number(e.target.value))}
              className="w-full accent-blue-500"
            />
            <div className="flex justify-between text-xs text-gray-500 mt-1">
              <span>5s</span>
              <span>10s</span>
              <span>15s</span>
            </div>
          </div>

          {/* Aspect Ratio */}
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-1.5">
              Aspect Ratio
            </label>
            <select
              value={aspectRatio}
              onChange={(e) => setAspectRatio(e.target.value)}
              className="w-full px-4 py-2.5 bg-gray-800 border border-gray-700 rounded-xl text-white focus:outline-none focus:ring-2 focus:ring-blue-500 transition"
            >
              <option value="16:9">16:9 (Landscape)</option>
              <option value="9:16">9:16 (Portrait)</option>
              <option value="1:1">1:1 (Square)</option>
              <option value="4:3">4:3</option>
              <option value="3:4">3:4</option>
            </select>
          </div>

          {/* Resolution */}
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-1.5">
              Resolution
            </label>
            <select
              value={resolution}
              onChange={(e) => setResolution(e.target.value)}
              className="w-full px-4 py-2.5 bg-gray-800 border border-gray-700 rounded-xl text-white focus:outline-none focus:ring-2 focus:ring-blue-500 transition"
            >
              <option value="720p">720p</option>
              <option value="480p">480p</option>
            </select>
          </div>

          {/* Image-to-Video */}
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-1.5">
              Reference Image (optional)
            </label>
            <button
              onClick={handleSelectImage}
              className="w-full py-2.5 border border-dashed border-gray-600 rounded-xl text-gray-400 hover:border-blue-500 hover:text-blue-400 transition text-sm"
            >
              {imagePath
                ? imagePath.split("/").pop()
                : "Click to select image..."}
            </button>
            {imagePath && (
              <button
                onClick={() => setImagePath(null)}
                className="mt-1 text-xs text-red-400 hover:text-red-300"
              >
                Remove image
              </button>
            )}
          </div>

          {/* Error display */}
          {error && (
            <div className="p-3 bg-red-500/10 border border-red-500/30 rounded-xl">
              <p className="text-sm text-red-400">{error}</p>
            </div>
          )}

          {/* Generate button */}
          <button
            onClick={handleGenerate}
            disabled={submitting || !prompt.trim()}
            className="w-full py-3 bg-blue-600 hover:bg-blue-500 disabled:bg-gray-700 disabled:cursor-not-allowed text-white font-medium rounded-xl transition mt-auto"
          >
            {submitting ? "Submitting..." : "Generate Video"}
          </button>
        </div>

        {/* Right: Job Queue */}
        <div className="flex-1 p-6 overflow-y-auto">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-medium">
              Queue{" "}
              <span className="text-gray-500 text-sm">({jobs.length})</span>
            </h2>
            <button
              onClick={async () => {
                try {
                  const dir = await invoke<string>("get_output_dir");
                  await openPath(dir);
                } catch (e) {
                  console.error("Failed to open output folder:", e);
                }
              }}
              className="text-sm text-gray-400 hover:text-blue-400 transition"
            >
              Open Output Folder
            </button>
          </div>

          {jobs.length === 0 ? (
            <div className="flex items-center justify-center h-64 text-gray-500">
              <p>No jobs yet. Create your first video!</p>
            </div>
          ) : (
            <div className="grid gap-3">
              {jobs.map((job) => (
                <JobCard key={job.id} job={job} />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
