# Windows Printer Manager

Ein umfassendes Druckerverwaltungssystem für Windows-Umgebungen, bestehend aus einem Management-Server, einem Client-Modul und einer Web-Anwendung.

## Übersicht

Der Windows Printer Manager ermöglicht die zentrale Verwaltung von Netzwerkdruckern über SMB-Freigaben. Das System besteht aus drei Hauptkomponenten:

1. **Management Server** - REST API Backend (ASP.NET Core)
2. **Client Module** - Autostart-Anwendung im Benutzerkontext für automatische Druckerinstallation
3. **Web Application** - Blazor Server UI für Verwaltung
<img width="1909" height="495" alt="grafik" src="https://github.com/user-attachments/assets/42c5f3a0-2820-4193-b557-a75516344973" />

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
- ✅ Rollenbasierte Zugriffskontrolle (Administrator, Benutzer)
- ✅ Client-Endpunkte über gemeinsamen Schlüssel (`X-Client-Key`) abgesichert
- ✅ Optionale LDAP/Active Directory-Integration (LDAPS bzw. StartTLS)
- ✅ HTTPS/SSL-Verschlüsselung
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

1. Server kompilieren:
```bash
cd src/PrinterManager.Server
dotnet build -c Release
```

2. Konfiguration anpassen (`appsettings.json`):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=printermanager.db"
  },
  "Jwt": {
    "Key": "MINDESTENS_32_ZEICHEN_LANGER_GEHEIMER_SCHLUESSEL"
  },
  "Cors": {
    "AllowedOrigins": [ "https://printermanager.meinefirma.de" ]
  },
  "ClientApi": {
    "Key": "zufaelliger-schluessel-fuer-die-clients"
  }
}
```

`Jwt:Key` ist Pflicht (min. 32 Zeichen) — ohne ihn startet der Server nicht.
Ist `ClientApi:Key` leer, sind die Client-Endpunkte unauthentifiziert erreichbar;
der Server warnt dann beim Start.

3. Server starten (Admin-Passwort nur beim allerersten Start nötig):
```bash
ADMIN_PASSWORD='ein-sicheres-passwort' dotnet run --urls "http://0.0.0.0:5000"
```

### Client installieren

1. Client kompilieren:
```bash
cd src/PrinterManager.Client
dotnet publish -c Release -r win-x64 --self-contained
```

2. Konfiguration anpassen (`appsettings.json`):
```json
{
  "ServerUrl": "http://server-ip:5000",
  "ClientApiKey": "derselbe-wert-wie-ClientApi:Key-auf-dem-Server",
  "PollIntervalSeconds": 60
}
```

3. Starten — der Client trägt sich beim ersten Start selbst in den Autostart des
   angemeldeten Benutzers ein (`HKCU\...\Run`):
```powershell
C:\Path\To\PrinterManager.Client.exe
```

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

1. Zu "Konfiguration" navigieren
2. Priorität wählen:
   - **Benutzer-Priorität**: Benutzer-Zuweisungen haben Vorrang
   - **Client-Priorität**: Client-Zuweisungen haben Vorrang
3. "Konfiguration speichern" klicken

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
- `GET /api/configuration` - Konfiguration abrufen
- `PUT /api/configuration` - Konfiguration aktualisieren (nur Administrator)

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

Es gibt **kein** Standardpasswort. Beim ersten Start legt der Server den Benutzer `admin`
mit dem Passwort aus der Umgebungsvariable `ADMIN_PASSWORD` an (mindestens 8 Zeichen).
Ist sie nicht gesetzt und existiert noch kein Administrator, bricht der Start mit einer
Fehlermeldung ab.

```bash
export ADMIN_PASSWORD='ein-sicheres-passwort'
dotnet run
```

Der letzte verbleibende Administrator kann weder gelöscht noch herabgestuft werden —
damit ist eine Aussperrung ausgeschlossen.

### JWT-Konfiguration

In `appsettings.json` des Servers:

```json
{
  "Jwt": {
    "Key": "IHR_GEHEIMER_SCHLÜSSEL_MINDESTENS_32_ZEICHEN_LANG",
    "Issuer": "PrinterManager",
    "Audience": "PrinterManager"
  }
}
```

**⚠️ WICHTIG**: Ändern Sie den JWT-Key in Produktivumgebungen!

### LDAP-Konfiguration

Optionale LDAP/Active Directory-Integration in `appsettings.json`:

```json
{
  "Ldap": {
    "Enabled": true,
    "Server": "ldap.ihrefirma.de",
    "Port": 389,
    "BaseDn": "dc=ihrefirma,dc=de",
    "UserDnTemplate": "uid={0},ou=users,dc=ihrefirma,dc=de"
  }
}
```

**Hinweis**: Bei LDAP-Authentifizierung werden Benutzer automatisch im System angelegt.

### HTTPS/SSL

Der Server läuft standardmäßig auf:
- **HTTP**: Port 5000
- **HTTPS**: Port 5443

Für Produktivumgebungen:

1. Eigenes SSL-Zertifikat erstellen:
```bash
dotnet dev-certs https --export-path ./certificate.pfx --password IhrPasswort
```

2. Konfiguration in `appsettings.json`:
```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:5443",
        "Certificate": {
          "Path": "./certificate.pfx",
          "Password": "IhrPasswort"
        }
      }
    }
  }
}
```

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

Ist `ClientApi:Key` nicht gesetzt, bleiben diese Endpunkte offen — das ist nur als
Übergang für bestehende Installationen gedacht und sollte in Produktivumgebungen
nicht so bleiben.

### Swagger/OpenAPI

Swagger UI verfügbar unter: `https://server:5443/swagger`

JWT-Token im Swagger UI verwenden:
1. Klicken Sie auf "Authorize"
2. Geben Sie ein: `Bearer YOUR_JWT_TOKEN`
3. Klicken Sie auf "Authorize"

## Sicherheitshinweise

- 🔒 **HTTPS verwenden**: In Produktivumgebungen nur HTTPS aktivieren
- 🔒 **JWT-Key setzen**: Pflichtfeld, mindestens 32 Zeichen, pro Umgebung unterschiedlich
- 🔒 **Client-Schlüssel setzen**: `ClientApi:Key` auf dem Server, `ClientApiKey` beim Client
- 🔒 **Admin-Passwort**: über `ADMIN_PASSWORD` beim ersten Start vergeben
- 🔒 **CORS einschränken**: `Cors:AllowedOrigins` in Produktivumgebungen befüllen
- 🔒 **Firewall-Regeln**: Nur notwendige Ports öffnen
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
- **Zertifikatspasswort im Klartext**: `SslConfiguration.CertificatePassword` liegt
  unverschlüsselt in der SQLite-Datei.
- **Rollenänderungen wirken verzögert**: JWTs sind 8 Stunden gültig und können nicht
  widerrufen werden; eine entzogene Administratorrolle greift erst nach Ablauf.

## Lizenz

Dieses Projekt dient als Beispielimplementierung. Bitte passen Sie es an Ihre Anforderungen an.

## Support

Bei Fragen oder Problemen erstellen Sie bitte ein Issue im Repository.
