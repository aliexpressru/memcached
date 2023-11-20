using System.Diagnostics;
using System.Text.Json;
using Aer.ConsistentHash;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Abstractions;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.CacheSync;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Diagnostics;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Aer.Memcached.Diagnostics;
using Aer.Memcached.Diagnostics.Listeners;
using Aer.Memcached.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aer.Memcached;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMemcached(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.Configure<MemcachedConfiguration>(configuration.GetSection(nameof(MemcachedConfiguration)));
        services.AddSingleton<IHashCalculator, HashCalculator>();
        services.AddSingleton<INodeProvider<Pod>, HeadlessServiceDnsLookupNodeProvider>();
        services.AddSingleton<INodeLocator<Pod>, HashRing<Pod>>();
        services.AddSingleton<INodeHealthChecker<Pod>, NodeHealthChecker<Pod>>();
        services.AddSingleton<ICommandExecutor<Pod>, CommandExecutor<Pod>>();
        services.AddSingleton<IExpirationCalculator, ExpirationCalculator>();
        
        services.AddHttpClient();
        services.AddSingleton<ISyncServersProvider, DefaultSyncServersProvider>();
        services.AddSingleton<ICacheSynchronizer, CacheSynchronizer>();
        services.AddSingleton<IErrorStatisticsStore, SlidingWindowStatisticsStore>();
        services.AddSingleton<ICacheSyncClient, CacheSyncClient>();

        services.AddHostedService<MemcachedMaintainer<Pod>>();
        services.AddScoped<IMemcachedClient, MemcachedClient<Pod>>();

        services.AddSingleton<IAuthenticationProvider, DefaultAuthenticationProvider>();
        services.Configure<MemcachedConfiguration.AuthenticationCredentials>(
            configuration.GetSection(nameof(MemcachedConfiguration.MemcachedAuth)));

        var config = configuration.GetSection(nameof(MemcachedConfiguration)).Get<MemcachedConfiguration>();
        if (!config.Diagnostics.DisableDiagnostics)
        {
            var metricFactory = Prometheus.Client.Metrics.DefaultFactory;
            var collectorRegistry = Prometheus.Client.Metrics.DefaultCollectorRegistry;

            services.AddSingleton(metricFactory);
            services.AddSingleton(collectorRegistry);
            services.AddSingleton<MemcachedMetrics>();
            services.AddSingleton<MetricsMemcachedDiagnosticListener>();
            services.AddSingleton<LoggingMemcachedDiagnosticListener>();
            services.AddSingleton(MemcachedDiagnosticSource.Instance);
        }
        
        return services;
    }

    public static IApplicationBuilder EnableMemcachedDiagnostics(this IApplicationBuilder applicationBuilder,
        IConfiguration configuration)
    {
        var config = configuration.GetSection(nameof(MemcachedConfiguration)).Get<MemcachedConfiguration>();

        if (!config.Diagnostics.DisableDiagnostics)
        {
            DiagnosticListener diagnosticSource =
                applicationBuilder.ApplicationServices.GetRequiredService<MemcachedDiagnosticSource>();

            var metricsListener =
                applicationBuilder.ApplicationServices.GetRequiredService<MetricsMemcachedDiagnosticListener>();

            var loggingListener =
                applicationBuilder.ApplicationServices.GetRequiredService<LoggingMemcachedDiagnosticListener>();

            diagnosticSource.SubscribeWithAdapter(metricsListener);
            diagnosticSource.SubscribeWithAdapter(loggingListener);
        }

        return applicationBuilder;
    }

    public static void AddMemcachedSyncEndpoint<T>(this IEndpointRouteBuilder endpoints, IConfiguration configuration)
    {
        var config = configuration.GetSection(nameof(MemcachedConfiguration)).Get<MemcachedConfiguration>();

        if (config.SyncSettings != null)
        {
            endpoints.MapPost(config.SyncSettings.SyncEndpoint + $"-{typeof(T).Name.ToLowerInvariant()}",
                async ([FromBody] CacheSyncModel<T> model, IMemcachedClient memcachedClient,
                    CancellationToken token) =>
                {
                    await memcachedClient.MultiStoreAsync(
                        model.KeyValues,
                        model.ExpirationTime,
                        token,
                        isManualSyncOn: false);
                });
        }
    }
}