using PrinterManager.Server.Security;

namespace PrinterManager.Server.Setup;

/// <summary>
/// Fasst beim Start zusammen, wie der Server konfiguriert ist, und nennt die Schritte
/// zum Absichern einer frisch eingerichteten Installation.
/// </summary>
public static class StartupReport
{
    public static void Write(
        WebApplication app,
        LocalSecrets.Result secrets,
        HttpsSetupResult https,
        ClientAuthenticationMode clientAuthentication)
    {
        ReportSecrets(app, secrets);
        ReportHttps(app, https);
        ReportClientAuthentication(app, https, clientAuthentication);
    }

    private static void ReportSecrets(WebApplication app, LocalSecrets.Result secrets)
    {
        if (!secrets.AnythingGenerated)
            return;

        app.Logger.LogInformation(
            "Fehlende Geheimnisse erzeugt ({Paths}) und abgelegt in: {Path}",
            string.Join(", ", secrets.GeneratedPaths),
            secrets.Path ?? "— konnte nicht geschrieben werden");

        if (secrets.Path == null)
        {
            app.Logger.LogWarning(
                "Die erzeugten Geheimnisse konnten nicht gespeichert werden. Sie gelten nur für " +
                "diesen Prozess — nach einem Neustart müssen sich alle Benutzer neu anmelden. " +
                "Bitte Schreibrechte auf {Directory} geben oder Jwt:Key fest konfigurieren.",
                app.Environment.ContentRootPath);
        }
    }

    private static void ReportHttps(WebApplication app, HttpsSetupResult https)
    {
        foreach (var warning in https.Warnings)
        {
            app.Logger.LogWarning("HTTPS: {Warning}", warning);
        }

        if (!https.Enabled)
        {
            app.Logger.LogWarning(
                "HTTPS ist nicht aktiv — Anmeldedaten und Tokens gehen im Klartext über das Netz.");
            return;
        }

        app.Logger.LogInformation(
            "HTTPS auf Port {Port} aktiv (Zertifikat: {Source}, Fingerabdruck: {Thumbprint})",
            https.Port, https.Source, https.Thumbprint);

        if (https.IsSelfSigned)
        {
            app.Logger.LogWarning(
                """

                ======================================================================
                 HTTPS läuft mit einem SELBST SIGNIERTEN Zertifikat
                 Browser und Clients stufen es als nicht vertrauenswürdig ein.

                 Für den Testbetrieb bei Client und Web-Anwendung eintragen:
                   "ServerCertificateThumbprint": "{Thumbprint}"

                 Für den Produktivbetrieb ein Zertifikat der eigenen CA verwenden
                 (Https:CertificateThumbprint oder Https:CertificatePath) und
                 anschließend "Https:RedirectToHttps" auf true setzen.
                ======================================================================
                """,
                https.Thumbprint);
        }
    }

    private static void ReportClientAuthentication(
        WebApplication app, HttpsSetupResult https, ClientAuthenticationMode mode)
    {
        switch (mode)
        {
            case ClientAuthenticationMode.Windows:
                app.Logger.LogInformation(
                    "Client-Endpunkte nutzen Windows-Authentifizierung (Negotiate). Die Identität " +
                    "stammt aus dem Kerberos-Ticket, nicht aus den Angaben des Clients.");
                break;

            case ClientAuthenticationMode.ApiKey:
                app.Logger.LogInformation(
                    "Client-Endpunkte erwarten den Header {Header}.", ClientAuthenticationOptions.HeaderName);

                if (!https.Enabled)
                {
                    app.Logger.LogWarning(
                        "Der Client-Schlüssel geht ohne HTTPS im Klartext über das Netz und ist " +
                        "beliebig wiederverwendbar.");
                }
                break;

            default:
                app.Logger.LogWarning(
                    """

                    ======================================================================
                     Client-Endpunkte sind NICHT authentifiziert
                     /api/clients/register und /api/clients/actions sind offen. Jeder im
                     Netz kann sich als beliebiger Benutzer ausgeben.

                     Empfohlen in einer Windows-Domäne — verlangt keine Schlüsselverteilung:
                       "ClientApi": {{ "Authentication": "Windows" }}

                     Sonst mit gemeinsamem Schlüssel:
                       1. Beim Client in appsettings.json:
                            "ClientApiKey": "{ClientKey}"
                       2. Auf dem Server in {File}:
                            "ClientApi": {{ "Authentication": "ApiKey" }}
                    ======================================================================
                    """,
                    app.Configuration[ClientAuthenticationOptions.ApiKeyKey],
                    LocalSettingsFile.FileName);
                break;
        }
    }
}
