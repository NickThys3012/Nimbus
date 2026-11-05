using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Entities.Base;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();
    public DbSet<LoginEvent> LoginEvents => Set<LoginEvent>();
    public DbSet<Balloon> Balloons => Set<Balloon>();
    public DbSet<Pilot> Pilots => Set<Pilot>();
    public DbSet<LaunchLocation> LaunchLocations => Set<LaunchLocation>();
    public DbSet<GoNoGoThreshold> GoNoGoThresholds => Set<GoNoGoThreshold>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<OfpSettings> OfpSettings => Set<OfpSettings>();
    public DbSet<FlightPreparation> FlightPreparations => Set<FlightPreparation>();
    public DbSet<Passenger> Passengers => Set<Passenger>();
    public DbSet<FlightPreparationImage> FlightPreparationImages => Set<FlightPreparationImage>();
    public DbSet<WindLevel> WindLevels => Set<WindLevel>();
    public DbSet<FlightPreparationShare> FlightPreparationShares => Set<FlightPreparationShare>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId);
        });

        builder.Entity<SentEmail>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Recipient).IsRequired().HasMaxLength(320);
            e.Property(s => s.Template).HasMaxLength(200);
            e.Property(s => s.ProviderMessageId).HasMaxLength(500);
            e.Property(s => s.FailureReason).HasMaxLength(2000);
            // Recent-attempts-for-a-recipient is the query the "did the reset email
            // actually go out" support question always turns into.
            e.HasIndex(s => new
            {
                s.Recipient, s.SentAt
            });
        });


        // Existing rows get the migration time as their request date; new rows set it explicitly.
        builder.Entity<ApplicationUser>()
            .Property(u => u.RequestedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(AsUtc);

        builder.Entity<LoginEvent>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.OccurredAt).HasConversion(AsUtc);
            e.Property(l => l.Email).HasMaxLength(320);
            e.Property(l => l.MaskedIp).HasMaxLength(64);
            e.Property(l => l.FailureReason).HasConversion<string>().HasMaxLength(32);
            // The admin list is always "newest N"; the audit lookup is "attempts for this email, newest first".
            e.HasIndex(l => l.OccurredAt);
            e.HasIndex(l => new { l.Email, l.OccurredAt });
        });

        builder.Entity<Balloon>(e =>
        {
            e.Property(b => b.Registration).IsRequired().HasMaxLength(20);
            e.Property(b => b.Type).IsRequired().HasMaxLength(100);
            e.Property(b => b.EnvelopeWeightKg).HasPrecision(7, 2);
            e.Property(b => b.BasketWeightKg).HasPrecision(7, 2);
            e.Property(b => b.BurnerWeightKg).HasPrecision(7, 2);
            e.Property(b => b.CylindersWeightKg).HasPrecision(7, 2);
            // Derived from the four reference weights; never persisted.
            e.Ignore(b => b.EmptyWeightKg);
            // Unique per owner, but a soft-deleted balloon frees its registration for reuse.
            e.HasIndex(b => new { b.OwnerId, b.Registration })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<Pilot>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(100);
            e.Property(p => p.WeightKg).HasPrecision(5, 2);
            e.HasIndex(p => p.OwnerId);
        });

        builder.Entity<LaunchLocation>(e =>
        {
            e.Property(l => l.Name).IsRequired().HasMaxLength(100);
            e.Property(l => l.IcaoCode).HasMaxLength(4);
            e.Property(l => l.Latitude).HasPrecision(9, 6);
            e.Property(l => l.Longitude).HasPrecision(9, 6);
            e.Property(l => l.ElevationM).HasPrecision(7, 2);
            e.Property(l => l.AirspaceNotes).HasMaxLength(2000);
            e.HasIndex(l => l.OwnerId);
        });

        builder.Entity<GoNoGoThreshold>(e =>
        {
            e.Property(t => t.WindYellowKt).HasPrecision(7, 2);
            e.Property(t => t.WindRedKt).HasPrecision(7, 2);
            e.Property(t => t.VisibilityYellowKm).HasPrecision(7, 2);
            e.Property(t => t.VisibilityRedKm).HasPrecision(7, 2);
            e.Property(t => t.CapeYellow).HasPrecision(7, 2);
            e.Property(t => t.CapeRed).HasPrecision(7, 2);
            // One row per user (unique on non-null owners); null-owner rows are the read-only global default.
            e.HasIndex(t => t.OwnerId).IsUnique();
        });

        builder.Entity<UserProfile>(e =>
        {
            e.Property(p => p.OperatorName).HasMaxLength(200);
            e.Property(p => p.PicWeightKg).HasPrecision(5, 2);
            // One profile per user.
            e.HasIndex(p => p.OwnerId).IsUnique();
        });

        builder.Entity<OfpSettings>(e =>
        {
            e.Property(s => s.EquipmentWeightPerPassengerKg).HasPrecision(5, 2);
            // One row per user.
            e.HasIndex(s => s.OwnerId).IsUnique();
        });

        ConfigureFlightPreparation(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySoftDeleteConvention(builder);
    }

    /// <summary>
    ///     Flight preparation dossier and its children (issue #31). Column naming is English
    ///     throughout (see docs/flight-preparation.md). All four child collections cascade-delete
    ///     with the parent row; <c>BalloonId</c>/<c>PilotId</c>/<c>LaunchLocationId</c> and
    ///     <c>Shares.SharedWithUserId</c> are plain ids (no FK constraint), the same convention
    ///     already used for <c>OwnerId</c>.
    /// </summary>
    private static void ConfigureFlightPreparation(ModelBuilder builder)
    {
        builder.Entity<FlightPreparation>(e =>
        {
            e.Property(f => f.Title).IsRequired().HasMaxLength(200);
            e.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);

            e.Property(f => f.WindSpeedKt).HasPrecision(5, 1);
            e.Property(f => f.VisibilityKm).HasPrecision(6, 2);
            e.Property(f => f.CapeJPerKg).HasPrecision(7, 2);
            e.Property(f => f.TemperatureC).HasPrecision(5, 2);
            e.Property(f => f.DewPointC).HasPrecision(5, 2);
            e.Property(f => f.QnhHpa).HasPrecision(6, 2);
            e.Property(f => f.Precipitation).HasMaxLength(200);
            e.Property(f => f.Metar).HasMaxLength(500);
            e.Property(f => f.Taf).HasMaxLength(1000);

            e.Property(f => f.AirspaceStructureNotes).HasMaxLength(2000);
            e.Property(f => f.AirspaceRestrictions).HasMaxLength(2000);
            e.Property(f => f.Obstacles).HasMaxLength(2000);

            e.Property(f => f.EnvelopeWeightKg).HasPrecision(7, 2);
            e.Property(f => f.LoadRemarks).HasMaxLength(2000);

            e.Property(f => f.RouteDescription).HasMaxLength(2000);
            e.Property(f => f.ClimbRateMetersPerSecond).HasPrecision(4, 2);
            e.Property(f => f.DescentRateMetersPerSecond).HasPrecision(4, 2);
            e.Property(f => f.CruiseAltitudesFtCsv).HasMaxLength(100);

            e.Property(f => f.BulletinText).HasMaxLength(4000);

            e.Property(f => f.OfpOperatorName).HasMaxLength(200);
            e.Property(f => f.OfpPicWeightKg).HasPrecision(5, 2);
            e.Property(f => f.OfpLandingLocation).HasMaxLength(200);

            e.Property(f => f.PaxBriefingVersion).HasMaxLength(50);

            e.Property(f => f.ActualLandingLocation).HasMaxLength(200);
            e.Property(f => f.FuelConsumedLiters).HasPrecision(6, 2);
            e.Property(f => f.DefectsDescription).HasMaxLength(2000);
            e.Property(f => f.OutcomeRemarks).HasMaxLength(2000);
            e.Property(f => f.KmlTrackObjectKey).HasMaxLength(1024);
            e.Property(f => f.KmlTrackContentType).HasMaxLength(100);

            // "My flights" list/paging (issue #40) and the owner-scoping convention.
            e.HasIndex(f => f.OwnerId);

            e.HasMany(f => f.Passengers)
                .WithOne(p => p.FlightPreparation)
                .HasForeignKey(p => p.FlightPreparationId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(f => f.Images)
                .WithOne(i => i.FlightPreparation)
                .HasForeignKey(i => i.FlightPreparationId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(f => f.WindLevels)
                .WithOne(w => w.FlightPreparation)
                .HasForeignKey(w => w.FlightPreparationId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(f => f.Shares)
                .WithOne(s => s.FlightPreparation)
                .HasForeignKey(s => s.FlightPreparationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Passenger>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.WeightKg).HasPrecision(5, 2);
            e.HasIndex(p => new { p.FlightPreparationId, p.Order });
        });

        builder.Entity<FlightPreparationImage>(e =>
        {
            e.Property(i => i.Section).HasConversion<string>().HasMaxLength(30);
            e.Property(i => i.ObjectKey).IsRequired().HasMaxLength(1024);
            e.Property(i => i.ContentType).IsRequired().HasMaxLength(100);
            e.HasIndex(i => new { i.FlightPreparationId, i.Section, i.Order });
        });

        builder.Entity<WindLevel>(e =>
        {
            e.Property(w => w.SpeedKt).HasPrecision(5, 1);
            e.Property(w => w.TemperatureC).HasPrecision(5, 2);
            e.HasIndex(w => new { w.FlightPreparationId, w.Order });
        });

        builder.Entity<FlightPreparationShare>(e =>
        {
            // One active share per recipient; a soft-deleted (revoked) share frees the slot for re-sharing.
            e.HasIndex(s => new { s.FlightPreparationId, s.SharedWithUserId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });
    }
    // datetime2 comes back as DateTimeKind.Unspecified; mark it UTC so JSON serialises with a "Z"
    // and clients do not read the UTC clock value as local time.
    private static readonly ValueConverter<DateTime, DateTime> AsUtc =
        new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static void ApplySoftDeleteConvention(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(entityType => entityType.ClrType.IsAssignableTo(typeof(BaseEntity)) &&
                         entityType.ClrType != typeof(BaseEntity)))
        {
            var entity = builder.Entity(entityType.ClrType);
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
            var filter = Expression.Lambda(Expression.Equal(isDeleted, Expression.Constant(false)), parameter);

            entity.HasQueryFilter(filter);
            entity.HasIndex(nameof(BaseEntity.IsDeleted))
                .HasFilter("IsDeleted = 0");
        }
    }


    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new())
    {
        foreach (var entity in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entity.State)
            {
                case EntityState.Added:
                    entity.Entity.CreatedDate = DateTime.UtcNow;
                    entity.Entity.UpdatedDate = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entity.Entity.UpdatedDate = DateTime.UtcNow;
                    break;

                case EntityState.Deleted:
                    entity.Entity.IsDeleted = true;
                    entity.State = EntityState.Modified; // Mark as modified instead of deleted
                    entity.Entity.UpdatedDate = DateTime.UtcNow;
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    break;
            }
        }
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
