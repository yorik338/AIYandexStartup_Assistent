namespace JarvisCore.Models;

/// <summary>
/// Result of a microphone recording session.
/// </summary>
public class AudioRecordResult
{
    public bool Success { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public long SizeBytes { get; set; }
    public string Format { get; set; } = "wav";
    public string Base64Data { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public string? Error { get; set; }
}
