using Domain.Entities.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public abstract class SoftDeletableEntityConfiguration<T> : AuditableEntityConfiguration<T>
    where T : class, ISoftDeletableEntity
{
    public override void Configure(EntityTypeBuilder<T> builder)
    {
        base.Configure(builder);
        builder.Property(static e => e.IsDeleted).HasDefaultValue(false);
        builder.Property(static e => e.DeletedAt).IsRequired(false);
        builder.Property(static e => e.DeletedBy).HasMaxLength(100);

        builder.HasIndex(static e => new { e.IsDeleted, e.DeletedAt });

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}