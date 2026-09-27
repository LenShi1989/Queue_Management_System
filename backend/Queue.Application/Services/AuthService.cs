using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Queue.Application.Abstractions;
using Queue.Application.Common;
using Queue.Application.Dtos;
using Queue.Domain.Common;
using Queue.Domain.Entities;
using Queue.Domain.Enums;
using Queue.Domain.Exceptions;

namespace Queue.Application.Services;

/// <summary>
/// 認證 / RBAC（§19 權限控制、§20 Authentication）
/// </summary>
public class AuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IQueueDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly JwtTokenFactory _tokenFactory;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IQueueDbContext db,
        IClock clock,
        ICurrentUser currentUser,
        JwtTokenFactory tokenFactory,
        ILogger<AuthService> logger)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _tokenFactory = tokenFactory;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var userName = request.UserName.Trim();

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Counter)
            .FirstOrDefaultAsync(u => u.UserName == userName, ct);

        if (user is null)
        {
            _logger.LogWarning("LoginFailed User={UserName} Reason=NotFound Ip={Ip}", userName, _currentUser.IpAddress);
            throw new DomainException("LOGIN_FAILED", "帳號或密碼錯誤");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("LoginFailed User={UserName} Reason=Inactive Ip={Ip}", userName, _currentUser.IpAddress);
            throw new DomainException("USER_INACTIVE", "帳號已被停用");
        }

        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > _clock.UtcNow)
        {
            var remaining = Math.Ceiling((user.LockoutEndAt.Value - _clock.UtcNow).TotalMinutes);
            _logger.LogWarning("LoginFailed User={UserName} Reason=Lockedout", userName);
            throw new DomainException("ACCOUNT_LOCKED", $"帳號已鎖定，請於 {remaining} 分鐘後再試");
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount += 1;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEndAt = _clock.UtcNow.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                _logger.LogWarning("LoginFailed User={UserName} Reason=LockoutTriggered Ip={Ip}", userName, _currentUser.IpAddress);
            }
            else
            {
                _logger.LogWarning("LoginFailed User={UserName} Reason=BadPassword Ip={Ip}", userName, _currentUser.IpAddress);
            }

            user.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new DomainException("LOGIN_FAILED", "帳號或密碼錯誤");
        }

        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = _clock.UtcNow;
        user.LastLoginIp = _currentUser.IpAddress;
        user.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        var roles = user.UserRoles.Select(ur => ur.Role!.Code).ToList();
        var counterId = request.CounterId ?? user.CounterId;

        _logger.LogInformation("LoginSuccess User={UserName} Roles={Roles} Ip={Ip}", user.UserName, string.Join(',', roles), _currentUser.IpAddress);

        return _tokenFactory.Create(user, roles, counterId, user.Counter?.Code);
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (!long.TryParse(_currentUser.UserId, out var userId))
        {
            throw new DomainException("UNAUTHORIZED", "尚未登入");
        }

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Counter)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainException("USER_NOT_FOUND", "找不到使用者");

        return MapUser(user);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (!long.TryParse(_currentUser.UserId, out var userId))
        {
            throw new DomainException("UNAUTHORIZED", "尚未登入");
        }

        var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);

        if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new DomainException("PASSWORD_INCORRECT", "目前密碼不正確");
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            throw new DomainException("PASSWORD_SAME", "新密碼不可與目前密碼相同");
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("PasswordChanged User={UserName}", user.UserName);
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Counter)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);

        return users.Select(MapUser).ToList();
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.UserName == request.UserName, ct))
        {
            throw new DomainException("USER_EXISTS", "帳號已存在");
        }

        await EnsureRolesExistAsync(request.Roles, ct);

        var now = _clock.UtcNow;
        var user = new AppUser
        {
            UserName = request.UserName.Trim(),
            DisplayName = request.DisplayName,
            Email = request.Email,
            CounterId = request.CounterId,
            PasswordHash = PasswordHasher.Hash(request.Password),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var roleIds = await _db.Roles.Where(r => request.Roles.Contains(r.Code)).Select(r => r.Id).ToListAsync(ct);
        foreach (var roleId in roleIds)
        {
            _db.UserRoles.Add(new AppUserRole { UserId = user.Id, RoleId = roleId });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("UserCreated User={UserName} Roles={Roles}", user.UserName, string.Join(',', request.Roles));
        return await GetUserAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateUserAsync(long id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("USER_NOT_FOUND", "找不到使用者");

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName;
        }

        if (request.Email is not null)
        {
            user.Email = request.Email;
        }

        if (request.CounterId.HasValue)
        {
            user.CounterId = request.CounterId;
        }

        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
        }

        if (request.Roles is not null)
        {
            await EnsureRolesExistAsync(request.Roles, ct);
            _db.UserRoles.RemoveRange(user.UserRoles);
            var roleIds = await _db.Roles.Where(r => request.Roles.Contains(r.Code)).Select(r => r.Id).ToListAsync(ct);
            foreach (var roleId in roleIds)
            {
                _db.UserRoles.Add(new AppUserRole { UserId = user.Id, RoleId = roleId });
            }
        }

        user.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetUserAsync(user.Id, ct);
    }

    public async Task ResetPasswordAsync(long id, string newPassword, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("USER_NOT_FOUND", "找不到使用者");

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("PasswordReset User={UserName} By={Operator}", user.UserName, _currentUser.UserName);
    }

    public async Task DeleteUserAsync(long id, CancellationToken ct = default)
    {
        if (long.TryParse(_currentUser.UserId, out var selfId) && selfId == id)
        {
            throw new DomainException("CANNOT_DELETE_SELF", "不可刪除自己的帳號");
        }

        var user = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("USER_NOT_FOUND", "找不到使用者");

        _db.UserRoles.RemoveRange(user.UserRoles);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("UserDeleted User={UserName} By={Operator}", user.UserName, _currentUser.UserName);
    }

    private async Task<UserDto> GetUserAsync(long id, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Counter)
            .FirstAsync(u => u.Id == id, ct);

        return MapUser(user);
    }

    private async Task EnsureRolesExistAsync(IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        if (roles.Count == 0)
        {
            throw new DomainException("ROLE_REQUIRED", "請至少指定一個角色");
        }

        var existing = await _db.Roles.AsNoTracking().Select(r => r.Code).ToListAsync(ct);
        var missing = roles.Where(r => !existing.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
        {
            throw new DomainException("ROLE_NOT_FOUND", $"無效的角色：{string.Join(',', missing)}");
        }
    }

    private static UserDto MapUser(AppUser user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        DisplayName = user.DisplayName,
        Email = user.Email,
        CounterId = user.CounterId,
        CounterNo = user.Counter?.Code,
        IsActive = user.IsActive,
        Roles = user.UserRoles.Select(ur => ur.Role!.Code).OrderBy(r => r).ToList(),
        LastLoginAt = user.LastLoginAt,
        CreatedAt = user.CreatedAt
    };
}
