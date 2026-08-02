namespace BunkerFlow.Api.Security;

/// <summary>
/// API key configuration. A list rather than a single value so a key can be
/// rotated: add the new one, move callers across, remove the old one, with no
/// window where every client is broken.
/// </summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "Api";

    public const string HeaderName = "X-Api-Key";

    public IReadOnlyList<string> Keys { get; set; } = [];

    /// <summary>
    /// With no keys configured the protected endpoints are open. That keeps a
    /// local run and the smoke script working with no setup, and it is logged
    /// loudly at startup. Any real deployment configures keys.
    /// </summary>
    public bool IsEnabled => Keys.Count > 0;
}
