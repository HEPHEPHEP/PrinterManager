using Microsoft.EntityFrameworkCore;
using PrinterManager.Shared.Models;

namespace PrinterManager.Server.Data;

public class PrinterManagerDbContext : DbContext
{
    public PrinterManagerDbContext(DbContextOptions<PrinterManagerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PrinterAssignment> PrinterAssignments => Set<PrinterAssignment>();
    public DbSet<ClientPrinter> ClientPrinters => Set<ClientPrinter>();
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<LdapConfiguration> LdapConfigurations => Set<LdapConfiguration>();
    public DbSet<SslConfiguration> SslConfigurations => Set<SslConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Printer configuration
        modelBuilder.Entity<Printer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PrinterId).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SharePath).IsRequired().HasMaxLength(500);

            entity.HasOne(e => e.ReplacementPrinter)
                .WithMany()
                .HasForeignKey(e => e.ReplacementPrinterId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Client configuration
        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Hostname).IsUnique();
            entity.Property(e => e.Hostname).IsRequired().HasMaxLength(200);
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserPrincipalName).IsUnique();
            entity.Property(e => e.UserPrincipalName).IsRequired().HasMaxLength(200);
        });

        // PrinterAssignment configuration
        modelBuilder.Entity<PrinterAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Printer)
                .WithMany(p => p.Assignments)
                .HasForeignKey(e => e.PrinterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.PrinterAssignments)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Client)
                .WithMany(c => c.PrinterAssignments)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ensure either UserId or ClientId is set, but not both
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_PrinterAssignment_UserOrClient",
                "(UserId IS NOT NULL AND ClientId IS NULL) OR (UserId IS NULL AND ClientId IS NOT NULL)"
            ));
        });

        // ClientPrinter configuration
        modelBuilder.Entity<ClientPrinter>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Client)
                .WithMany(c => c.InstalledPrinters)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SystemConfiguration
        modelBuilder.Entity<SystemConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // ApplicationUser configuration
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
        });

        // Seed default configuration
        modelBuilder.Entity<SystemConfiguration>().HasData(
            new SystemConfiguration
            {
                Id = 1,
                AssignmentPriority = AssignmentPriority.UserPriority,
                AutoAssignReplacementPrinters = true
            }
        );

        // LdapConfiguration
        modelBuilder.Entity<LdapConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // SslConfiguration
        modelBuilder.Entity<SslConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // Admin-Benutzer wird NICHT mehr per Seed mit festem Hash erstellt.
        // Stattdessen wird beim ersten Start ein Admin mit Passwort aus der
        // Umgebungsvariable ADMIN_PASSWORD erstellt (siehe Program.cs).
        // 
        // Falls die DB bereits einen alten SHA256-Admin-Hash enthält:
        // Beim nächsten Login wird der Hash automatisch auf BCrypt migriert.

        // Seed default LDAP configuration (leer — wird über Admin-UI konfiguriert)
        modelBuilder.Entity<LdapConfiguration>().HasData(
            new LdapConfiguration
            {
                Id = 1,
                Enabled = false,
                Server = "",
                Port = 389,
                BaseDn = "",
                UserDnTemplate = ""
            }
        );

        // Seed default SSL configuration
        modelBuilder.Entity<SslConfiguration>().HasData(
            new SslConfiguration
            {
                Id = 1,
                Enabled = false,
                HttpsPort = 5443
            }
        );
    }
}
