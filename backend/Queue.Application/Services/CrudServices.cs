using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Queue.Application.Abstractions;
using Queue.Application.Common;
using Queue.Application.Dtos;
using Queue.Domain.Common;
using Queue.Domain.Entities;

namespace Queue.Application.Services;

/// <summary>
/// JWT 簽發（§20 Token：sub / name / role / counterId）
/// </summary>
public class JwtTokenFactory
{
    public const string ClaimCounterId = "counterId";
    public const string ClaimDisplayName = "displayName";
    public const string ClaimCounterNo = "counterNo";

    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenFactory(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
        SymmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
    }

    public SymmetricSecurityKey SymmetricSecurityKey { get; }

    public int ExpireMinutes => _options.ExpireMinutes;

    public LoginResponse Create(AppUser user, IReadOnlyList<string> roles, long? counterId, string? counterNo)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_options.ExpireMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimDisplayName, user.DisplayName)
        };

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        if (counterId.HasValue)
        {
            claims.Add(new Claim(ClaimCounterId, counterId.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (!string.IsNullOrWhiteSpace(counterNo))
        {
            claims.Add(new Claim(ClaimCounterNo, counterNo));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(SymmetricSecurityKey, SecurityAlgorithms.HmacSha256));

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponse
        {
            AccessToken = encoded,
            TokenType = "Bearer",
            ExpiresIn = (int)Math.Max(0, (expires - now).TotalSeconds),
            ExpiresAt = expires,
            UserId = user.Id.ToString(CultureInfo.InvariantCulture),
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            CounterId = counterId,
            CounterNo = counterNo,
            Roles = roles.ToList()
        };
    }
}

/// <summary>
/// 服務類型 CRUD（§11.4 Service API、§5.1 服務類型管理）
/// </summary>
public class QueueServiceTypeService
{
    private readonly IQueueDbContext _db;
    private readonly IClock _clock;

    public QueueServiceTypeService(IQueueDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ServiceDto>> GetAllAsync(bool includeInactive, CancellationToken ct = default)
    {
        var today = _clock.Today;

        var query = _db.Services.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .Select(s => new ServiceDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                Prefix = s.Prefix,
                NumberLength = s.NumberLength,
                Priority = s.Priority,
                EstimatedServiceMinutes = s.EstimatedServiceMinutes,
                SkipLineEnabled = s.SkipLineEnabled,
                DisplayOrder = s.DisplayOrder,
                Description = s.Description,
                IsActive = s.IsActive,
                WaitingCount = _db.Tickets.Count(t => t.QueueDate == today && t.ServiceId == s.Id && t.Status == Domain.Enums.QueueTicketStatus.Waiting),
                ServingCount = _db.Tickets.Count(t => t.QueueDate == today && t.ServiceId == s.Id && t.Status == Domain.Enums.QueueTicketStatus.Serving),
                CurrentTicketNo = _db.Tickets
                    .Where(t => t.QueueDate == today && t.ServiceId == s.Id && t.Status == Domain.Enums.QueueTicketStatus.Calling)
                    .OrderByDescending(t => t.CalledAt)
                    .Select(t => t.TicketNo)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public async Task<ServiceDto> GetAsync(long id, CancellationToken ct = default)
    {
        var dto = (await GetAllAsync(true, ct)).FirstOrDefault(s => s.Id == id);
        return dto ?? throw new Domain.Exceptions.DomainException("SERVICE_NOT_FOUND", "找不到服務類型");
    }

    public async Task<ServiceDto> CreateAsync(UpsertServiceRequest request, CancellationToken ct = default)
    {
        if (await _db.Services.AnyAsync(s => s.Code == request.Code, ct))
        {
            throw new Domain.Exceptions.DomainException("SERVICE_EXISTS", "服務代碼已存在");
        }

        if (await _db.Services.AnyAsync(s => s.Prefix == request.Prefix, ct))
        {
            throw new Domain.Exceptions.DomainException("PREFIX_EXISTS", $"前綴 {request.Prefix} 已被其他服務使用");
        }

        var now = _clock.UtcNow;
        var entity = new QueueService { CreatedAt = now, UpdatedAt = now };
        Apply(entity, request);
        _db.Services.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<ServiceDto> UpdateAsync(long id, UpsertServiceRequest request, CancellationToken ct = default)
    {
        var entity = await _db.Services.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new Domain.Exceptions.DomainException("SERVICE_NOT_FOUND", "找不到服務類型");

        if (await _db.Services.AnyAsync(s => s.Code == request.Code && s.Id != id, ct))
        {
            throw new Domain.Exceptions.DomainException("SERVICE_EXISTS", "服務代碼已存在");
        }

        if (await _db.Services.AnyAsync(s => s.Prefix == request.Prefix && s.Id != id, ct))
        {
            throw new Domain.Exceptions.DomainException("PREFIX_EXISTS", $"前綴 {request.Prefix} 已被其他服務使用");
        }

        Apply(entity, request);
        entity.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await _db.Services.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new Domain.Exceptions.DomainException("SERVICE_NOT_FOUND", "找不到服務類型");

        if (await _db.Tickets.AnyAsync(t => t.ServiceId == id, ct))
        {
            throw new Domain.Exceptions.DomainException("SERVICE_IN_USE", "此服務類型已有排隊資料，請改為停用");
        }

        if (await _db.Counters.AnyAsync(c => c.ServiceId == id, ct))
        {
            throw new Domain.Exceptions.DomainException("SERVICE_IN_USE", "此服務類型仍有櫃台使用中，請改為停用");
        }

        _db.Services.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(QueueService entity, UpsertServiceRequest request)
    {
        entity.Code = request.Code.Trim().ToUpperInvariant();
        entity.Name = request.Name.Trim();
        entity.Prefix = request.Prefix.Trim().ToUpperInvariant();
        entity.NumberLength = request.NumberLength;
        entity.Priority = request.Priority;
        entity.EstimatedServiceMinutes = request.EstimatedServiceMinutes;
        entity.SkipLineEnabled = request.SkipLineEnabled;
        entity.DisplayOrder = request.DisplayOrder;
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;
    }

    private static ServiceDto Map(QueueService s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Name = s.Name,
        Prefix = s.Prefix,
        NumberLength = s.NumberLength,
        Priority = s.Priority,
        EstimatedServiceMinutes = s.EstimatedServiceMinutes,
        SkipLineEnabled = s.SkipLineEnabled,
        DisplayOrder = s.DisplayOrder,
        Description = s.Description,
        IsActive = s.IsActive
    };
}

/// <summary>
/// 櫃台 CRUD（§11.5 Counter API、§5.1 櫃台管理）
/// </summary>
public class QueueCounterService
{
    private readonly IQueueDbContext _db;
    private readonly IClock _clock;

    public QueueCounterService(IQueueDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CounterDto>> GetAllAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = _db.Counters.AsNoTracking()
            .Include(c => c.Service)
            .Include(c => c.CurrentTicket)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var today = _clock.Today;
        var list = await query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).ToListAsync(ct);

        var waitingCounts = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.QueueDate == today && t.Status == Domain.Enums.QueueTicketStatus.Waiting)
            .GroupBy(t => t.ServiceId)
            .Select(g => new { ServiceId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var waitingByService = waitingCounts.ToDictionary(x => x.ServiceId, x => x.Count);

        return list.Select(c => new CounterDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            ServiceId = c.ServiceId,
            ServiceName = c.Service?.Name,
            ServiceCode = c.Service?.Code,
            Status = c.Status.ToString(),
            CurrentTicketId = c.CurrentTicketId,
            CurrentTicketNo = c.CurrentTicket?.TicketNo,
            CurrentTicketStatus = c.CurrentTicket?.Status.ToString(),
            Weight = c.Weight,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
            WaitingCount = c.ServiceId.HasValue
                ? waitingByService.GetValueOrDefault(c.ServiceId.Value)
                : waitingByService.Values.DefaultIfEmpty(0).Sum()
        }).ToList();
    }

    public async Task<CounterDto> GetAsync(long id, CancellationToken ct = default)
    {
        var dto = (await GetAllAsync(true, ct)).FirstOrDefault(c => c.Id == id);
        return dto ?? throw new Domain.Exceptions.DomainException("COUNTER_NOT_FOUND", "找不到櫃台");
    }

    public async Task<CounterDto> CreateAsync(UpsertCounterRequest request, CancellationToken ct = default)
    {
        if (await _db.Counters.AnyAsync(c => c.Code == request.Code, ct))
        {
            throw new Domain.Exceptions.DomainException("COUNTER_EXISTS", "櫃台代碼已存在");
        }

        var now = _clock.UtcNow;
        var entity = new QueueCounter { CreatedAt = now, UpdatedAt = now, Status = Domain.Enums.QueueCounterStatus.Idle };
        await ApplyAsync(entity, request, ct);
        _db.Counters.Add(entity);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(entity.Id, ct);
    }

    public async Task<CounterDto> UpdateAsync(long id, UpsertCounterRequest request, CancellationToken ct = default)
    {
        var entity = await _db.Counters.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new Domain.Exceptions.DomainException("COUNTER_NOT_FOUND", "找不到櫃台");

        if (await _db.Counters.AnyAsync(c => c.Code == request.Code && c.Id != id, ct))
        {
            throw new Domain.Exceptions.DomainException("COUNTER_EXISTS", "櫃台代碼已存在");
        }

        await ApplyAsync(entity, request, ct);
        entity.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await _db.Counters.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new Domain.Exceptions.DomainException("COUNTER_NOT_FOUND", "找不到櫃台");

        if (entity.CurrentTicketId.HasValue)
        {
            throw new Domain.Exceptions.DomainException("COUNTER_BUSY", "櫃台仍有服務中的票據，無法刪除");
        }

        _db.Counters.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(QueueCounter entity, UpsertCounterRequest request, CancellationToken ct)
    {
        entity.Code = request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.Weight = request.Weight;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;

        if (request.ServiceId.HasValue)
        {
            var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServiceId.Value, ct)
                ?? throw new Domain.Exceptions.DomainException("SERVICE_NOT_FOUND", "找不到服務類型");
            entity.ServiceId = service.Id;
            entity.ServiceCode = service.Code;
        }
        else
        {
            entity.ServiceId = null;
            entity.ServiceCode = null;
        }
    }
}

/// <summary>
/// 系統設定（§5.1 叫號/語音/顯示器設定）
/// </summary>
public class QueueSettingService
{
    private readonly IQueueDbContext _db;
    private readonly IClock _clock;

    public QueueSettingService(IQueueDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<SettingDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Settings.AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .Select(s => new SettingDto
            {
                Id = s.Id,
                Key = s.Key,
                Value = s.Value,
                ValueType = s.ValueType,
                Category = s.Category,
                Description = s.Description,
                IsSystem = s.IsSystem
            })
            .ToListAsync(ct);
    }

    public async Task UpsertAsync(UpsertSettingRequest request, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var setting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == request.Key, ct);

        if (setting is null)
        {
            setting = new QueueSetting { Key = request.Key, CreatedAt = now };
            _db.Settings.Add(setting);
        }

        setting.Value = request.Value;
        setting.ValueType = request.ValueType ?? InferType(request.Value);
        setting.Category = request.Category ?? "General";
        setting.Description = request.Description;
        setting.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var setting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct)
            ?? throw new Domain.Exceptions.DomainException("SETTING_NOT_FOUND", "找不到設定項目");

        if (setting.IsSystem)
        {
            throw new Domain.Exceptions.DomainException("SETTING_PROTECTED", "系統預設設定不可刪除");
        }

        _db.Settings.Remove(setting);
        await _db.SaveChangesAsync(ct);
    }

    private static string InferType(string value)
    {
        if (bool.TryParse(value, out _)) return "bool";
        if (int.TryParse(value, out _)) return "int";
        if (double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out _)) return "double";
        return "string";
    }
}
