# Windows Printer Manager

Ein umfassendes Druckerverwaltungssystem für Windows-Umgebungen, bestehend aus einem Management-Server, einem Client-Modul und einer Web-Anwendung.

## Übersicht

Der Windows Printer Manager ermöglicht die zentrale Verwaltung von Netzwerkdruckern über SMB-Freigaben. Das System besteht aus drei Hauptkomponenten:

1. **Management Server** - REST API Backend (ASP.NET Core)
2. **Client Module** - Autostart-Anwendung im Benutzerkontext für automatische Druckerinstallation
3. **Web Application** - Blazor Server UI für Verwaltung
<img width="1909" height="495" alt="grafik" src="https://github.com/user-attachments/assets/42c5f3a0-2820-4193-b557-a75516344973" />

## Schnellstart

Drei Terminals, keine Vorbereitung:

```bash
# 1. Server (erzeugt Schlüssel, Datenbank und Admin-Benutzer selbst)
cd src/PrinterManager.Server && dotnet run

# 2. Web-Oberfläche  ->  http://localhost:5001
cd src/PrinterManager.Web && dotnet run

# 3. Client (nur unter Windows)
cd src/PrinterManager.Client && dotnet run
```

Das Passwort für den Benutzer `admin` steht in der Startausgabe des Servers und in
`src/PrinterManager.Server/initial-admin-password.txt`.

Der Server lauscht auf **HTTP 5000** und **HTTPS 5443**. Für HTTPS erzeugt er beim ersten
Start ein selbst signiertes Zertifikat — gut zum Ausprobieren, für den Produktivbetrieb
ist ein Zertifikat der eigenen CA nötig.

