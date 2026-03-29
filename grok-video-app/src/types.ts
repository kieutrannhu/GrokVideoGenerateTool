export interface JobInfo {
  id: string;
  request_id: string | null;
  prompt: string;
  status: string;
  progress: number;
  video_path: string | null;
  error: string | null;
}

export interface GeneratePayload {
  prompt: string;
  duration?: number;
  aspect_ratio?: string;
  resolution?: string;
  image_path?: string;
}
