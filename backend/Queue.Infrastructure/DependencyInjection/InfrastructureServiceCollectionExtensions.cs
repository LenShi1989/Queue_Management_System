using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Queue.Application.Abstractions;
using Queue.Application.DependencyInjection;
using Queue.Application.Services;
using Queue.Domain.Common;
using Queue.Infrastructure.Persistence;

namespace Queue.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ConnectionName = "DefaultConnection";

    public static IServiceCollection AddQueueInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionName = ConnectionName)
    {
        var connectionString = configuration.GetConnectionString(connectionName)
                               ?? throw new InvalidOperationException(
                                   $"找不到連線字串 ConnectionStrings:{connectionName}");

        services.AddDbContext<QueueDbContext>(options =>
        {
            // 欄位/索引名稱採 snake_case，與 raw SQL（FOR UPDATE、UPSERT）一致
            options.UseSnakeCaseNamingConvention();

            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(QueueDbContext).Assembly.FullName);
                npgsql.CommandTimeout(60);
                npgsql.EnableRetryOnFailure(3);
            });
        });

        services.AddScoped<IQueueDbContext>(sp => sp.GetRequiredService<QueueDbContext>());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<QueueDbContext>());

        services.AddQueueApplication(configuration);

        return services;
    }
}
