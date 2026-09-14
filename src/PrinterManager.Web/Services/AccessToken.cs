using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace PrinterManager.Web.Services;

/// <summary>
/// Das JWT des Servers als Claim im Anmelde-Cookie. Die Web-Anwendung prüft das Token nicht
/// selbst — das tut der Server bei jedem Aufruf. Hier wird es nur mitgeführt und sein Ablauf
/// gelesen, damit das Cookie nicht länger gilt als das Token.
/// </summary>
public static class AccessToken
{
    public const string ClaimType = "printermanager:access_token";

    public static string? From(ClaimsPrincipal user) => user.FindFirst(ClaimType)?.Value;

    /// <summary>Liest den <c>exp</c>-Claim, ohne die Signatur zu prüfen.</summary>
    /// <returns><c>null</c>, wenn das Token kein lesbares <c>exp</c> enthält.</returns>
    public static DateTimeOffset? ReadExpiry(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return null;

        try
        {
            using var payload = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            // ValueKind zuerst: TryGetInt64 wirft bei einem String, statt false zu liefern.
            return payload.RootElement.ValueKind == JsonValueKind.Object
                && payload.RootElement.TryGetProperty("exp", out var exp)
                && exp.ValueKind == JsonValueKind.Number
                && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
