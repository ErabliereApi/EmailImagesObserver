using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace BlazorApp.Extension;

/// <summary>
/// Class containing extension method to register the IDistributedCache implementation
/// </summary>
public static class AddDistributedCachingExtension
{
    /// <summary>
    /// Register the IDistributedCache implementation. If REDIS_HOSTNAME environment variable is
    /// null or whitespace, the InMemory implementation is register otherwise, the RedisCache 
    /// implementation is register.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection AddDistributedCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redisHostname = configuration.GetValue<string?>("REDIS_HOSTNAME");

        if (redisHostname != null && string.IsNullOrWhiteSpace(redisHostname))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddOptions();

            services.Configure<RedisCacheOptions>(o =>
            {
                o.Configuration = redisHostname;
            });

            services.AddSingleton<IDistributedCache, RedisCache>();
        }

        return services;
    }
}
