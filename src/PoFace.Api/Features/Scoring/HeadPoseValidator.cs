namespace PoFace.Api.Features.Scoring;

/// <summary>
/// Validates head-pose angles from the Face API.
/// A pose is valid when neither yaw nor pitch exceed ±20 degrees.
/// Invalid pose forces the round score to 0.
/// </summary>
public static class HeadPoseValidator
{
    /// <summary>
    /// Returns <c>true</c> when the pose is within the allowed range.
    /// Boundary values (±20) are considered valid per the data model specification.
    /// </summary>
    public static bool Validate(double yaw, double pitch)
        => Math.Abs(yaw) <= 20.0 && Math.Abs(pitch) <= 20.0;
}
