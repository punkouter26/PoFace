using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace PoFace.Api.Infrastructure.Telemetry;

/// <summary>
/// Registers the PoFace custom OTel metrics meter and its instruments.
/// Call <see cref="AddPoFaceMetrics"/> from Program.cs during service registration.
/// </summary>
public static class OtelMetrics
{
    public const string MeterName = "PoFace.Api.Metrics";

    // Shared meter — resolved once at startup
    public static readonly Meter Meter = new(MeterName, "1.0");

    // In-memory store for the observable gauge callback.
    // Keyed by emotion label (e.g. "happiness"), value is latest average confidence [0–1].
    private static readonly ConcurrentDictionary<string, double> _emotionAverages = new();

    /// <summary>
    /// Observable gauge that tracks average emotion intensity per emotion label.
    /// Updated by calling <see cref="RecordEmotionIntensity"/> from ScoreRoundHandler.
    /// </summary>
    public static readonly ObservableGauge<double> EmotionIntensityAverage =
        Meter.CreateObservableGauge<double>(
            "emotion.intensity.average",
            observeValues: () => _emotionAverages.Select(
                kv => new Measurement<double>(
                    kv.Value,
                    new KeyValuePair<string, object?>("emotion", kv.Key))),
            unit: "confidence",
            description: "Average raw confidence for each emotion across recent rounds.");

    /// <summary>
    /// Counter incremented once per completed game session (all 5 rounds scored).
    /// </summary>
    public static readonly Counter<long> SessionCompletionCount =
        Meter.CreateCounter<long>(
            "session.completion.count",
            unit: "sessions",
            description: "Total number of completed game sessions.");

    /// <summary>
    /// Records (overwrites) the latest average confidence for a given emotion.
    /// Called from ScoreRoundHandler after each Face API response.
    /// </summary>
    public static void RecordEmotionIntensity(string emotion, double averageConfidence)
        => _emotionAverages[emotion] = averageConfidence;

    /// <summary>
    /// Registers the custom meter. Azure Monitor exporter wiring is added from Program.cs
    /// when an Application Insights connection string is available.
    /// </summary>
    public static IOpenTelemetryBuilder AddPoFaceMetrics(this IOpenTelemetryBuilder otelBuilder)
    {
        otelBuilder.WithMetrics(metrics => metrics.AddMeter(MeterName));
        return otelBuilder;
    }
}

