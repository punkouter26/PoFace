namespace PoFace.Api.Features.Diagnostics;

public sealed class ConfigMaskingService
{
    public string Mask(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return "****";

        if (key.Length < 8)
            return "****";

        return $"{key[..4]}****{key[^4..]}";
    }
}
