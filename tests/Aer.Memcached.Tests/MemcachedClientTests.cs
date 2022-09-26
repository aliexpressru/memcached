using Aer.ConsistentHash;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Models;
using Aer.Memcached.Tests.Models;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Aer.Memcached.Tests;


[TestClass]
public class MemcachedClientTests
{
    private readonly MemcachedClient<Pod> _client;
    private readonly Fixture _fixture;

    private const int ExpirationInSeconds = 10;

    public MemcachedClientTests()
    {
        var hashCalculator = new HashCalculator();
        var nodeLocator = new HashRing<Pod>(hashCalculator);
        nodeLocator.AddNodes(new Pod[]
        {
            new()
            {
                IpAddress = "localhost"
            }
        });

        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var commandExecutorLogger = loggerFactory.CreateLogger<CommandExecutor<Pod>>();
        var config = new MemcachedConfiguration();
        var authProvider = new DefaultAuthenticationProvider(new OptionsWrapper<MemcachedConfiguration.AuthenticationCredentials>(config.MemcachedAuth));

        _client = new MemcachedClient<Pod>(
            nodeLocator, 
            new CommandExecutor<Pod>(
                new OptionsWrapper<MemcachedConfiguration>(
                    config), authProvider, commandExecutorLogger));
        
        _fixture = new Fixture();
    }

    [TestMethod]
    public async Task StoreAndGet_CheckAllTypes()
    {
        await StoreAndGet_CheckType<string>();
        await StoreAndGet_CheckType<bool>();
        await StoreAndGet_CheckType<sbyte>();
        await StoreAndGet_CheckType<byte>();
        await StoreAndGet_CheckType<short>();
        await StoreAndGet_CheckType<int>();
        await StoreAndGet_CheckType<long>();
        await StoreAndGet_CheckType<ushort>();
        await StoreAndGet_CheckType<uint>();
        await StoreAndGet_CheckType<ulong>();
        await StoreAndGet_CheckType<char>();
        await StoreAndGet_CheckType<DateTime>();
        await StoreAndGet_CheckType<double>();
        await StoreAndGet_CheckType<float>();
    }
    
    [TestMethod]
    public async Task MultiStoreAndGet_CheckAllTypes()
    {
        await MultiStoreAndGet_CheckType<string>();
        await MultiStoreAndGet_CheckType<bool>();
        await MultiStoreAndGet_CheckType<sbyte>();
        await MultiStoreAndGet_CheckType<byte>();
        await MultiStoreAndGet_CheckType<short>();
        await MultiStoreAndGet_CheckType<int>();
        await MultiStoreAndGet_CheckType<long>();
        await MultiStoreAndGet_CheckType<ushort>();
        await MultiStoreAndGet_CheckType<uint>();
        await MultiStoreAndGet_CheckType<ulong>();
        await MultiStoreAndGet_CheckType<char>();
        await MultiStoreAndGet_CheckType<DateTime>();
        await MultiStoreAndGet_CheckType<double>();
        await MultiStoreAndGet_CheckType<float>();
    }

    [TestMethod]
    public async Task Get_CheckAllTypes_DefaultValue()
    {
        await Get_CheckType<string>();
        await Get_CheckType<bool>();
        await Get_CheckType<sbyte>();
        await Get_CheckType<byte>();
        await Get_CheckType<short>();
        await Get_CheckType<int>();
        await Get_CheckType<long>();
        await Get_CheckType<ushort>();
        await Get_CheckType<uint>();
        await Get_CheckType<ulong>();
        await Get_CheckType<char>();
        await Get_CheckType<DateTime>();
        await Get_CheckType<double>();
        await Get_CheckType<float>();
        await Get_CheckType<SimpleObject>();
    }
    
