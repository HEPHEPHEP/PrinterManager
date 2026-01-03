# Windows Printer Manager

Ein umfassendes Druckerverwaltungssystem für Windows-Umgebungen, bestehend aus einem Management-Server, einem Client-Modul und einer Web-Anwendung.

## Übersicht

Der Windows Printer Manager ermöglicht die zentrale Verwaltung von Netzwerkdruckern über SMB-Freigaben. Das System besteht aus drei Hauptkomponenten:

1. **Management Server** - REST API Backend (ASP.NET Core)
2. **Client Module** - Windows Service für automatische Druckerinstallation
3. **Web Application** - Blazor Server UI für Verwaltung
<img width="1909" height="495" alt="grafik" src="https://github.com/user-attachments/assets/42c5f3a0-2820-4193-b557-a75516344973" />

### Hauptfunktionen

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
- ✅ JWT-basierte Authentifizierung
- ✅ Rollenbasierte Zugriffskontrolle (Administrator, Benutzer)
- ✅ Optionale LDAP/Active Directory-Integration
- ✅ HTTPS/SSL-Verschlüsselung
- ✅ Passwort-Hashing (SHA256)
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
  }
}
```

3. Server starten:
```bash
dotnet run --urls "http://0.0.0.0:5000"
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
  "PollIntervalSeconds": 60
}
```

3. Als Windows Service installieren:
```powershell
sc.exe create "PrinterManager Client" binPath="C:\Path\To\PrinterManager.Client.exe"
sc.exe start "PrinterManager Client"
```

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
- `POST /api/printers` - Neuen Drucker erstellen
- `PUT /api/printers/{id}` - Drucker aktualisieren
- `DELETE /api/printers/{id}` - Drucker löschen
- `POST /api/printers/{id}/availability` - Verfügbarkeit setzen

### Zuweisungen
- `GET /api/assignments` - Alle Zuweisungen
- `GET /api/assignments/user/{userId}` - Benutzer-Zuweisungen
- `GET /api/assignments/client/{clientId}` - Client-Zuweisungen
- `POST /api/assignments` - Neue Zuweisung
- `DELETE /api/assignments/{id}` - Zuweisung löschen

### Clients
- `POST /api/clients/register` - Client registrieren
- `GET /api/clients/actions` - Drucker-Aktionen für Client
- `GET /api/clients` - Alle Clients
- `GET /api/clients/users` - Alle Benutzer

### Druckerserver
- `POST /api/printserver/scan` - Server scannen

### Konfiguration
- `GET /api/configuration` - Konfiguration abrufen
- `PUT /api/configuration` - Konfiguration aktualisieren

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
2. Server ermittelt alle Zuweisungen für diesen Drucker
3. Server erstellt temporäre Zuweisungen für Ersatzdrucker
4. Bei nächstem Poll installieren Clients den Ersatzdrucker
5. Beim Reaktivieren werden Original-Zuweisungen wiederhergestellt

## Sicherheitskonfiguration

### Standard-Anmeldung
- **Benutzername**: admin
- **Passwort**: admin
- **⚠️ WICHTIG**: Ändern Sie das Admin-Passwort sofort nach der ersten Anmeldung!

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

Alle API-Endpunkte (außer `/api/auth/login`) erfordern einen gültigen JWT-Token:

```bash
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" https://server:5443/api/printers
```

### Swagger/OpenAPI

Swagger UI verfügbar unter: `https://server:5443/swagger`

JWT-Token im Swagger UI verwenden:
1. Klicken Sie auf "Authorize"
2. Geben Sie ein: `Bearer YOUR_JWT_TOKEN`
3. Klicken Sie auf "Authorize"

## Sicherheitshinweise

- 🔒 **HTTPS verwenden**: In Produktivumgebungen nur HTTPS aktivieren
- 🔒 **JWT-Key ändern**: Standard-Key muss geändert werden
- 🔒 **Admin-Passwort ändern**: Sofort nach Installation
- 🔒 **Firewall-Regeln**: Nur notwendige Ports öffnen
- 🔒 **Client-Kommunikation**: Client-API sollte nur intern erreichbar sein
- 🔒 **Datenbankzugriff**: SQLite-Datei mit Dateisystemberechtigungen schützen
- 🔒 **LDAP-Verbindung**: LDAPS (Port 636) für verschlüsselte Verbindungen verwenden
- 🔒 **Regelmäßige Updates**: .NET und Abhängigkeiten aktuell halten

## Lizenz

Dieses Projekt dient als Beispielimplementierung. Bitte passen Sie es an Ihre Anforderungen an.

## Support

Bei Fragen oder Problemen erstellen Sie bitte ein Issue im Repository.
