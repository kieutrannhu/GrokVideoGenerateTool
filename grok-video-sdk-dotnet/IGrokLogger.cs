namespace GrokVideoSdk
{
    /// <summary>
    /// Simple logging interface for GrokClient.
    /// Implement this to capture SDK log messages.
    /// </summary>
    public interface IGrokLogger
    {
        void Log(string message);
    }
}