Damit läuft alles, ist aber noch **nicht produktionsreif**: siehe
[Produktivbetrieb vorbereiten](#produktivbetrieb-vorbereiten).

## Hauptfunktionen

### Druckerverwaltung
- ✅ Drucker über eindeutige IDs verwalten
- ✅ Installation via SMB-Freigaben (`\\server\drucker`)
- ✅ Drucker als temporär nicht verfügbar markieren
- ✅ Ersatzdrucker definieren und automatisch zuweisen
- ✅ Druckerserver scannen und Drucker importieren

### Zuweisungen
- ✅ Drucker Benutzern (UserPrincipalName) zuweisen
- ✅ Drucker Clients (Hostname) zuweisen
- ✅ Konfigurierbare Priorität (Benutzer vs. Client)
- ✅ Standarddrucker festlegen

### Client-Registrierung
- ✅ Automatische Registrierung mit UserPrincipalName und Hostname
- ✅ Import bereits installierter Drucker bei Erstanmeldung
- ✅ Regelmäßige Synchronisation mit Server
- ✅ Automatische Installation/Deinstallation von Druckern

### Ersatzdrucker-Funktionalität
- ✅ Ersatzdrucker pro Drucker konfigurierbar
- ✅ Automatische Zuweisung bei Nichtverfügbarkeit
- ✅ Automatische Wiederherstellung bei Verfügbarkeit

### Sicherheit & Authentifizierung
- ✅ JWT-basierte Authentifizierung — **alle** API-Endpunkte sind per Fallback-Policy geschützt
- ✅ Web-Oberfläche mit Cookie-Anmeldung (HttpOnly, verschlüsselt) — übersteht Seitenwechsel und Neuladen
- ✅ Rollenbasierte Zugriffskontrolle (Administrator, Benutzer)
- ✅ Client-Endpunkte über Kerberos/NTLM oder gemeinsamen Schlüssel absicherbar
- ✅ Identität der Clients aus dem Kerberos-Ticket statt aus deren eigener Angabe
- ✅ Optionale LDAP/Active Directory-Integration (LDAPS bzw. StartTLS)
- ✅ HTTPS mit Zertifikat aus dem Windows-Zertifikatspeicher, PFX-Datei oder selbst signiert
- ✅ Passwort-Hashing mit BCrypt (Work Factor 12); alte SHA256-Hashes werden beim Login migriert
- ✅ Benutzerverwaltung über Web-Interface

## Architektur

```
PrinterManager/
├── src/
│   ├── PrinterManager.Shared/         # Shared Models & DTOs
│   │   ├── Models/
│   │   │   ├── Printer.cs
│   │   │   ├── Client.cs
│   │   │   ├── User.cs
│   │   │   ├── PrinterAssignment.cs
│   │   │   └── SystemConfiguration.cs
│   │   └── DTOs/
│   │       ├── ClientRegistrationDto.cs
│   │       ├── PrinterActionDto.cs
│   │       └── ...
│   │
│   ├── PrinterManager.Server/          # REST API Server
│   │   ├── Controllers/
│   │   │   ├── PrintersController.cs
│   │   │   ├── AssignmentsController.cs
│   │   │   ├── ClientsController.cs
│   │   │   └── ConfigurationController.cs
│   │   ├── Services/
│   │   │   ├── PrinterService.cs
│   │   │   ├── AssignmentService.cs
│   │   │   ├── ClientService.cs
│   │   │   └── PrintServerScanService.cs
│   │   ├── Data/
│   │   │   └── PrinterManagerDbContext.cs
│   │   └── Program.cs
│   │
│   ├── PrinterManager.Client/          # Windows Client
│   │   ├── Services/
│   │   │   ├── PrinterDetectionService.cs
│   │   │   ├── PrinterManagementService.cs
│   │   │   └── ServerCommunicationService.cs
│   │   ├── Worker.cs
│   │   └── Program.cs
│   │
│   └── PrinterManager.Web/             # Blazor Web UI
│       ├── Components/
│       │   ├── Pages/
│       │   │   ├── Home.razor
│       │   │   ├── Printers.razor
│       │   │   ├── Scan.razor
│       │   │   ├── Assignments.razor
│       │   │   ├── Clients.razor
│       │   │   └── Configuration.razor
│       │   └── Layout/
│       ├── Services/
│       │   └── ApiService.cs
│       └── Program.cs
└── PrinterManager.sln
```

## Installation

### Voraussetzungen
- .NET 8.0 SDK
- Windows Server (für Server-Komponente mit WMI-Zugriff)
- Windows Clients mit PowerShell

### Server installieren

1. Server kompilieren und starten — mehr ist für den ersten Start nicht nötig:
```bash
cd src/PrinterManager.Server
dotnet run --urls "http://0.0.0.0:5000"
```

Beim ersten Start legt der Server selbst an:
- `Jwt:Key` und `ClientApi:Key` — erzeugt und gespeichert in `appsettings.Local.json`
  (die Datei ist in `.gitignore`; die Werte überleben Neustarts, damit ausgestellte
  Tokens gültig bleiben)
- die SQLite-Datenbank
- den Administrator `admin` mit erzeugtem Passwort

Das Passwort steht in der Startausgabe und zusätzlich in `initial-admin-password.txt`:

```
======================================================================
 ERSTEINRICHTUNG — Administrator angelegt
 Benutzer:  admin
 Passwort:  Kf3mQp9xRt2vLw7nBz4hYs6d
 ...
======================================================================
```

Wer das Passwort selbst vorgeben will, setzt vor dem ersten Start `ADMIN_PASSWORD`
(mindestens 8 Zeichen):

```bash
ADMIN_PASSWORD='ein-sicheres-passwort' dotnet run --urls "http://0.0.0.0:5000"
```

2. Optional: `appsettings.json` anpassen

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=printermanager.db"
  }
}
```

#### Rangfolge der Konfiguration

Von schwach nach stark:

```
appsettings.json  <  appsettings.Local.json  <  Umgebungsvariablen  <  Kommandozeile
                     (Oberfläche schreibt hier)
