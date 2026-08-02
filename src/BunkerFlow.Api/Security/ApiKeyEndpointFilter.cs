using System.Security.Cryptography;
using System.Text;

namespace BunkerFlow.Api.Security;

/// <summary>
/// Requires a valid API key on the endpoints it is applied to.
///
/// Comparison is constant-time. A plain string equality leaks how much of a key
/// was correct through response timing, which is exactly how a key gets guessed
/// one byte at a time.
/// </summary>
public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    private readonly byte[][] _keyHashes;

    public ApiKeyEndpointFilter(ApiKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Hashing first means every comparison runs over the same length,
        // whatever the caller sends.
        _keyHashes = [.. options.Keys.Select(Hash)];
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (_keyHashes.Length == 0)
        {
            return await next(context);
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyOptions.HeaderName, out var provided)
            || provided.Count == 0
            || string.IsNullOrWhiteSpace(provided[0]))
        {
            return Unauthorized("api_key_missing", $"The {ApiKeyOptions.HeaderName} header is required.");
        }

        return IsKnown(provided[0]!)
            ? await next(context)
            : Unauthorized("api_key_invalid", "The supplied API key is not valid.");
    }

    private bool IsKnown(string provided)
    {
        var providedHash = Hash(provided);

        var matched = false;
        foreach (var candidate in _keyHashes)
        {
            // No early exit: check every key so the time taken does not reveal
            // which one matched, or how many are configured.
            matched |= CryptographicOperations.FixedTimeEquals(providedHash, candidate);
        }

        return matched;
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static IResult Unauthorized(string code, string message) =>
        Results.Json(ApiResponse.Error(code, message), statusCode: StatusCodes.Status401Unauthorized);
}
