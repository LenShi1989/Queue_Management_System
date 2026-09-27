using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Queue.Infrastructure.Persistence;

/// <summary>
/// 設計時（dotnet ef migrations）用的 DbContext 工廠。
/// 不依賴 API 專案啟動，可獨立於執行環境產生/套用 Migration。
/// </summary>
public class QueueDbContextFactory : IDesignTimeDbContextFactory<QueueDbContext>
{
    private const string FallbackConnection =
        "Host=127.0.0.1;Port=5432;Database=queue;Username=postgres;Password=St923420!;";

    public QueueDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? FallbackConnection;

        var options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseSnakeCaseNamingConvention()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(QueueDbContext).Assembly.FullName))
            .Options;

        return new QueueDbContext(options);
    }
}