```

Die Oberfläche schreibt nach `appsettings.Local.json`. Wer einen Wert fest vorgeben will
— etwa `Jwt__Key` aus einem Secret-Store — setzt ihn als Umgebungsvariable; er überstimmt
dann die Oberfläche. Der Reiter **Status** zeigt an, welche Einstellungen davon betroffen
sind.

### Client installieren

1. Client kompilieren:
```bash
cd src/PrinterManager.Client
dotnet publish -c Release -r win-x64 --self-contained
```

2. In `appsettings.json` die Serveradresse eintragen:
```json
{
  "ServerUrl": "http://server-ip:5000",
  "PollIntervalSeconds": 60
}
```

3. Starten — der Client trägt sich beim ersten Start selbst in den Autostart des
   angemeldeten Benutzers ein (`HKCU\...\Run`):
```powershell
C:\Path\To\PrinterManager.Client.exe
```

Ein Schlüssel ist zunächst nicht nötig: die Client-Endpunkte sind offen, bis
`ClientApi:Authentication` gesetzt wird (siehe
[Client-Authentifizierung einrichten](#client-authentifizierung-einrichten)).

> Der Client läuft bewusst **nicht** als Windows-Dienst: Druckerverbindungen sind
> benutzer- und sitzungsgebunden und wären aus dem `LocalSystem`-Kontext heraus für
> den angemeldeten Benutzer nicht sichtbar.

### Web-Anwendung installieren

1. Web-App kompilieren:
```bash
cd src/PrinterManager.Web
dotnet build -c Release
```

2. Konfiguration anpassen (`appsettings.json`):
```json
{
  "ApiUrl": "http://server-ip:5000"
}
```

3. Web-App starten:
```bash
dotnet run --urls "http://0.0.0.0:5001"
```

#### Anmeldung in der Web-Anwendung

Die Web-Anwendung meldet sich beim Server an und merkt sich das Ergebnis in einem Cookie
(`PrinterManager.Auth`). Es enthält Benutzername, Rolle und das JWT des Servers, ist
`HttpOnly` und per ASP.NET Core Data Protection verschlüsselt und signiert.

- **Gültigkeit**: Das Cookie endet zusammen mit dem JWT (Sitzungsdauer, Vorgabe 8 Stunden)
  und wird nicht verlängert. Es ist ein Sitzungscookie — nach dem Schließen des Browsers ist
  eine neue Anmeldung nötig, ein Neuladen der Seite übersteht es.
- **Abmelden** löscht das Cookie im Browser.
- **HTTPS**: Hinter HTTPS wird das Cookie als `Secure` gesetzt. Über HTTP läuft die
  Anmeldung weiterhin, das Cookie ist dann aber — wie jeder andere Verkehr — mitlesbar.
- **Data-Protection-Schlüssel**: Unter Windows legt ASP.NET Core sie im Profil des Kontos
  ab, unter dem die Web-Anwendung läuft (`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`).
  Hat das Konto kein geladenes Profil (etwa ein IIS-Anwendungspool ohne
  „Benutzerprofil laden“), werden die Schlüssel nur im Speicher gehalten — dann meldet ein
  Neustart der Web-Anwendung alle Benutzer ab.

## Verwendung

### 1. Druckerserver scannen

1. In der Web-Anwendung zu "Server scannen" navigieren
2. Servername eingeben (z.B. `PRINTSERVER01`)
3. Optional: Anmeldedaten für Remote-Zugriff
4. "Server scannen" klicken
5. Gefundene Drucker auswählen und importieren

### 2. Drucker zuweisen

**Benutzer-Zuweisung:**
1. Zu "Zuweisungen" → "Neue Zuweisung" navigieren
2. Drucker auswählen
3. Typ: "Benutzer"
4. Benutzer (UserPrincipalName) auswählen
5. Optional: Als Standarddrucker markieren

**Client-Zuweisung:**
1. Zu "Zuweisungen" → "Neue Zuweisung" navigieren
2. Drucker auswählen
3. Typ: "Client"
4. Client (Hostname) auswählen
5. Optional: Als Standarddrucker markieren

### 3. Ersatzdrucker konfigurieren

1. Zu "Drucker" navigieren
2. Bei gewünschtem Drucker auf "Bearbeiten" klicken
3. Ersatzdrucker aus Dropdown auswählen
4. "Speichern" klicken
5. Drucker als "Nicht verfügbar" markieren → Ersatzdrucker wird automatisch zugewiesen

### 4. Priorität einstellen

1. Zu "Einstellungen → Zuweisungen" navigieren
2. Priorität wählen:
   - **Benutzer-Priorität**: Benutzer-Zuweisungen haben Vorrang
   - **Client-Priorität**: Client-Zuweisungen haben Vorrang
3. "Speichern" klicken

### 5. Server konfigurieren

Unter **Einstellungen** lassen sich als Administrator alle Server-Einstellungen pflegen:

| Reiter | Inhalt | Wirkt |
|---|---|---|
| Zuweisungen | Priorität, automatische Ersatzdrucker | sofort |
| HTTPS | Port, Zertifikatsquelle, Umleitung | nach Neustart |
| Client-Zugang | Verfahren (Keines/Windows/Schlüssel), Schlüssel anzeigen und neu erzeugen | nach Neustart |
| Anmeldung | Sitzungsdauer, Issuer, Audience, Signaturschlüssel neu erzeugen | nach Neustart |
| Netzwerk | Erlaubte Browser-Herkünfte (CORS) | nach Neustart |
| Status | Aktives Zertifikat samt Fingerabdruck und Ablauf, aktiver Client-Zugang, überschriebene Einstellungen | — |

Benutzerverwaltung und LDAP liegen weiterhin unter **Sicherheit**.

Die Reiter außer „Zuweisungen" schreiben nach `appsettings.Local.json`. Diese Werte
werden beim Start gelesen — die Oberfläche weist nach dem Speichern auf den nötigen
Neustart hin.

> Der Reiter **Status** meldet, wenn eine Einstellung durch eine Umgebungsvariable oder
> `appsettings.Production.json` überschrieben wird. Ohne diesen Hinweis wundert man sich,
> warum eine Änderung in der Oberfläche folgenlos bleibt.

## API-Endpunkte

### Authentifizierung
- `POST /api/auth/login` - Benutzer-Login (JWT Token)
- `POST /api/auth/register` - Neuen Benutzer registrieren (nur Administrator)
- `GET /api/auth/users` - Alle Benutzer auflisten (nur Administrator)
- `DELETE /api/auth/users/{id}` - Benutzer löschen (nur Administrator)

### Drucker
- `GET /api/printers` - Alle Drucker auflisten
- `GET /api/printers/{id}` - Drucker Details
- `POST /api/printers` - Neuen Drucker erstellen (nur Administrator)
- `PUT /api/printers/{id}` - Drucker aktualisieren (nur Administrator)
- `DELETE /api/printers/{id}` - Drucker löschen (nur Administrator)
- `POST /api/printers/{id}/availability` - Verfügbarkeit setzen (nur Administrator)

### Zuweisungen
- `GET /api/assignments` - Alle Zuweisungen
- `GET /api/assignments/user/{userId}` - Benutzer-Zuweisungen
- `GET /api/assignments/client/{clientId}` - Client-Zuweisungen
- `POST /api/assignments` - Neue Zuweisung (nur Administrator)
- `POST /api/assignments/bulk` - Mehrere Zuweisungen (nur Administrator)
- `DELETE /api/assignments/{id}` - Zuweisung löschen (nur Administrator)
- `PUT /api/assignments/{id}/set-default` - Zuweisung als Standarddrucker ihres Benutzers bzw. Clients festlegen (nur Administrator)

### Clients
- `POST /api/clients/register` - Client registrieren (Client-Schlüssel statt JWT)
- `GET /api/clients/actions` - Drucker-Aktionen für Client (Client-Schlüssel statt JWT)
- `GET /api/clients` - Alle Clients
- `GET /api/clients/users` - Alle Benutzer
- `DELETE /api/clients/{id}` - Client löschen (nur Administrator)
- `DELETE /api/clients/users/{id}` - Benutzer löschen (nur Administrator)

### Druckerserver
- `POST /api/printserver/scan` - Server scannen (nur Administrator)

### Konfiguration
- `GET /api/configuration` - Zuweisungseinstellungen abrufen
- `PUT /api/configuration` - Zuweisungseinstellungen aktualisieren (nur Administrator)

### Server-Einstellungen (nur Administrator)
- `GET /api/settings` - Einstellungen aus appsettings.Local.json
- `GET /api/settings/status` - Laufzeitzustand: Zertifikat, aktiver Client-Zugang, überschriebene Werte
- `GET /api/settings/client-key` - Gemeinsamen Client-Schlüssel im Klartext abrufen
- `PUT /api/settings` - Einstellungen speichern (Neustart erforderlich)

> Alle Endpunkte außer `POST /api/auth/login` und den beiden Client-Endpunkten
> erfordern einen gültigen JWT-Token. Das erzwingt eine globale Fallback-Policy —
> ein neuer Controller ist damit automatisch geschützt.

## Datenbank-Schema

### Tabellen
- **Printers** - Verwaltete Drucker
- **Clients** - Registrierte Client-Computer
- **Users** - Registrierte Benutzer (Client-Registrierung)
- **ApplicationUsers** - Web-Anwendungsbenutzer (Login)
- **PrinterAssignments** - Zuweisungen (Benutzer/Client ↔ Drucker)
- **ClientPrinters** - Auf Clients installierte Drucker
- **SystemConfigurations** - Systemeinstellungen

## Workflow

### Client-Registrierung
1. Client-Service startet auf Windows-Computer
2. Erkennt aktuellen Benutzer (UserPrincipalName) und Hostname
3. Scannt bereits installierte Drucker
4. Sendet Registrierungsdaten an Server
5. Server erstellt/aktualisiert Client und Benutzer in DB
6. Server speichert installierte Drucker

### Drucker-Zuweisung
1. Administrator weist Drucker zu (über Web-UI)
2. Client-Service fragt regelmäßig Server nach Aktionen
3. Server berechnet basierend auf Priorität die Zuweisungen
4. Server sendet Installationskommandos an Client
5. Client installiert/entfernt Drucker via PowerShell
6. Client setzt ggf. Standarddrucker

### Ersatzdrucker-Logik
1. Administrator markiert Drucker als "nicht verfügbar"
2. Beim nächsten Poll löst der Server die Zuweisung auf den ersten verfügbaren
   Drucker der Ersatzdrucker-Kette auf — die gespeicherten Zuweisungen bleiben unverändert
3. Clients installieren den Ersatzdrucker und entfernen den Originaldrucker
4. Beim Reaktivieren greift automatisch wieder der Originaldrucker

> Die Auflösung passiert zur Laufzeit, es werden keine zusätzlichen Zuweisungen in der
> Datenbank angelegt. Zyklen in der Ersatzdrucker-Kette werden beim Speichern abgelehnt.
> Die Auflösung lässt sich über `AutoAssignReplacementPrinters` in der Konfiguration abschalten.

## Sicherheitskonfiguration

### Erst-Anmeldung

Es gibt **kein** Standardpasswort. Beim ersten Start legt der Server `admin` mit einem
zufällig erzeugten Passwort an und gibt es aus (Konsole + `initial-admin-password.txt`).
Mit gesetztem `ADMIN_PASSWORD` wird stattdessen dieser Wert verwendet.

Nach der ersten Anmeldung:
1. Passwort unter **Sicherheit → Benutzerverwaltung** ändern
2. `initial-admin-password.txt` löschen

Der letzte verbleibende Administrator kann weder gelöscht noch herabgestuft werden —
eine Aussperrung ist damit ausgeschlossen.

### Client-Authentifizierung einrichten

`POST /api/clients/register` und `GET /api/clients/actions` sind nach der Installation
**offen**, damit die Clients ohne Vorbereitung starten. Das ist für die Inbetriebnahme
gedacht, nicht für den Dauerbetrieb.

Einzustellen unter **Einstellungen → Client-Zugang** oder direkt über
`ClientApi:Authentication`:

| Wert | Bedeutung |
|---|---|
| `None` (Vorgabe) | Keine Prüfung. Jeder im Netz kann sich als beliebiger Benutzer ausgeben. |
| `Windows` | Kerberos/NTLM. **Empfohlen in einer Domäne.** |
| `ApiKey` | Gemeinsamer Schlüssel im Header `X-Client-Key`. |

#### Windows-Authentifizierung (empfohlen)

Auf dem Server eintragen und neu starten:

```json
{ "ClientApi": { "Authentication": "Windows" } }
```

Am Client ist **nichts** zu konfigurieren: er beantwortet die Negotiate-Aufforderung
automatisch mit dem Kerberos-Ticket des angemeldeten Benutzers.

Der entscheidende Gewinn liegt nicht in der Zugangssperre, sondern in der Identität: der
Server übernimmt den Benutzer aus dem Kerberos-Ticket und ignoriert die Angabe aus dem
Request. Ohne das kann jeder, der die Endpunkte erreicht, fremde Druckerzuweisungen
abfragen — auch mit gemeinsamem Schlüssel, denn der steht im Klartext beim Client und ist
für jeden angemeldeten Benutzer lesbar.

Voraussetzungen:
- Server und Clients sind in derselben Domäne (oder es besteht eine Vertrauensstellung)
- Ein SPN zeigt auf das Dienstkonto des Servers, z. B.
  `setspn -S HTTP/printermanager.firma.de DOMAENE\SvcPrinterManager`
- Läuft der Server nicht unter Windows, wird eine Keytab-Datei benötigt

> **Verbleibende Einschränkung**: Kerberos weist den *Benutzer* aus, nicht den Rechner —
> der Client läuft im Benutzerkontext. Der gemeldete `Hostname` bleibt damit eine Angabe
> des Clients. Benutzer-Zuweisungen sind fälschungssicher, Client-Zuweisungen nicht.

#### Gemeinsamer Schlüssel (ohne Domäne)

1. Schlüssel aus `appsettings.Local.json` (`ClientApi:Key`) bei jedem Client eintragen:
   ```json
   { "ClientApiKey": "der-erzeugte-schluessel" }
   ```
2. Auf dem Server aktivieren und neu starten:
   ```json
   { "ClientApi": { "Authentication": "ApiKey" } }
   ```

Greift nur, wenn auch ein Schlüssel hinterlegt ist — ein Tippfehler kann also nicht alle
Clients aussperren. Der Schlüssel schützt den Zugang, **nicht** die Identität: siehe oben.

### HTTPS

Am bequemsten über die Oberfläche unter **Einstellungen → HTTPS**. Die folgenden Werte
lassen sich alternativ direkt in `appsettings.Local.json` oder
`appsettings.Production.json` setzen.

Der Server bringt einen HTTPS-Endpunkt auf Port 5443 mit (`Https:Port`). Das Zertifikat
wird in dieser Reihenfolge gesucht:

1. **Windows-Zertifikatspeicher** — `Https:CertificateThumbprint` oder
   `Https:CertificateSubject`. Der übliche Weg in einer Domäne: das Zertifikat kommt per
   AD-CS-Autoenrollment, es ist nichts zu verteilen.
2. **PFX-Datei** — `Https:CertificatePath` und `Https:CertificatePassword`
3. **Selbst signiert** — wird beim ersten Start erzeugt und als
   `printermanager-selfsigned.pfx` abgelegt

Mit dem selbst signierten Zertifikat stufen Browser und Clients die Verbindung als nicht
vertrauenswürdig ein. Für den Testbetrieb lässt sich der Fingerabdruck festnageln, statt
die Prüfung abzuschalten — bei Client und Web-Anwendung:

```json
{ "ServerCertificateThumbprint": "AB12CD34..." }
```

Der Fingerabdruck steht in der Startausgabe des Servers.

Sobald ein vertrauenswürdiges Zertifikat eingerichtet ist, HTTP-Zugriffe umleiten:

```json
{ "Https": { "RedirectToHttps": true } }
```

Das aktiviert zugleich HSTS. Vorher nicht einschalten — sonst laufen die Clients in
Zertifikatsfehler statt in eine funktionierende Verbindung.

Abschalten lässt sich HTTPS mit `"Https": { "Enabled": false }`.

### JWT-Konfiguration

`Jwt:Key` wird beim ersten Start erzeugt (64 zufällige Bytes) und in
`appsettings.Local.json` abgelegt. Eingreifen muss man nur, wenn der Schlüssel
woanders herkommen soll — etwa aus einem Secret-Store oder weil mehrere
Serverinstanzen dieselben Tokens akzeptieren sollen:

```json
{
  "Jwt": {
    "Key": "IHR_GEHEIMER_SCHLÜSSEL_MINDESTENS_32_ZEICHEN_LANG",
    "Issuer": "PrinterManager",
    "Audience": "PrinterManager"
  }
}
```

Ein konfigurierter Wert hat Vorrang vor dem erzeugten. Wird der Schlüssel gewechselt,
müssen sich alle Benutzer neu anmelden.

**Wichtig bei mehreren Instanzen**: `appsettings.Local.json` wird pro Instanz erzeugt.
Hinter einem Load Balancer muss `Jwt:Key` deshalb zentral vorgegeben werden.

### LDAP-Konfiguration

LDAP wird **in der Oberfläche** unter „Sicherheit → LDAP-Einstellungen" gepflegt und in
der Datenbank gespeichert — nicht in `appsettings.json`.

| Feld | Beispiel |
|---|---|
| Server | `ldap.ihrefirma.de` |
| Port | `636` (LDAPS) |
| Base-DN | `dc=ihrefirma,dc=de` |
| User-DN-Vorlage | `uid={0},ou=users,dc=ihrefirma,dc=de` |

Auf Port 636 wird LDAPS verwendet, sonst StartTLS versucht; gelingt das nicht, wird
gewarnt und unverschlüsselt weitergemacht. **Port 636 verwenden**, sonst gehen die
Zugangsdaten im Klartext über das Netz.

Bei LDAP-Authentifizierung werden Benutzer automatisch angelegt. Schlägt die
LDAP-Anmeldung fehl, versucht der Server anschließend die lokale Anmeldung — eine
fehlerhafte LDAP-Konfiguration sperrt damit nicht den lokalen Administrator aus.

### Rollenbasierte Zugriffskontrolle

Zwei Benutzerrollen verfügbar:

1. **Administrator**
   - Vollzugriff auf alle Funktionen
   - Benutzerverwaltung
   - Systemkonfiguration
   - Drucker- und Zuweisungsverwaltung

2. **Benutzer**
   - Lesezugriff auf Drucker und Zuweisungen
   - Kein Zugriff auf Benutzerverwaltung
   - Keine Systemkonfiguration

### API-Authentifizierung

Alle API-Endpunkte erfordern einen gültigen JWT-Token. Ausgenommen sind nur
`POST /api/auth/login` sowie die beiden Client-Endpunkte, die stattdessen den
gemeinsamen Client-Schlüssel verwenden.

```bash
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" https://server:5443/api/printers
```

### Client-Authentifizierung

Der Client-Dienst besitzt kein JWT. Er authentifiziert sich mit dem gemeinsamen
Schlüssel aus `ClientApi:Key`:

```bash
curl -H "X-Client-Key: IHR_CLIENT_SCHLUESSEL" \
     "https://server:5443/api/clients/actions?hostname=PC01&userPrincipalName=user@firma.de"
