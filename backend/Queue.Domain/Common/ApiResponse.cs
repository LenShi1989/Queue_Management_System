namespace Queue.Domain.Common;

/// <summary>
/// 統一 API 回應格式（§32）
/// 成功：{ "success": true, "data": {} }
/// 失敗：{ "success": false, "code": "QUEUE_NOT_FOUND", "message": "...", "traceId": "..." }
/// </summary>
public class ApiResponse
{
    public bool Success { get; init; }

    public string? Code { get; init; }

    public string? Message { get; init; }

    public string? TraceId { get; init; }

    public object? Data { get; init; }

    public static ApiResponse Ok(object? data = null, string? message = null)
        => new() { Success = true, Data = data, Message = message };

    public static ApiResponse Fail(string code, string message, string? traceId = null)
        => new() { Success = false, Code = code, Message = message, TraceId = traceId };
}

public class ApiResponse<T> : ApiResponse
{
    public new T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null)
        => new() { Success = true, Data = data, Message = message };

    public static new ApiResponse<T> Fail(string code, string message, string? traceId = null)
        => new() { Success = false, Code = code, Message = message, TraceId = traceId };
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
        => new() { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
}
