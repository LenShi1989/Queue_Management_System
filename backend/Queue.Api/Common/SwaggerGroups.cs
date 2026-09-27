using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Queue.Domain.Common;

namespace Queue.Api.Common;

/// <summary>
/// Swagger API 分組（§30）
/// Auth / Queue / Counter / Service / Display / Statistics / Admin
/// </summary>
public static class SwaggerGroups
{
    public const string Auth = "Auth";
    public const string Queue = "Queue";
    public const string Counter = "Counter";
    public const string Service = "Service";
    public const string Display = "Display";
    public const string Statistics = "Statistics";
    public const string Admin = "Admin";

    public static readonly string[] All = [Auth, Queue, Counter, Service, Display, Statistics, Admin];

    public static IEnumerable<string> Prepend(this IEnumerable<string> source, string first)
        => new[] { first }.Concat(source);
}

/// <summary>
/// Health Check 輸出格式（§29）
/// </summary>
public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            success = report.Status == HealthStatus.Healthy,
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 2)
                }),
            timestamp = DateTimeOffset.UtcNow
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
