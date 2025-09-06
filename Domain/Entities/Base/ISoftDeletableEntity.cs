namespace Domain.Entities.Base;

public interface ISoftDeletableEntity : IAuditableEntity
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}