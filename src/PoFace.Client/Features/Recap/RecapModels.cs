using System.Text.Json.Serialization;

namespace PoFace.Client.Features.Recap;

/// <summary>Client-side DTO matching the GET /api/recap/{sessionId} response contract.</summary>
public sealed class RecapDto
{
    public string              SessionId      { get; set; } = string.Empty;
    public string              UserId         { get; set; } = string.Empty;
    public string              DisplayName    { get; set; } = string.Empty;
    public int                 TotalScore     { get; set; }
    public bool                IsPersonalBest { get; set; }
    public DateTimeOffset      CompletedAt    { get; set; }
    public List<RecapRoundDto> Rounds         { get; set; } = [];
}

/// <summary>A single round's result within a RecapDto.</summary>
public sealed class RecapRoundDto
{
    public int            RoundNumber   { get; set; }
    public string         TargetEmotion { get; set; } = string.Empty;
    public int            Score         { get; set; }
    public bool           HeadPoseValid { get; set; }
    public string         ImageUrl      { get; set; } = string.Empty;
    public DateTimeOffset CapturedAt    { get; set; }
    public List<RecapLandmarkDto> Landmarks { get; set; } = [];
}

public sealed class RecapLandmarkDto
{
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("z")] public float Z { get; set; }
}
