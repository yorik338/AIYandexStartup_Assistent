using JarvisCore.Models;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace JarvisCore.Services;

/// <summary>
/// Records audio from the default microphone and saves it to disk.
/// </summary>
public class MicrophoneRecorder
{
    private const int DefaultSampleRate = 16_000;
    private const int DefaultChannels = 1;
    private readonly ILogger<MicrophoneRecorder> _logger;
    private readonly string _recordingsDirectory;

    public MicrophoneRecorder(ILogger<MicrophoneRecorder> logger)
    {
        _logger = logger;
        _recordingsDirectory = Path.Combine(AppContext.BaseDirectory, "recordings");
        Directory.CreateDirectory(_recordingsDirectory);
    }

    /// <summary>
    /// Records audio for a fixed duration and returns metadata with Base64 data.
    /// </summary>
    public async Task<AudioRecordResult> RecordAsync(double durationSeconds, string? fileName = null, int sampleRate = DefaultSampleRate, int channels = DefaultChannels)
    {
        if (durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be greater than zero seconds.");
        }

        if (durationSeconds > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration cannot exceed 300 seconds for safety.");
        }

        if (channels is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(channels), "Only mono or stereo capture is supported.");
        }

        string resolvedFileName = fileName;
        if (string.IsNullOrWhiteSpace(resolvedFileName))
        {
            resolvedFileName = $"mic-{DateTime.UtcNow:yyyyMMddTHHmmss}.wav";
        }

        string targetPath = Path.Combine(_recordingsDirectory, resolvedFileName);
        var waveFormat = new WaveFormat(sampleRate, 16, channels);

        _logger.LogInformation("Starting microphone capture for {Duration:F1}s at {SampleRate}Hz ({Channels}ch). Output: {Path}", durationSeconds, sampleRate, channels, targetPath);

        using var waveIn = new WaveInEvent
        {
            WaveFormat = waveFormat,
            BufferMilliseconds = 200
        };

        using var waveFileWriter = new WaveFileWriter(targetPath, waveFormat);
        var recordingCompleted = new TaskCompletionSource<bool>();
        Exception? recordException = null;

        waveIn.DataAvailable += (_, args) =>
        {
            try
            {
                waveFileWriter.Write(args.Buffer, 0, args.BytesRecorded);
            }
            catch (Exception ex)
            {
                recordException = ex;
                recordingCompleted.TrySetResult(false);
            }
        };

        waveIn.RecordingStopped += (_, args) =>
        {
            if (args.Exception != null)
            {
                recordException = args.Exception;
            }

            recordingCompleted.TrySetResult(true);
        };

        waveIn.StartRecording();
        await Task.Delay(TimeSpan.FromSeconds(durationSeconds));
        waveIn.StopRecording();

        await recordingCompleted.Task.ConfigureAwait(false);

        if (recordException != null)
        {
            _logger.LogError(recordException, "Microphone recording failed");
            throw new InvalidOperationException("Microphone recording failed", recordException);
        }

        await waveFileWriter.FlushAsync();

        byte[] data = await File.ReadAllBytesAsync(targetPath);
        string base64 = Convert.ToBase64String(data);

        _logger.LogInformation("Microphone capture finished. Saved {Bytes} bytes to {Path}", data.Length, targetPath);

        return new AudioRecordResult
        {
            Success = true,
            FileName = Path.GetFileName(targetPath),
            Path = targetPath,
            DurationSeconds = durationSeconds,
            SampleRate = sampleRate,
            Channels = channels,
            SizeBytes = data.Length,
            Format = "wav",
            Base64Data = base64,
            CapturedAt = DateTime.UtcNow
        };
    }
}
