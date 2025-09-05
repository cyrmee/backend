using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class AppSettingsConfiguration : BaseEntityConfiguration<AppSettings>
{
    public override void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        base.Configure(builder);

        // Configure specific properties
        builder.Property(static a => a.ThemePreference)
            .HasMaxLength(50)
            .HasDefaultValue(ThemePreference.System);

        builder.Property(static a => a.Onboarded)
            .HasDefaultValue(false);

        builder.Property(static a => a.UserId)
            .HasMaxLength(450)
            .IsRequired();

        // Configure indexes
        builder.HasIndex(static a => a.UserId).IsUnique();
        builder.HasIndex(static a => a.ThemePreference);
        builder.HasIndex(static a => new { a.Onboarded, a.CreatedAt });

        // Configure relationships
        builder.HasOne(static a => a.User)
            .WithOne(static u => u.AppSettings)
            .HasForeignKey<AppSettings>(static a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure query filter for soft delete
        builder.HasQueryFilter(static a => !a.IsDeleted);
    }
}
