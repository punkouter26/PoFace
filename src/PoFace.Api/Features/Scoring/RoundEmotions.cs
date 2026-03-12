namespace PoFace.Api.Features.Scoring;

/// <summary>Canonical 5-round emotion sequence (FR-009 — order MUST NOT be shuffled).</summary>
internal static class RoundEmotions
{
    private static readonly string[] Order =
        ["Happiness", "Surprise", "Anger", "Sadness", "Fear"];

    public static string ForRound(int roundNumber) => Order[roundNumber - 1];

    public static IReadOnlyList<string> All => Order;
}
