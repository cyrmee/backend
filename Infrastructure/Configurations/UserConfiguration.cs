using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(static u => u.ProfilePicture)
            .HasMaxLength(500)
            .IsUnicode(false); // For URLs

        builder.Property(static u => u.LastLoginAt).IsRequired(false);
        builder.Property(static e => e.CreatedAt).IsRequired();
        builder.Property(static e => e.UpdatedAt).IsRequired();

        builder.Property(static u => u.CreatedBy)
            .HasMaxLength(100);

        builder.Property(static u => u.UpdatedBy)
            .HasMaxLength(100);

        // Configure soft delete
        builder.Property(static u => u.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(static u => u.DeletedAt).IsRequired(false);

        builder.Property(static u => u.DeletedBy)
            .HasMaxLength(100);

        // Configure indexes
        builder.HasIndex(static u => u.Email);
        builder.HasIndex(static u => u.CreatedAt);
        builder.HasIndex(static u => new { u.IsDeleted, u.CreatedAt });

        // Configure query filter for soft delete
        builder.HasQueryFilter(static u => !u.IsDeleted);

        // Configure relationships
        builder.HasOne(static u => u.AppSettings)
            .WithOne(static a => a.User)
            .HasForeignKey<AppSettings>(static a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
