using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Entities.Base;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();

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


        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySoftDeleteConvention(builder);
    }

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
