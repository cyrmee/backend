using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public abstract class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(static e => e.Id);
        builder.Property(static e => e.CreatedAt).IsRequired();
        builder.Property(static e => e.UpdatedAt).IsRequired();
        builder.Property(static e => e.CreatedBy).HasMaxLength(100);
        builder.Property(static e => e.UpdatedBy).HasMaxLength(100);
    }
}