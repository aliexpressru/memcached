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
using Aer.Memcached.Helpers;
using Aer.Memcached.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Aer.Memcached;

/// <summary>
/// The extension methods for setting up Memcached services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Memcached services to the <see cref="IServiceCollection"/> with settings from app configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
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

    /// <summary>
    /// Enables Memcached diagnostics listeners for metrics and logging.
    /// </summary>
    /// <param name="applicationBuilder">The application builder instance.</param>
    /// <param name="configuration">The application configuration.</param>
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

    /// <summary>
    /// Adds endpoints for internode synchronization to the <see cref="IEndpointRouteBuilder"/> with settings from app configuration.
    /// </summary>
    /// <param name="endpoints">The service endpoints route builder instance.</param>
    /// <param name="configuration">The application configuration.</param>
    public static void AddMemcachedEndpoints(
        this IEndpointRouteBuilder endpoints,
        IConfiguration configuration)
    {
        var config = configuration
            .GetSection(nameof(MemcachedConfiguration))
            .Get<MemcachedConfiguration>();

        var deleteEndpoint = config.SyncSettings == null
            ? MemcachedConfiguration.DefaultDeleteEndpoint
            : config.SyncSettings.DeleteEndpoint;

        var flushEndpoint = config.SyncSettings == null
            ? MemcachedConfiguration.DefaultFlushEndpoint
            : config.SyncSettings.FlushEndpoint;

        var getEndpoint = config.SyncSettings == null
            ? MemcachedConfiguration.DefaultGetEndpoint
            : config.SyncSettings.GetEndpoint;

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
            }
        ).AllowAnonymousIfConfigured(config);

        endpoints.MapPost(
            flushEndpoint,
            async (IMemcachedClient memcachedClient, CancellationToken token) =>
            {
                await memcachedClient.FlushAsync(token);
            }
        ).AllowAnonymousIfConfigured(config);

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
                        token,
                        model.ExpirationMap,
                        model.BatchingOptions);
                }
            ).AllowAnonymousIfConfigured(config);
        }

        endpoints.MapPost(
            getEndpoint,
            (MultiGetTypedRequest request, IMemcachedClient memcachedClient, CancellationToken token) =>
            {
                try
                {
                    var resolvedType = Type.GetType(request.Type);
                    if (resolvedType == null)
                    {
                        return Results.Ok(
                            $"Type is not found. Try {typeof(string).FullName} or {typeof(object).FullName}");
                    }

                    var method = typeof(MemcachedClient<Pod>).GetMethod(nameof(MemcachedClient<Pod>.MultiGetAsync));
                    if (method == null)
                    {
                        return Results.Ok($"Method for the type {resolvedType} is not found");
                    }

                    var genericMethod = method.MakeGenericMethod(resolvedType);

                    var task =
                        genericMethod.Invoke(
                            memcachedClient,
                            parameters: [request.Keys, token, null, (uint) 0]) as Task;
                    if (task == null)
                    {
                        return Results.Ok($"Method for the type {resolvedType} is not found");
                    }

                    var result = task.GetType().GetProperty("Result")?.GetValue(task);

                    return Results.Ok(Newtonsoft.Json.JsonConvert.SerializeObject(result));
                }
                catch (Exception e)
                {
                    return Results.BadRequest(e);
                }
            }
        ).AllowAnonymousIfConfigured(config);
    }

    private class MultiGetTypedRequest
    {
        public string[] Keys { get; set; }
        
        public string Type { get; set; }
    }
}