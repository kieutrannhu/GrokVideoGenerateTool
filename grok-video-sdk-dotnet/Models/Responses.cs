using Newtonsoft.Json;

namespace GrokVideoSdk.Models
{
    public class SubmitResponse
    {
        [JsonProperty("request_id")]
        public string RequestId { get; set; }
    }

    public class StatusResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("video")]
        public VideoData Video { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("progress")]
        public int? Progress { get; set; }
    }

    public class VideoData
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("duration")]
        public float? Duration { get; set; }
    }

    public class ApiErrorBody
    {
        [JsonProperty("error")]
        public ApiErrorDetail Error { get; set; }
    }

    public class ApiErrorDetail
    {
        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
