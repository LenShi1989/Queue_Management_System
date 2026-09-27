using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Queue.Domain.Common;
using Queue.Domain.Exceptions;

namespace Queue.Api.Middleware;

/// <summary>
/// 全域例外處理 → §32 統一錯誤格式
/// { "success": false, "code": "...", "message": "...", "traceId": "..." }
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        var (statusCode, code, message) = exception switch
        {
            DomainException domain => (StatusCodes.Status400BadRequest, domain.Code, domain.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "無權存取"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "NOT_FOUND", "找不到資料"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "資料已被其他操作更新，請重試"),
            DbUpdateException dbEx => (StatusCodes.Status400BadRequest, "DB_ERROR", _environment.IsDevelopment() ? dbEx.Message : "資料庫操作失敗"),
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "REQUEST_CANCELED", "請求已取消"),
            _ => (StatusCodes.Status500InternalServerError, "SYSTEM_ERROR", _environment.IsDevelopment() ? exception.Message : "系統發生錯誤")
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "UnhandledException TraceId={TraceId} Path={Path}", traceId, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("RequestFailed Code={Code} TraceId={TraceId} Path={Path} Message={Message}",
                code, traceId, context.Request.Path, message);
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsync(JsonSerializer.Serialize(
            ApiResponse.Fail(code, message, traceId), JsonOptions));
    }
}