```

Das gilt für `ClientApi:Authentication: "ApiKey"`. Im empfohlenen Modus `"Windows"`
läuft die Anmeldung über Kerberos und es ist kein Schlüssel im Spiel — siehe
[Client-Authentifizierung einrichten](#client-authentifizierung-einrichten).

### Swagger/OpenAPI

Swagger UI verfügbar unter: `https://server:5443/swagger`

JWT-Token im Swagger UI verwenden:
1. Klicken Sie auf "Authorize"
2. Geben Sie ein: `Bearer YOUR_JWT_TOKEN`
3. Klicken Sie auf "Authorize"

## Sicherheitshinweise

### Produktivbetrieb vorbereiten

Nach der Inbetriebnahme in dieser Reihenfolge abarbeiten:

1. 🔒 **Admin-Passwort ändern** und `initial-admin-password.txt` löschen
2. 🔒 **Vertrauenswürdiges Zertifikat** einrichten und `Https:RedirectToHttps`
   aktivieren (siehe [HTTPS](#https)) — ohne TLS sind alle weiteren Maßnahmen Kosmetik
3. 🔒 **Client-Authentifizierung** auf `Windows` stellen (siehe
   [Client-Authentifizierung einrichten](#client-authentifizierung-einrichten))
4. 🔒 **`appsettings.Local.json` schützen**: enthält JWT- und Client-Schlüssel,
   Dateirechte auf das Dienstkonto beschränken
5. 🔒 **Firewall-Regeln**: nur notwendige Ports öffnen; HTTP 5000 schließen, sobald
   alle Beteiligten auf HTTPS umgestellt sind
- 🔒 **Client-Kommunikation**: Client-API sollte nur intern erreichbar sein
- 🔒 **Datenbankzugriff**: SQLite-Datei mit Dateisystemberechtigungen schützen
- 🔒 **LDAP-Verbindung**: LDAPS (Port 636) verwenden; auf Port 389 wird StartTLS versucht
  und bei Misserfolg gewarnt
- 🔒 **Regelmäßige Updates**: .NET und Abhängigkeiten aktuell halten

## Bekannte Einschränkungen

- **Kein Migrationsmodell**: Das Schema wird mit `EnsureCreated()` angelegt. Änderungen
  am Datenmodell werden auf bestehenden Datenbanken **nicht** nachgezogen — für
  weitere Schemaänderungen sollte auf EF-Core-Migrationen umgestellt werden
  (`dotnet ef migrations add …` + `Database.Migrate()`).
- **Keine automatisierten Tests**: Die Lösung enthält kein Testprojekt. Besonders
  lohnend wären Tests für die Zuweisungs-Priorität, die Ersatzdrucker-Auflösung und
  das Escaping in `PrinterManagementService`/`LdapService`.
- **Kein Brute-Force-Schutz**: `POST /api/auth/login` ist nicht ratenbegrenzt.
  In exponierten Umgebungen empfiehlt sich Rate Limiting oder eine Sperre nach
  mehreren Fehlversuchen.
- **Geheimnisse im Klartext**: JWT-Schlüssel, Client-Schlüssel und ein etwaiges
  Zertifikatspasswort liegen unverschlüsselt in `appsettings.Local.json`. Die Datei
  sollte nur für das Dienstkonto lesbar sein.
- **Rollenänderungen wirken verzögert**: JWTs lassen sich nicht widerrufen; eine entzogene
  Administratorrolle greift erst nach Ablauf der Sitzungsdauer (einstellbar unter
  Einstellungen → Anmeldung, Vorgabe 8 Stunden). Dasselbe gilt für das Anmelde-Cookie der
  Web-Anwendung: Abmelden löscht es im Browser, eine vorher abgegriffene Kopie bliebe bis
  zum Ablauf gültig.
- **Neuer JWT-Schlüssel erfordert Neuanmeldung**: Wird `Jwt:Key` gewechselt, lehnt der
  Server die Tokens in bestehenden Cookies ab. Die Seiten zeigen dann Ladefehler, bis sich
  der Benutzer ab- und wieder anmeldet.
- **Einstellungsänderungen brauchen einen Neustart**: alles außerhalb der
  Zuweisungseinstellungen und LDAP wird beim Start gelesen. Die Oberfläche kann den
  Server nicht selbst neu starten.

## Lizenz

Dieses Projekt dient als Beispielimplementierung. Bitte passen Sie es an Ihre Anforderungen an.

## Support

Bei Fragen oder Problemen erstellen Sie bitte ein Issue im Repository.
