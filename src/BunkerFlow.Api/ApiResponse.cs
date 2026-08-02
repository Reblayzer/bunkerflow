namespace BunkerFlow.Api;

/// <summary>
/// One response envelope for every endpoint. Clients check <c>ok</c> before
/// touching <c>data</c>, and success and failure never share a shape.
/// </summary>
public static class ApiResponse
{
    public static OkEnvelope<T> Ok<T>(T data) => new(true, data);

    public static ErrorEnvelope Error(string code, string message) => new(false, new ApiError(code, message));
}

public sealed record OkEnvelope<T>(bool Ok, T Data);

public sealed record ErrorEnvelope(bool Ok, ApiError Error);

public sealed record ApiError(string Code, string Message);
