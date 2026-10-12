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

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySoftDeleteConvention(builder);
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
