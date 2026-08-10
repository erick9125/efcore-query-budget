using Microsoft.Extensions.DependencyInjection;

namespace ErickMorales.EntityFrameworkCore.QueryBudget;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEfCoreQueryBudget(
        this IServiceCollection services,
        Action<QueryBudgetLibraryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<QueryBudgetLibraryOptions>();
        }

        services.AddSingleton<QueryBudgetCommandInterceptor>();
        return services;
    }
}
