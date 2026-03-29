using Newtonsoft.Json;

namespace GrokVideoSdk.Models
{
    public class VideoGenerationRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; } = "grok-imagine-video";

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("duration", NullValueHandling = NullValueHandling.Ignore)]
        public int? Duration { get; set; }

        [JsonProperty("aspect_ratio", NullValueHandling = NullValueHandling.Ignore)]
        public string AspectRatio { get; set; }

        [JsonProperty("resolution", NullValueHandling = NullValueHandling.Ignore)]
        public string Resolution { get; set; }

        [JsonProperty("image", NullValueHandling = NullValueHandling.Ignore)]
        public ImageInput Image { get; set; }

        public VideoGenerationRequest(string prompt)
        {
            Prompt = prompt;
        }

        public VideoGenerationRequest WithDuration(int seconds)
        {
            Duration = seconds;
            return this;
        }

        public VideoGenerationRequest WithAspectRatio(string ratio)
        {
            AspectRatio = ratio;
            return this;
        }

        public VideoGenerationRequest WithResolution(string resolution)
        {
            Resolution = resolution;
            return this;
        }

        public VideoGenerationRequest WithImageUrl(string url)
        {
            Image = new ImageInput { Url = url };
            return this;
        }
    }

    public class ImageInput
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}
