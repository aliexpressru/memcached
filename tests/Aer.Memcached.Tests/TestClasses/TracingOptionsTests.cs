using System.Diagnostics;
using Aer.ConsistentHash;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Aer.Memcached.Client.Serializers;
using Aer.Memcached.Tests.Infrastructure;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class TracingOptionsTests
{
    private readonly Fixture _fixture;
    private MemcachedClient<Pod> _client;
    private ServiceProvider _serviceProvider;
    private readonly List<Activity> _capturedActivities;
    private TracerProvider _tracerProvider;
    private ActivitySource _activitySource;
    private ActivityListener _activityListener;

    public TracingOptionsTests()
    {
        _fixture = new Fixture();
        _capturedActivities = new List<Activity>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
        _tracerProvider?.Dispose();
        _activityListener?.Dispose();
        _activitySource?.Dispose();
        _capturedActivities.Clear();
    }

    [TestMethod]
    public async Task StoreAsync_WithTracingDisabled_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<string>();

        using var activity = new Activity("test-parent").Start();

        // Act
        var result = await client.StoreAsync(
            key,
            value,
            TimeSpan.FromSeconds(60),
            CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task StoreAsync_WithTracingEnabled_CreatesSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<string>();

        using var activity = new Activity("test-parent").Start();
        activity.Should().NotBeNull("Parent activity should be created");

        // Act
        var result = await client.StoreAsync(
            key,
            value,
            TimeSpan.FromSeconds(60),
            CancellationToken.None,
            tracingOptions: TracingOptions.Enabled);

        // Assert
        result.Success.Should().BeTrue();

        // Verify that spans were created when TracingOptions.Enabled is passed
        _capturedActivities.Should().NotBeEmpty(
            "spans should be created when EnableTracing=true and TracingOptions.Enabled");
        _capturedActivities.Should().Contain(a => a.DisplayName.Contains("memcached"),
            "should contain memcached operation spans");
    }

    [TestMethod]
    public async Task StoreAsync_WithoutTracingOptions_UsesDefaultBehavior()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<string>();

        using var activity = new Activity("test-parent").Start();
        activity.Should().NotBeNull("Parent activity should be created");

        // Act
        var result = await client.StoreAsync(
            key,
            value,
            TimeSpan.FromSeconds(60),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Verify that with EnableTracing = true and Activity.Current != null,
        // the default behavior enables tracing (spans should be created without explicit TracingOptions)
        _capturedActivities.Should().NotBeEmpty(
            "spans should be created by default when EnableTracing=true");
        _capturedActivities.Should().Contain(a => a.DisplayName.Contains("memcached"),
            "should contain memcached operation spans");
    }

    [TestMethod]
    public async Task MultiStoreAsync_WithTracingDisabled_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var keyValues = new Dictionary<string, string>
        {
            [Guid.NewGuid().ToString()] = _fixture.Create<string>(),
            [Guid.NewGuid().ToString()] = _fixture.Create<string>()
        };

        // Act
        var result = await client.MultiStoreAsync(
            keyValues,
            TimeSpan.FromSeconds(60),
            CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetAsync_WithTracingDisabled_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<string>();

        await client.StoreAsync(key, value, TimeSpan.FromSeconds(60), CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        _capturedActivities.Clear();

        // Act
        var result = await client.GetAsync<string>(
            key,
            CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task MultiGetAsync_WithTracingDisabled_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var keyValues = new Dictionary<string, string>
        {
            [Guid.NewGuid().ToString()] = _fixture.Create<string>(),
            [Guid.NewGuid().ToString()] = _fixture.Create<string>()
        };

        await client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(60), CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        _capturedActivities.Clear();

        // Act
        var results = await client.MultiGetAsync<string>(
            keyValues.Keys,
            CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        results.Should().HaveCount(keyValues.Count);
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task DeleteAsync_WithTracingDisabled_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<string>();

        await client.StoreAsync(key, value, TimeSpan.FromSeconds(60), CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        _capturedActivities.Clear();

        // Act
        var result = await client.DeleteAsync(
            key,
            CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task IncrAsync_WithTracingDisabled_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var key = Guid.NewGuid().ToString();

        // Act
        var result = await client.IncrAsync(
            key,
            amountToAdd: 1,
            initialValue: 0,
            TimeSpan.FromSeconds(60),
            CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task DecrAsync_WithTracingDisabled_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var key = Guid.NewGuid().ToString();

        // Act
        var result = await client.DecrAsync(
            key,
            amountToSubtract: 1,
            initialValue: 100,
            TimeSpan.FromSeconds(60),
            CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetAndTouchAsync_WithTracingDisabled_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<string>();

        await client.StoreAsync(key, value, TimeSpan.FromSeconds(60), CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        _capturedActivities.Clear();

        // Act
        var result = await client.GetAndTouchAsync<string>(
            key,
            TimeSpan.FromSeconds(120),
            CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TracingOptions_Disabled_WorksInFireAndForgetScenario()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var keyValues = new Dictionary<string, string>
        {
            [Guid.NewGuid().ToString()] = _fixture.Create<string>(),
            [Guid.NewGuid().ToString()] = _fixture.Create<string>()
        };

        // Act
        var fireAndForgetTask = Task.Run(async () =>
        {
            await Task.Delay(100);

            await client.MultiStoreAsync(
                keyValues,
                TimeSpan.FromMinutes(30),
                CancellationToken.None,
                tracingOptions: TracingOptions.Disabled);
        });

        await fireAndForgetTask;

        fireAndForgetTask.IsCompletedSuccessfully.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task StoreAsync_WithoutTracer_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: false);
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<string>();

        // Act
        var result = await client.StoreAsync(
            key,
            value,
            TimeSpan.FromSeconds(60),
            CancellationToken.None,
            tracingOptions: TracingOptions.Enabled);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task MultiStoreAsync_WithBatching_WithTracingDisabled_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var keyValues = new Dictionary<string, string>();

        for (int i = 0; i < 10; i++)
        {
            keyValues[Guid.NewGuid().ToString()] = _fixture.Create<string>();
        }

        var batchingOptions = new BatchingOptions
        {
            BatchSize = 3,
            MaxDegreeOfParallelism = 2
        };

        // Act
        var result = await client.MultiStoreAsync(
            keyValues,
            TimeSpan.FromSeconds(60),
            CancellationToken.None,
            batchingOptions: batchingOptions,
            tracingOptions: TracingOptions.Disabled);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task MultiGetAsync_WithBatching_WithTracingDisabled_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var keyValues = new Dictionary<string, string>();

        for (int i = 0; i < 10; i++)
        {
            keyValues[Guid.NewGuid().ToString()] = _fixture.Create<string>();
        }

        var batchingOptions = new BatchingOptions
        {
            BatchSize = 3,
            MaxDegreeOfParallelism = 2
        };

        await client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(60), CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        _capturedActivities.Clear();

        // Act
        var results = await client.MultiGetAsync<string>(
            keyValues.Keys,
            CancellationToken.None,
            batchingOptions: batchingOptions,
            tracingOptions: TracingOptions.Disabled);

        results.Should().HaveCount(keyValues.Count);
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Configuration_EnableTracingFalse_DoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: false);
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<string>();

        using var activity = new Activity("test-parent").Start();

        // Act
        var result = await client.StoreAsync(
            key,
            value,
            TimeSpan.FromSeconds(60),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Configuration_EnableTracingFalse_WithTracingOptionsEnabled_StillDoesNotCreateSpans()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: false);
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<string>();

        using var activity = new Activity("test-parent").Start();

        // Act
        var result = await client.StoreAsync(
            key,
            value,
            TimeSpan.FromSeconds(60),
            CancellationToken.None,
            tracingOptions: TracingOptions.Enabled);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Configuration_EnableTracingTrue_CreatesSpansByDefault()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<string>();

        using var activity = new Activity("test-parent").Start();
        activity.Should().NotBeNull("Parent activity should be created");

        // Act
        var result = await client.StoreAsync(
            key,
            value,
            TimeSpan.FromSeconds(60),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Verify that spans are created by default when EnableTracing = true
        _capturedActivities.Should().NotBeEmpty(
            "spans should be created by default when EnableTracing=true in configuration");
        _capturedActivities.Should().Contain(a => a.DisplayName.Contains("memcached"),
            "should contain memcached operation spans");
    }

    [TestMethod]
    public async Task Configuration_EnableTracingTrue_CanBeDisabledWithTracingOptions()
    {
        // Arrange
        var client = CreateClientWithTracing(enableTracing: true);
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<string>();

        using var activity = new Activity("test-parent").Start();

        // Act
        var result = await client.StoreAsync(
            key,
            value,
            TimeSpan.FromSeconds(60),
            CancellationToken.None,
            tracingOptions: TracingOptions.Disabled);

        result.Success.Should().BeTrue();
        _capturedActivities.Should().BeEmpty();
    }

    private MemcachedClient<Pod> CreateClientWithTracing(bool enableTracing = true)
    {
        var hashCalculator = new HashCalculator();
        var nodeLocator = new HashRing<Pod>(hashCalculator);
        nodeLocator.AddNodes(new Pod("localhost", port: 11211));

        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var config = new MemcachedConfiguration
        {
            Diagnostics = new MemcachedConfiguration.MemcachedDiagnosticsSettings
            {
                DisableDiagnostics = true,
                DisableRebuildNodesStateLogging = true,
                EnableTracing = enableTracing
            },
            BinarySerializerType = ObjectBinarySerializerType.Bson
        };

        var authProvider = new DefaultAuthenticationProvider(
            new OptionsWrapper<MemcachedConfiguration.AuthenticationCredentials>(config.MemcachedAuth));

        var configWrapper = new OptionsWrapper<MemcachedConfiguration>(config);
        var expirationCalculator = new ExpirationCalculator(hashCalculator, configWrapper);

        ServiceCollection sc = new ServiceCollection();
        sc.AddSingleton<IObjectBinarySerializer, TestObjectBinarySerializer>();

        if (enableTracing)
        {
            // Create ActivitySource with "Aer.Memcached" name - this is what OpenTelemetry will use
            // to create activities when tracer.StartActiveSpan() is called
            _activitySource = new ActivitySource("Aer.Memcached");

            // Set up ActivityListener to capture all activities from "Aer.Memcached" source
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Aer.Memcached",
                // Use AllDataAndRecorded instead of AllData to ensure activities are both:
                // 1. Recorded (Activity.IsAllDataRequested = true) - enables all data to be captured
                // 2. Exportable (Activity.Recorded = true) - makes them visible to exporters and listeners
                // This is required for ActivityListener.ActivityStopped to receive the activities
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    // Capture all stopped activities from Aer.Memcached source
                    if (activity.Source.Name == "Aer.Memcached")
                    {
                        _capturedActivities.Add(activity);
                    }
                }
            };
            ActivitySource.AddActivityListener(_activityListener);

            // Configure OpenTelemetry TracerProvider
            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("test-service"))
                .AddSource("Aer.Memcached") // Listen to activities from "Aer.Memcached" source
                .Build();

            sc.AddSingleton(_tracerProvider);
            // GetTracer with "Aer.Memcached" name - this must match the ActivitySource name
            sc.AddSingleton(_ => _tracerProvider.GetTracer("Aer.Memcached"));
        }

        _serviceProvider = sc.BuildServiceProvider();

        var tracer = enableTracing ? _serviceProvider.GetService<Tracer>() : null;

        _client = new MemcachedClient<Pod>(
            nodeLocator,
            new CommandExecutor<Pod>(
                configWrapper,
                authProvider,
                loggerFactory.CreateLogger<CommandExecutor<Pod>>(),
                nodeLocator,
                tracer),
            expirationCalculator,
            null,
            new BinarySerializer(
                new ObjectBinarySerializerFactory(configWrapper, _serviceProvider)),
            loggerFactory.CreateLogger<MemcachedClient<Pod>>(),
            configWrapper);

        return _client;
    }
}

