using Domain.Entities.Base;
using Microsoft.AspNetCore.Identity;

namespace Domain.Entities;

public class User : IdentityUser<Guid>, ISoftDeletableEntity
{
	public string? Name { get; init; }
	public string? ProfilePicture { get; init; }
	public DateTime? LastLoginAt { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	public string? CreatedBy { get; set; }
	public string? UpdatedBy { get; set; }
	public bool IsDeleted { get; set; }
	public DateTime? DeletedAt { get; set; }
	public string? DeletedBy { get; set; }
}