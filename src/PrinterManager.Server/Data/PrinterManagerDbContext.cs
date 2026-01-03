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

        // Seed default admin user (password: admin)
        modelBuilder.Entity<ApplicationUser>().HasData(
            new ApplicationUser
            {
                Id = 1,
                Username = "admin",
                PasswordHash = "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=", // SHA256 of "admin"
                IsActive = true,
                IsLdapUser = false,
                Role = UserRole.Administrator,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Seed default LDAP configuration
        modelBuilder.Entity<LdapConfiguration>().HasData(
            new LdapConfiguration
            {
                Id = 1,
                Enabled = false,
                Server = "ldap.example.com",
                Port = 389,
                BaseDn = "dc=example,dc=com",
                UserDnTemplate = "uid={0},ou=users,dc=example,dc=com"
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
