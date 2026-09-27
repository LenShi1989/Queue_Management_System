namespace Queue.Domain.Entities;

public class AppRole
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
}

public class AppUserRole
{
    public long UserId { get; set; }

    public AppUser? User { get; set; }

    public long RoleId { get; set; }

    public AppRole? Role { get; set; }
}
