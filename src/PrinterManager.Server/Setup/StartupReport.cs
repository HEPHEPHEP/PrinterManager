using PrinterManager.Server.Security;

namespace PrinterManager.Server.Setup;

/// <summary>
/// Fasst beim Start zusammen, wie der Server konfiguriert ist, und nennt die Schritte
/// zum Absichern einer frisch eingerichteten Installation.
/// </summary>
public static class StartupReport
{
    public static void Write(WebApplication app, LocalSecrets.Result secrets)
    {
        if (secrets.AnythingGenerated)
        {
            app.Logger.LogInformation(
                "Fehlende Geheimnisse erzeugt ({Paths}) und abgelegt in: {Path}",
                string.Join(", ", secrets.GeneratedPaths),
                secrets.Path ?? "— konnte nicht geschrieben werden");
        }

        if (secrets.AnythingGenerated && secrets.Path == null)
        {
            app.Logger.LogWarning(
                "Die erzeugten Geheimnisse konnten nicht gespeichert werden. Sie gelten nur für " +
                "diesen Prozess — nach einem Neustart müssen sich alle Benutzer neu anmelden. " +
                "Bitte Schreibrechte auf {Directory} geben oder Jwt:Key fest konfigurieren.",
                app.Environment.ContentRootPath);
        }

        if (!ClientApiKeyFilter.IsRequired(app.Configuration))
        {
            app.Logger.LogWarning(
                """

                ======================================================================
                 Client-Endpunkte sind NICHT authentifiziert
                 /api/clients/register und /api/clients/actions sind offen.

                 Zum Absichern:
                   1. Auf dem Client in appsettings.json eintragen:
                        "ClientApiKey": "{ClientKey}"
                   2. Auf dem Server "ClientApi:RequireKey" auf true setzen
                      (in {File}) und den Server neu starten.
                ======================================================================
                """,
                app.Configuration[ClientApiKeyFilter.ConfigurationKey],
                LocalSecrets.FileName);
        }
    }
}
