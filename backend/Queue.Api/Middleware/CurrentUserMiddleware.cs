using System.Security.Claims;
using Queue.Application.Services;
using Queue.Domain.Common;
using Queue.Domain.Enums;

namespace Queue.Api.Middleware;

/// <summary>
/// 從 JWT Claims 建立 ICurrentUser（Audit Log / RBAC / 櫃台綁定）。
/// </summary>
public class CurrentUserMiddleware
{
    private readonly RequestDelegate _next;

    public CurrentUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserAccessor accessor)
    {
        var user = context.User;
        var counterId = user.FindFirstValue(JwtTokenFactory.ClaimCounterId);

        accessor.Current = new CurrentUserContext
        {
            UserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"),
            UserName = user.Identity?.Name,
            DisplayName = user.FindFirstValue(JwtTokenFactory.ClaimDisplayName),
            CounterId = long.TryParse(counterId, out var id) ? id : null,
            Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
                .Concat(user.FindAll("role").Select(c => c.Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            IpAddress = ResolveIp(context),
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };

        await _next(context);
    }

    internal static string? ResolveIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp.Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}

/// <summary>
/// Scoped 的 ICurrentUser 容器，因 ICurrentUser 由 Middleware 於 DI 之前建立。
/// </summary>
public interface ICurrentUserAccessor
{
    ICurrentUser Current { get; set; }
}

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    public ICurrentUser Current { get; set; } = new CurrentUserContext();
}
