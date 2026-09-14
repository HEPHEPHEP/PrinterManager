using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace PrinterManager.Shared.Http;

/// <summary>
/// Erlaubt genau ein Serverzertifikat anhand seines Fingerabdrucks. Gedacht für
/// selbst signierte Zertifikate: statt die Zertifikatsprüfung abzuschalten, wird das
/// erwartete Zertifikat festgenagelt. Ein ausgetauschtes Zertifikat fällt damit auf.
/// </summary>
public static class ServerCertificatePinning
{
    /// <summary>
    /// Liefert eine Prüfroutine für <c>HttpClientHandler.ServerCertificateCustomValidationCallback</c>
    /// oder <c>null</c>, wenn kein Fingerabdruck hinterlegt ist — dann gilt die normale Prüfung.
    /// </summary>
    public static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>?
        CreateValidator(string? expectedThumbprint)
    {
        if (string.IsNullOrWhiteSpace(expectedThumbprint))
            return null;

        var expected = Normalize(expectedThumbprint);

        return (_, certificate, _, errors) =>
        {
            if (certificate == null)
                return false;

            // Ein regulär gültiges Zertifikat wird ohnehin akzeptiert.
            if (errors == SslPolicyErrors.None)
                return true;

            return string.Equals(Normalize(certificate.Thumbprint), expected, StringComparison.OrdinalIgnoreCase);
        };
    }

    /// <summary>Entfernt Leerzeichen und unsichtbare Zeichen, die beim Kopieren mitkommen.</summary>
    private static string Normalize(string thumbprint) =>
        new(thumbprint.Where(char.IsLetterOrDigit).ToArray());
}
