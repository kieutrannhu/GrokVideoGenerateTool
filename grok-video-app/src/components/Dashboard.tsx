import { useEffect, useState } from "react";
import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import { open } from "@tauri-apps/plugin-dialog";
import { openPath } from "@tauri-apps/plugin-opener";
import type { JobInfo, GeneratePayload, ImagePayload } from "../types";
import JobCard from "./JobCard";

type Mode = "video" | "image";

export default function Dashboard() {
  const [mode, setMode] = useState<Mode>("video");

  // Video state
  const [prompt, setPrompt] = useState("");
  const [duration, setDuration] = useState(5);
  const [aspectRatio, setAspectRatio] = useState("16:9");
  const [resolution, setResolution] = useState("720p");
  const [imagePath, setImagePath] = useState<string | null>(null);

  // Image state
  const [imagePrompt, setImagePrompt] = useState("");
  const [imageCount, setImageCount] = useState(1);
  const [imageAspectRatio, setImageAspectRatio] = useState("1:1");
  const [imageResolution, setImageResolution] = useState("1k");
  const [refImagePath, setRefImagePath] = useState<string | null>(null);

  const [jobs, setJobs] = useState<JobInfo[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    invoke<JobInfo[]>("get_jobs").then(setJobs).catch(console.error);
  }, []);

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

  const handleGenerateVideo = async () => {
    if (!prompt.trim()) return;
    setSubmitting(true);
    setError("");

    const payload: GeneratePayload = {
      prompt: prompt.trim(),
      duration,
      aspect_ratio: aspectRatio,
      resolution,
    };

    if (imagePath) {
      payload.image_path = imagePath;
    }

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

  const handleSelectRefImage = async () => {
    const file = await open({
      multiple: false,
      filters: [{ name: "Images", extensions: ["png", "jpg", "jpeg", "webp"] }],
    });
    if (file) {
      setRefImagePath(file);
    }
  };

  const handleGenerateImage = async () => {
    if (!imagePrompt.trim()) return;
    setSubmitting(true);
    setError("");

    const payload: ImagePayload = {
      prompt: imagePrompt.trim(),
      count: imageCount,
      aspect_ratio: imageAspectRatio,
      resolution: imageResolution,
    };

    if (refImagePath) {
      payload.image_path = refImagePath;
    }

    try {
      const job = await invoke<JobInfo>("create_image_task", { payload });
      setJobs((prev) => [job, ...prev]);
      setImagePrompt("");
      setRefImagePath(null);
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
        <h1 className="text-xl font-semibold">Grok Generator</h1>
      </header>

      <div className="flex h-[calc(100vh-65px)]">
        {/* Left: Input Section */}
        <div className="w-[420px] border-r border-gray-800 p-6 flex flex-col gap-5 overflow-y-auto">
          {/* Tab switcher */}
          <div className="flex bg-gray-800 rounded-xl p-1 gap-1">
            <button
              onClick={() => { setMode("video"); setError(""); }}
              className={`flex-1 py-2 text-sm font-medium rounded-lg transition ${
                mode === "video"
                  ? "bg-blue-600 text-white"
                  : "text-gray-400 hover:text-white"
              }`}
            >
              Video
            </button>
            <button
              onClick={() => { setMode("image"); setError(""); }}
              className={`flex-1 py-2 text-sm font-medium rounded-lg transition ${
                mode === "image"
                  ? "bg-purple-600 text-white"
                  : "text-gray-400 hover:text-white"
              }`}
            >
              Image
            </button>
          </div>

          {mode === "video" ? (
            <>
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

              <button
                onClick={handleGenerateVideo}
                disabled={submitting || !prompt.trim()}
                className="w-full py-3 bg-blue-600 hover:bg-blue-500 disabled:bg-gray-700 disabled:cursor-not-allowed text-white font-medium rounded-xl transition mt-auto"
              >
                {submitting ? "Submitting..." : "Generate Video"}
              </button>
            </>
          ) : (
            <>
              {/* Image Prompt */}
              <div>
                <label className="block text-sm font-medium text-gray-300 mb-1.5">
                  Prompt
                </label>
                <textarea
                  value={imagePrompt}
                  onChange={(e) => setImagePrompt(e.target.value)}
                  rows={5}
                  placeholder="Describe the image you want to generate..."
                  className="w-full px-4 py-3 bg-gray-800 border border-gray-700 rounded-xl text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-purple-500 resize-none transition"
                />
              </div>

              {/* Number of images */}
              <div>
                <label className="block text-sm font-medium text-gray-300 mb-1.5">
                  Number of Images: {imageCount}
                </label>
                <input
                  type="range"
                  min={1}
                  max={4}
                  step={1}
                  value={imageCount}
                  onChange={(e) => setImageCount(Number(e.target.value))}
                  className="w-full accent-purple-500"
                />
                <div className="flex justify-between text-xs text-gray-500 mt-1">
                  <span>1</span>
                  <span>2</span>
                  <span>3</span>
                  <span>4</span>
                </div>
              </div>

              {/* Aspect Ratio */}
              <div>
                <label className="block text-sm font-medium text-gray-300 mb-1.5">
                  Aspect Ratio
                </label>
                <select
                  value={imageAspectRatio}
                  onChange={(e) => setImageAspectRatio(e.target.value)}
                  className="w-full px-4 py-2.5 bg-gray-800 border border-gray-700 rounded-xl text-white focus:outline-none focus:ring-2 focus:ring-purple-500 transition"
                >
                  <option value="1:1">1:1 (Square)</option>
                  <option value="16:9">16:9 (Landscape)</option>
                  <option value="9:16">9:16 (Portrait)</option>
                  <option value="4:3">4:3</option>
                  <option value="3:4">3:4</option>
                  <option value="3:2">3:2</option>
                  <option value="2:3">2:3</option>
                  <option value="2:1">2:1</option>
                  <option value="1:2">1:2</option>
                  <option value="auto">Auto</option>
                </select>
              </div>

              {/* Resolution */}
              <div>
                <label className="block text-sm font-medium text-gray-300 mb-1.5">
                  Resolution
                </label>
                <select
                  value={imageResolution}
                  onChange={(e) => setImageResolution(e.target.value)}
                  className="w-full px-4 py-2.5 bg-gray-800 border border-gray-700 rounded-xl text-white focus:outline-none focus:ring-2 focus:ring-purple-500 transition"
                >
                  <option value="1k">1K</option>
                  <option value="2k">2K</option>
                </select>
              </div>

              {/* Reference image for img2img */}
              <div>
                <label className="block text-sm font-medium text-gray-300 mb-1.5">
                  Reference Image (optional)
                </label>
                <button
                  onClick={handleSelectRefImage}
                  className="w-full py-2.5 border border-dashed border-gray-600 rounded-xl text-gray-400 hover:border-purple-500 hover:text-purple-400 transition text-sm"
                >
                  {refImagePath
                    ? refImagePath.split("/").pop()
                    : "Click to select image..."}
                </button>
                {refImagePath && (
                  <button
                    onClick={() => setRefImagePath(null)}
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

              <button
                onClick={handleGenerateImage}
                disabled={submitting || !imagePrompt.trim()}
                className="w-full py-3 bg-purple-600 hover:bg-purple-500 disabled:bg-gray-700 disabled:cursor-not-allowed text-white font-medium rounded-xl transition mt-auto"
              >
                {submitting ? "Generating..." : "Generate Image"}
              </button>
            </>
          )}
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
              <p>No jobs yet. Generate your first video or image!</p>
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
