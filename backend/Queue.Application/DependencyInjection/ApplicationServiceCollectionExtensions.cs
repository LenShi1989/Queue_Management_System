using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Queue.Application.Abstractions;
using Queue.Application.Common;
using Queue.Application.Services;
using Queue.Domain.Common;

namespace Queue.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddQueueApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<QrOptions>(configuration.GetSection(QrOptions.SectionName));
        services.Configure<QueueOptions>(configuration.GetSection(QueueOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<QrTokenService>();
        services.AddSingleton<JwtTokenFactory>();

        services.AddScoped<TicketNumberGenerator>();
        services.AddScoped<SettingProvider>();
        services.AddScoped<WaitTimeEstimator>();
        services.AddScoped<TicketHistoryWriter>();
        services.AddScoped<TicketService>();
        services.AddScoped<CounterService>();
        services.AddScoped<DisplayQueryService>();
        services.AddScoped<StatisticsService>();
        services.AddScoped<AuthService>();
        services.AddScoped<QueueServiceTypeService>();
        services.AddScoped<QueueCounterService>();
        services.AddScoped<QueueSettingService>();

        return services;
    }
}
