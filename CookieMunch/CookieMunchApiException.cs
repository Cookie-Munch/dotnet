namespace CookieMunch;

/// <summary>
/// Thrown for any non-2xx response from the Cookie Munch API. Carries the HTTP
/// <see cref="StatusCode"/> and the raw response <see cref="Body"/>. When the server
/// returns a JSON error envelope (<c>{ "error": "...", "code": "..." }</c>) the parsed
/// values are exposed via <see cref="Error"/> and <see cref="Code"/>, and the exception
/// <see cref="System.Exception.Message"/> is the server's <c>error</c> string.
/// </summary>
public sealed class CookieMunchApiException : Exception
{
    /// <summary>The HTTP status code of the failing response.</summary>
    public int StatusCode { get; }

    /// <summary>The raw response body (may be empty). Never <c>null</c>.</summary>
    public string Body { get; }

    /// <summary>The server's <c>error</c> field, when the body was a JSON error envelope.</summary>
    public string? Error { get; }

    /// <summary>The server's machine-readable <c>code</c> field, when present.</summary>
    public string? Code { get; }

    /// <summary>Create an API exception.</summary>
    public CookieMunchApiException(int statusCode, string body, string? error = null, string? code = null)
        : base(error ?? $"Cookie Munch request failed with status {statusCode}")
    {
        StatusCode = statusCode;
        Body = body ?? string.Empty;
        Error = error;
        Code = code;
    }
}
