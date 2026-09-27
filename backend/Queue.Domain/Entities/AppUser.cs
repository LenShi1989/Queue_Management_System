namespace Queue.Domain.Entities;

public class AppUser
{
    public long Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    /// <summary>PBKDF2-SHA256（100k iterations）雜湊，格式：iterations.salt.hash</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>綁定櫃台（Counter 角色使用，寫入 JWT claim）。</summary>
    public long? CounterId { get; set; }

    public QueueCounter? Counter { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastLoginAt { get; set; }

    public string? LastLoginIp { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTimeOffset? LockoutEndAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
}