    [TestMethod]
    public async Task MultiGet_CheckAllTypes_EmptyDictionary()
    {
        await MultiGet_CheckType<string>();
        await MultiGet_CheckType<bool>();
        await MultiGet_CheckType<sbyte>();
        await MultiGet_CheckType<byte>();
        await MultiGet_CheckType<short>();
        await MultiGet_CheckType<int>();
        await MultiGet_CheckType<long>();
        await MultiGet_CheckType<ushort>();
        await MultiGet_CheckType<uint>();
        await MultiGet_CheckType<ulong>();
        await MultiGet_CheckType<char>();
        await MultiGet_CheckType<DateTime>();
        await MultiGet_CheckType<double>();
        await MultiGet_CheckType<float>();
        await MultiGet_CheckType<SimpleObject>();
    }

    [TestMethod]
    public async Task StoreAndGet_NullAsString()
    {
        var key = Guid.NewGuid().ToString();
        string value = null;

        await _client.StoreAsync(key, value, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);

        var getValue = await _client.GetAsync<string>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value);
        getValue.Success.Should().BeTrue();
    }

    [TestMethod]
    public async Task StoreAndGet_ValueExpired()
    {
        var key = Guid.NewGuid().ToString();
        string value = "test";

        await _client.StoreAsync(key, value, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(ExpirationInSeconds * 2));
        
        await _client.GetAsync<string>(key, CancellationToken.None);
        
        var getValue = await _client.GetAsync<string>(key, CancellationToken.None);

        getValue.Result.Should().BeNull();
        getValue.Success.Should().BeTrue();
    }

    [TestMethod]
    public async Task MultiStoreAndGet_ValueExpired()
    {
        var key = Guid.NewGuid().ToString();
        string value = "test";

        await _client.MultiStoreAsync(
            new Dictionary<string, string>()
            {
                [key] = value
            },
            TimeSpan.FromSeconds(ExpirationInSeconds),
            CancellationToken.None);

        var getValue = await _client.MultiGetAsync<string>(new[] {key}, CancellationToken.None);

        getValue.Count.Should().Be(1);
        
        await Task.Delay(TimeSpan.FromSeconds(ExpirationInSeconds * 2));

        getValue = await _client.MultiGetAsync<string>(new[]{key}, CancellationToken.None);

        getValue.Count.Should().Be(0);
    }
    
    [TestMethod]
    public async Task StoreAndGet_EmptyString()
    {
        var key = Guid.NewGuid().ToString();
        string value = string.Empty;

        await _client.StoreAsync(key, value, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);

        var getValue = await _client.GetAsync<string>(key, CancellationToken.None);

        getValue.Result.Should().BeNull(value);
        getValue.Success.Should().BeTrue();
    }
    
    [TestMethod]
    public async Task MultiStoreAndGet_NullAsString()
    {
        var keyValues = new Dictionary<string, string>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = null;
        }
    
        await _client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);
    
        var getValues = await _client.MultiGetAsync<string>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().Be(keyValues[keyValue.Key]);
        }
    }
    
    [TestMethod]
    public async Task MultiStoreAndGet_EmptyString()
    {
        var keyValues = new Dictionary<string, string>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = string.Empty;
        }
    
        await _client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);
    
        var getValues = await _client.MultiGetAsync<string>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeNull();
        }
    }

    [TestMethod]
    public async Task StoreAndGet_SimpleObject()
    {
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<SimpleObject>();

        await _client.StoreAsync(key, value, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);

        var getValue = await _client.GetAsync<SimpleObject>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value, options => options.Excluding(info => info.DateTimeValue));
        getValue.Result.DateTimeValue.Should().BeCloseTo(value.DateTimeValue, TimeSpan.FromMilliseconds(1));
    }
    
    [TestMethod]
    public async Task MultiStoreAndGet_SimpleObject()
    {
        var keyValues = new Dictionary<string, SimpleObject>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = _fixture.Create<SimpleObject>();
        }
    
        await _client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);
    
        var getValues = await _client.MultiGetAsync<SimpleObject>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeEquivalentTo(keyValues[keyValue.Key], options => options.Excluding(info => info.DateTimeValue));
            getValues[keyValue.Key].DateTimeValue.Should().BeCloseTo(keyValues[keyValue.Key].DateTimeValue, TimeSpan.FromMilliseconds(1));
        }
    }

    [TestMethod]
    public async Task MultiStoreAndGetBatched_InvalidBatchSize()
    {
        Func<Task> act = async () => await _client.MultiGetAsync<string>(
            new[] {"some_key"}, // this value is not important since we are checking method parameters validation
            CancellationToken.None,
            new BatchingOptions()
            {
                BatchSize = -100 // invalid batch size
            });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains("should be > 0"));
    }

    [TestMethod]
    public async Task MultiStoreAndGetBatched()
    {
        var keyValues = new Dictionary<string, string>();

        foreach (var _ in Enumerable.Range(0, 10_000))
        {
            keyValues[Guid.NewGuid().ToString()] = Guid.NewGuid().ToString();
        }

        await _client.MultiStoreAsync(
            keyValues,
            TimeSpan.FromSeconds(ExpirationInSeconds),
            CancellationToken.None,
            batchingOptions: new BatchingOptions());

        var getValues = await _client.MultiGetAsync<string>(
            keyValues.Keys,
            CancellationToken.None,
            new BatchingOptions());

        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeEquivalentTo(keyValues[keyValue.Key]);
        }
    }

    [TestMethod]
    public async Task StoreAndGet_ObjectWithCollections()
    {
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<ObjectWithCollections>();

        await _client.StoreAsync(key, value, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);

        var getValue = await _client.GetAsync<ObjectWithCollections>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value, options => options.Excluding(info => info.SimpleObjects));
        getValue.Success.Should().BeTrue();
        for (int i = 0; i < getValue.Result.SimpleObjects.Count; i++)
        {
            getValue.Result.SimpleObjects[i].Should().BeEquivalentTo(value.SimpleObjects[i], options => options.Excluding(info => info.DateTimeValue));
            getValue.Result.SimpleObjects[i].DateTimeValue.Should().BeCloseTo(value.SimpleObjects[i].DateTimeValue, TimeSpan.FromMilliseconds(1));
        }
    }
    
    [TestMethod]
    public async Task MultiStoreAndGet_ObjectWithCollections()
    {
        var keyValues = new Dictionary<string, ObjectWithCollections>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = _fixture.Create<ObjectWithCollections>();
        }
    
        await _client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);
    
        var getValues = await _client.MultiGetAsync<ObjectWithCollections>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeEquivalentTo(keyValues[keyValue.Key], options => options.Excluding(info => info.SimpleObjects));
            for (int i = 0; i < getValues[keyValue.Key].SimpleObjects.Count; i++)
            {
                getValues[keyValue.Key].SimpleObjects[i].Should().BeEquivalentTo(keyValues[keyValue.Key].SimpleObjects[i], options => options.Excluding(info => info.DateTimeValue));
                getValues[keyValue.Key].SimpleObjects[i].DateTimeValue.Should().BeCloseTo(keyValues[keyValue.Key].SimpleObjects[i].DateTimeValue, TimeSpan.FromMilliseconds(1));
            }
        }
    }
    
    [TestMethod]
    public async Task StoreAndGet_ObjectWithEmbeddedObject()
    {
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<ObjectWithEmbeddedObject>();

        await _client.StoreAsync(key, value, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);

        var getValue = await _client.GetAsync<ObjectWithEmbeddedObject>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value, options => options.Excluding(info => info.ComplexObject.SimpleObject.DateTimeValue));
        getValue.Success.Should().BeTrue();
        getValue.Result.ComplexObject.SimpleObject.DateTimeValue.Should().BeCloseTo(value.ComplexObject.SimpleObject.DateTimeValue, TimeSpan.FromMilliseconds(1));
    }
    
    [TestMethod]
    public async Task MultiStoreAndGet_ObjectWithEmbeddedObject()
    {
        var keyValues = new Dictionary<string, ObjectWithEmbeddedObject>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = _fixture.Create<ObjectWithEmbeddedObject>();
        }
    
        await _client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);
    
        var getValues = await _client.MultiGetAsync<ObjectWithEmbeddedObject>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeEquivalentTo(keyValues[keyValue.Key], options => options.Excluding(info => info.ComplexObject.SimpleObject.DateTimeValue));
            getValues[keyValue.Key].ComplexObject.SimpleObject.DateTimeValue.Should().BeCloseTo(keyValues[keyValue.Key].ComplexObject.SimpleObject.DateTimeValue, TimeSpan.FromMilliseconds(1));
        }
    }

    [TestMethod]
    public async Task Store_KeyIsTooLong_ValueNotStored_ExceptionLogged()
    {
        var hashCalculator = new HashCalculator();
        var nodeLocator = new HashRing<Pod>(hashCalculator);
        nodeLocator.AddNodes(new Pod[]
        {
            new()
            {
                IpAddress = "localhost"
            }
        });

        var loggerMock = new Mock<ILogger<CommandExecutor<Pod>>>();
        var config = new MemcachedConfiguration();
        var authProvider = new DefaultAuthenticationProvider(new OptionsWrapper<MemcachedConfiguration.AuthenticationCredentials>(config.MemcachedAuth));
        
        var client = new MemcachedClient<Pod>(
            nodeLocator, 
            new CommandExecutor<Pod>(
                new OptionsWrapper<MemcachedConfiguration>(
                    config), authProvider, loggerMock.Object));
        
        var key = new string('*', 251);
        var value = Guid.NewGuid().ToString();

        await client.StoreAsync(key, value, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);

        var getValue = await client.GetAsync<string>(key, CancellationToken.None);

        getValue.Result.Should().BeNull();
        getValue.Success.Should().BeFalse();
        loggerMock.Invocations.Count(i => i.Method.Name == nameof(LoggerExtensions.Log) && i.Arguments.First().ToString() == LogLevel.Error.ToString()).Should().Be(2);
    }
    
    private async Task StoreAndGet_CheckType<T>()
    {
        var key = Guid.NewGuid().ToString();
        var value = _fixture.Create<T>();

        await _client.StoreAsync(key, value, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);

        var getValue = await _client.GetAsync<T>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value);
        getValue.Success.Should().BeTrue();
    }

    private async Task MultiStoreAndGet_CheckType<T>()
    {
        var keyValues = new Dictionary<string, T>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = _fixture.Create<T>();
        }
    
        await _client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(ExpirationInSeconds), CancellationToken.None);
    
        var getValues = await _client.MultiGetAsync<T>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().Be(keyValues[keyValue.Key]);
        }
    }
    
    [TestMethod]
    public async Task Flush_RemoveAllItems()
    {
        var keys = await MultiStoreAndGetKeys();
        var getValues = await _client.MultiGetAsync<string>(keys, CancellationToken.None);
        getValues.Count.Should().Be(keys.Length);
        
        await _client.FlushAsync(CancellationToken.None);
        getValues = await _client.MultiGetAsync<string>(keys, CancellationToken.None);
        getValues.Should().BeEmpty();
    }
    
    private async Task<string[]> MultiStoreAndGetKeys()
    {
        var keyValues = Enumerable.Range(0, 10).Select(_ => (key: Guid.NewGuid().ToString("N"), value: Guid.NewGuid().ToString("N")))
            .ToDictionary(x => x.key, x => x.value);

        await _client.MultiStoreAsync(keyValues, TimeSpan.FromMinutes(10), CancellationToken.None);
        
        return keyValues.Keys.ToArray();
    }

    private async Task Get_CheckType<T>()
    {
        var key = Guid.NewGuid().ToString();

        var getValue = await _client.GetAsync<T>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(default(T));
        getValue.Success.Should().BeTrue();
    }

    private async Task MultiGet_CheckType<T>()
    {
        var keyValues = new Dictionary<string, T>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = _fixture.Create<T>();
        }

        var getValues = await _client.MultiGetAsync<T>(keyValues.Keys, CancellationToken.None);
        getValues.Count.Should().Be(0);
    }
}