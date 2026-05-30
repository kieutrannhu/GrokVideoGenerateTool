import { revealItemInDir } from "@tauri-apps/plugin-opener";
import type { JobInfo } from "../types";

interface Props {
  job: JobInfo;
}

const statusColors: Record<string, string> = {
  pending: "bg-yellow-500/20 text-yellow-400",
  processing: "bg-blue-500/20 text-blue-400",
  downloading: "bg-purple-500/20 text-purple-400",
  done: "bg-green-500/20 text-green-400",
  failed: "bg-red-500/20 text-red-400",
};

export default function JobCard({ job }: Props) {
  const colorClass = statusColors[job.status] || statusColors.pending;
  const outputPath = job.task_type === "image" ? job.image_path : job.video_path;

  const openFolder = async () => {
    if (outputPath) {
      try {
        await revealItemInDir(outputPath);
      } catch (e) {
        console.error("Failed to open folder:", e);
      }
    }
  };

  return (
    <div className="bg-gray-800/50 rounded-xl p-4 border border-gray-700/50">
      <div className="flex items-start justify-between gap-3 mb-3">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <span className="text-xs text-gray-500 font-medium uppercase tracking-wide">
              {job.task_type === "image" ? "Image" : "Video"}
            </span>
          </div>
          <p className="text-sm text-gray-200 line-clamp-2">{job.prompt}</p>
        </div>
        <span
          className={`shrink-0 text-xs font-medium px-2.5 py-1 rounded-full ${colorClass}`}
        >
          {job.status}
        </span>
      </div>

      {/* Progress bar */}
      {job.status !== "done" && job.status !== "failed" && (
        <div className="w-full bg-gray-700 rounded-full h-2 mb-2">
          <div
            className="bg-blue-500 h-2 rounded-full transition-all duration-500"
            style={{ width: `${job.progress}%` }}
          />
        </div>
      )}

      {job.status === "done" && job.progress === 100 && (
        <div className="w-full bg-gray-700 rounded-full h-2 mb-2">
          <div className="bg-green-500 h-2 rounded-full w-full" />
        </div>
      )}

      {/* Footer */}
      <div className="flex items-center justify-between mt-2">
        <span className="text-xs text-gray-500">{job.id.substring(0, 8)}</span>

        {job.status === "done" && outputPath && (
          <button
            onClick={openFolder}
            className="text-xs text-blue-400 hover:text-blue-300 transition"
          >
            Open Folder
          </button>
        )}

        {job.status === "failed" && job.error && (
          <span className="text-xs text-red-400 truncate max-w-[200px]">
            {job.error}
          </span>
        )}
      </div>
    </div>
  );
}
