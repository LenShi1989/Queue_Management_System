using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Queue.Api.Common;
using Queue.Api.Hubs;
using Queue.Api.Middleware;
using Queue.Application.Abstractions;
using Queue.Application.Common;
using Queue.Application.Services;
using Queue.Domain.Common;
using Queue.Domain.Enums;
using Queue.Infrastructure.DependencyInjection;
using Queue.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────── Serilog（§28） ───────────────────────────
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Queue.Api")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// ─────────────────────────── Infrastructure + Application ───────────────────────────
builder.Services.AddQueueInfrastructure(builder.Configuration);

// ─────────────────────────── SignalR（§13） ───────────────────────────
var redisConnection = builder.Configuration["Redis:ConnectionString"];
var signalRBuilder = builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    signalRBuilder.AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("Queue.Api");
    });
}

builder.Services.AddSingleton<IQueueNotifier, SignalRQueueNotifier>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<ICurrentUserAccessor>().Current);

// ─────────────────────────── JWT（§20） ───────────────────────────
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Secret) && o.Secret.Length >= 32,
        "Jwt:Secret 必須設定且長度至少 32 字元（§40）")
    .ValidateOnStart();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                 ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(
                    ApiResponse.Fail("UNAUTHORIZED", "尚未登入或登入已過期", context.HttpContext.TraceIdentifier)));
            },
            OnForbidden = async context =>
            {
                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(
                    ApiResponse.Fail("FORBIDDEN", "沒有存取此資源的權限", context.HttpContext.TraceIdentifier)));
            }
        };
    });

builder.Services.AddAuthorization();

// ─────────────────────────── Rate Limiting（§25） ───────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        if (context.HttpContext.Response.HasStarted)
        {
            return;
        }

        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(
            ApiResponse.Fail("RATE_LIMITED", "請求過於頻繁，請稍後再試", context.HttpContext.TraceIdentifier)));
    };

    // 公開端點（取號、QR 查詢）較嚴格
    options.AddFixedWindowLimiter("kiosk", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });

    options.AddFixedWindowLimiter("public-query", limiter =>
    {
        limiter.PermitLimit = 120;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });

    // 全域預設（登入等敏感操作）
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
        context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 600,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ─────────────────────────── Swagger（§30） ───────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Queue Management System API",
        Version = "v1.0",
        Description = "排隊號碼進度管理系統 REST API（v1.0 開發規格書）",
        Contact = new OpenApiContact { Name = "QMS" }
    });

    foreach (var group in SwaggerGroups.All)
    {
        options.SwaggerDoc(group, new OpenApiInfo
        {
            Title = $"QMS API - {group}",
            Version = "v1.0",
            Description = $"分組：{group}"
        });
    }

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    // 一次註冊一個 predicate：多次呼叫會被 AND 串接，導致所有分組都無路徑
    options.DocInclusionPredicate((docName, apiDescription) =>
    {
        var groupName = apiDescription.GroupName ?? "v1";

        // v1 為總覽文件，包含所有 API
        if (string.Equals(docName, "v1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(docName, groupName, StringComparison.OrdinalIgnoreCase);
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "直接輸入 JWT（不需加 Bearer 前綴）"
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer")] = []
    });
});

// ─────────────────────────── CORS（§25） ───────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy.SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("Content-Disposition");
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("Content-Disposition");
        }
    });
});

// ─────────────────────────── Health Check（§29） ───────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
builder.Services.AddHealthChecks()
    .AddCheck("api", () => HealthCheckResult.Healthy("API is running"), tags: ["live"])
    .AddNpgSql(connectionString, name: "database", tags: ["ready"]);

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddHealthChecks()
        .AddRedis(redisConnection, name: "redis", tags: ["ready"]);
}

builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Period = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// ─────────────────────────── Pipeline ───────────────────────────
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("ClientIp", CurrentUserMiddleware.ResolveIp(httpContext));
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    foreach (var group in SwaggerGroups.All.Prepend("v1"))
    {
        options.SwaggerEndpoint($"/swagger/{group}/swagger.json", $"QMS API - {group}");
    }

    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Queue Management System API";
});

app.UseAuthentication();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<QueueHub>(QueueHub.Route);

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = HealthResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("live"),
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();

app.MapGet("/", () => Results.Ok(new ApiResponse
{
    Success = true,
    Message = "Queue Management System API v1.0",
    Data = new
    {
        docs = "/swagger",
        health = "/health",
        signalr = QueueHub.Route,
        time = DateTimeOffset.Now
    }
})).AllowAnonymous();

app.MapGet("/api/version", () => Results.Ok(ApiResponse.Ok(new
{
    version = "1.0.0",
    environment = app.Environment.EnvironmentName,
    timezone = "Asia/Taipei",
    utcNow = DateTimeOffset.UtcNow
}))).AllowAnonymous();

// ─────────────────────────── 啟動時：Migration + 設定初始化 ───────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<QueueDbContext>();
    var queueOptions = scope.ServiceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<QueueOptions>>().Value;
    var displayQuery = scope.ServiceProvider.GetRequiredService<DisplayQueryService>();

    try
    {
        if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
        {
            logger.LogInformation("套用資料庫 Migration…");
            await db.Database.MigrateAsync();
            logger.LogInformation("資料庫 Migration 完成");
        }

        displayQuery.Configure(queueOptions.MaxRecallCount);
        logger.LogInformation(
            "Queue.Api 啟動完成。平均服務分鐘數={Avg}；最大再叫次數={MaxRecall}；老化={Aging}",
            queueOptions.DefaultAverageServiceMinutes, queueOptions.MaxRecallCount, queueOptions.AgingEnabled);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "資料庫初始化失敗，應用程式無法啟動。請確認 PostgreSQL 連線設定。");
        throw;
    }
}

await app.RunAsync();

/// <summary>供整合測試 WebApplicationFactory 使用。</summary>
public partial class Program;
