using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class UserConfiguration : SoftDeletableEntityConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);

        builder.Property(static u => u.Name)
            .HasMaxLength(UserValidationConstraints.MaxNameLength)
            .IsUnicode();

        builder.Property(static u => u.Email)
            .HasMaxLength(UserValidationConstraints.MaxEmailLength)
            .IsUnicode()
            .IsRequired();

        builder.Property(static u => u.UserName)
            .HasMaxLength(UserValidationConstraints.MaxUserNameLength)
            .IsUnicode()
            .IsRequired();

        builder.Property(static u => u.ProfilePicture)
            .HasMaxLength(500)
            .IsUnicode(false); // For URLs

        builder.Property(static u => u.LastLoginAt).IsRequired(false);
    }
}