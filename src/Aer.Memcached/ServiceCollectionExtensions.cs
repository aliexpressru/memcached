using System.Diagnostics;
using Aer.ConsistentHash;
using Aer.ConsistentHash.Abstractions;
using Aer.ConsistentHash.Config;
using Aer.Memcached.Abstractions;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.CacheSync;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Diagnostics;
using Aer.Memcached.Client.Extensions;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Aer.Memcached.Client.Serializers;
using Aer.Memcached.Diagnostics;
using Aer.Memcached.Diagnostics.Listeners;
using Aer.Memcached.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Aer.Memcached;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMemcached(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<MemcachedConfiguration>(configuration.GetSection(nameof(MemcachedConfiguration)));
        services.Configure<HashRingSettings>(configuration.GetSection($"{nameof(MemcachedConfiguration)}:{nameof(MemcachedConfiguration.HashRing)}"));
        services.AddSingleton<IHashCalculator, HashCalculator>();
        services.AddSingleton<INodeProvider<Pod>, HeadlessServiceDnsLookupNodeProvider>();
        services.AddSingleton<INodeLocator<Pod>, HashRing<Pod>>();
        services.AddSingleton<INodeHealthChecker<Pod>, NodeHealthChecker<Pod>>();
        services.AddSingleton<ICommandExecutor<Pod>, CommandExecutor<Pod>>();
        services.AddSingleton<IExpirationCalculator, ExpirationCalculator>();

        services.AddSingleton<IObjectBinarySerializerFactory, ObjectBinarySerializerFactory>();
        services.AddSingleton<BinarySerializer>();

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

            // add prometheus metrics dependencies
            services.AddSingleton(metricFactory);
            services.AddSingleton(collectorRegistry);

            // add open telemetry metrics dependencies
            services.AddOpenTelemetryMetrics(MemcachedMetricsProvider.MeterName);
            
            services.AddSingleton<MemcachedMetricsProvider>();
            
            services.AddSingleton<MetricsMemcachedDiagnosticListener>();
            services.AddSingleton<LoggingMemcachedDiagnosticListener>();
            services.AddSingleton(MemcachedDiagnosticSource.Instance);
        }

        return services;
    }

    private static void AddOpenTelemetryMetrics(this IServiceCollection services, string meterName)
    {
        ArgumentNullException.ThrowIfNull(meterName);

        // Register IMeterFactory - https://github.com/dotnet/core/issues/8436#issuecomment-1575846943
        services.AddMetrics();
        
        services.AddOpenTelemetry().WithMetrics(
            builder =>
            {
                builder.AddMeter(meterName);
                
                builder.AddView(
                    instrument =>
                    {
                        var buckets = MemcachedMetricsProvider.MetricsBuckets.GetValueOrDefault(instrument.Name);

                        return buckets is not null
                            ? new ExplicitBucketHistogramConfiguration() {Boundaries = buckets}
                            : null;
                    });
            });
    }

    public static IApplicationBuilder EnableMemcachedDiagnostics(
        this IApplicationBuilder applicationBuilder,
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

    public static void AddMemcachedSyncEndpoint(this IEndpointRouteBuilder endpoints, IConfiguration configuration)
    {
        var config = configuration.GetSection(nameof(MemcachedConfiguration)).Get<MemcachedConfiguration>();

        if (config.SyncSettings != null)
        {
            endpoints.MapPost(
                config.SyncSettings.SyncEndpoint + TypeExtensions.GetTypeName<byte>(),
                async (
                    [FromBody] CacheSyncModel model,
                    IMemcachedClient memcachedClient,
                    CancellationToken token) =>
                {
                    await memcachedClient.MultiStoreSynchronizeDataAsync(
                        model.KeyValues,
                        model.Flags,
                        model.ExpirationTime,
                        token);
                });
        }
    }

    public static void AddMemcachedEndpoints(this IEndpointRouteBuilder endpoints, IConfiguration configuration)
    {
        var config = configuration.GetSection(nameof(MemcachedConfiguration)).Get<MemcachedConfiguration>();
        var deleteEndpoint = config.SyncSettings == null
            ? MemcachedConfiguration.DefaultDeleteEndpoint
            : config.SyncSettings.DeleteEndpoint;
        var flushEndpoint = config.SyncSettings == null
            ? MemcachedConfiguration.DefaultFlushEndpoint
            : config.SyncSettings.FlushEndpoint;

        endpoints.MapPost(
            deleteEndpoint,
            async (
                [FromBody] IEnumerable<string> keys,
                IMemcachedClient memcachedClient,
                CancellationToken token) =>
            {
                await memcachedClient.MultiDeleteAsync(
                    keys,
                    token,
                    cacheSyncOptions: new CacheSyncOptions
                    {
                        IsManualSyncOn = false
                    });
            });

        endpoints.MapPost(
            flushEndpoint,
            async (IMemcachedClient memcachedClient, CancellationToken token) =>
            {
                await memcachedClient.FlushAsync(token);
            });
    }
}