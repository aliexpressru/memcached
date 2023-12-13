using System.Diagnostics.CodeAnalysis;
using Aer.ConsistentHash;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Models;
using Aer.Memcached.Tests.Base;
using Aer.Memcached.Tests.Model.StoredObjectModels;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class MemcachedClientTests : MemcachedClientTestsBase
{
    public MemcachedClientTests():base(isSingleNodeCluster: true)
    { }

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
        await StoreAndGet_CheckType<DateTimeOffset>();
        await StoreAndGet_CheckType<double>();
        await StoreAndGet_CheckType<float>();
        await StoreAndGet_CheckType<SimpleObject>();
        await StoreAndGet_CheckType<Dictionary<string, int>>();
        //await StoreAndGet_CheckType<Dictionary<KeyObject, SimpleObject>>();
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task MultiStoreAndGet_CheckAllTypes(bool withReplicas)
    {
        await MultiStoreAndGet_CheckType<string>(withReplicas);
        await MultiStoreAndGet_CheckType<bool>(withReplicas);
        await MultiStoreAndGet_CheckType<sbyte>(withReplicas);
        await MultiStoreAndGet_CheckType<byte>(withReplicas);
        await MultiStoreAndGet_CheckType<short>(withReplicas);
        await MultiStoreAndGet_CheckType<int>(withReplicas);
        await MultiStoreAndGet_CheckType<long>(withReplicas);
        await MultiStoreAndGet_CheckType<ushort>(withReplicas);
        await MultiStoreAndGet_CheckType<uint>(withReplicas);
        await MultiStoreAndGet_CheckType<ulong>(withReplicas);
        await MultiStoreAndGet_CheckType<char>(withReplicas);
        await MultiStoreAndGet_CheckType<DateTime>(withReplicas);
        await MultiStoreAndGet_CheckType<DateTimeOffset>(withReplicas);
        await MultiStoreAndGet_CheckType<double>(withReplicas);
        await MultiStoreAndGet_CheckType<float>(withReplicas);
        await MultiStoreAndGet_CheckType<SimpleObject>(withReplicas);
        await MultiStoreAndGet_CheckType<Dictionary<string, int>>(withReplicas);
        //await StoreAndGet_CheckType<Dictionary<KeyObject, SimpleObject>>();
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
        await Get_CheckType<DateTimeOffset>();
        await Get_CheckType<double>();
        await Get_CheckType<float>();
        await Get_CheckType<SimpleObject>();
        await Get_CheckType<Dictionary<string, int>>();
        //await StoreAndGet_CheckType<Dictionary<KeyObject, SimpleObject>>();
    }
    
    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task MultiGet_CheckAllTypes_EmptyDictionary(bool withReplicas)
    {
        await MultiGet_CheckType<string>(withReplicas);
        await MultiGet_CheckType<bool>(withReplicas);
        await MultiGet_CheckType<sbyte>(withReplicas);
        await MultiGet_CheckType<byte>(withReplicas);
        await MultiGet_CheckType<short>(withReplicas);
        await MultiGet_CheckType<int>(withReplicas);
        await MultiGet_CheckType<long>(withReplicas);
        await MultiGet_CheckType<ushort>(withReplicas);
        await MultiGet_CheckType<uint>(withReplicas);
        await MultiGet_CheckType<ulong>(withReplicas);
        await MultiGet_CheckType<char>(withReplicas);
        await MultiGet_CheckType<DateTime>(withReplicas);
        await MultiGet_CheckType<DateTimeOffset>(withReplicas);
        await MultiGet_CheckType<double>(withReplicas);
        await MultiGet_CheckType<float>(withReplicas);
        await MultiGet_CheckType<SimpleObject>(withReplicas);
        await MultiGet_CheckType<Dictionary<string, int>>(withReplicas);
        // await StoreAndGet_CheckType<Dictionary<KeyObject, SimpleObject>>();
    }

    [TestMethod]
    [SuppressMessage("ReSharper", "ExpressionIsAlwaysNull", Justification = "Using null as test and expected value")]
    public async Task StoreAndGet_NullAsString()
    {
        var key = Guid.NewGuid().ToString();
        string nullValue = null;

        await Client.StoreAsync(
            key,
            nullValue,
            TimeSpan.FromSeconds(CacheItemExpirationSeconds),
            CancellationToken.None);

        var getValue = await Client.GetAsync<string>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(nullValue);
        getValue.Success.Should().BeTrue();
        getValue.IsEmptyResult.Should().BeFalse();
    }

    [TestMethod]
    public async Task StoreAndGet_ValueExpired()
    {
        var key = Guid.NewGuid().ToString();
        string value = "test";

        await Client.StoreAsync(
            key,
            value,
            TimeSpan.FromSeconds(CacheItemExpirationSeconds),
            CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(CacheItemExpirationSeconds * 2));

        await Client.GetAsync<string>(key, CancellationToken.None);

        var getValue = await Client.GetAsync<string>(key, CancellationToken.None);

        getValue.Result.Should().BeNull();
        getValue.Success.Should().BeTrue();
        getValue.IsEmptyResult.Should().BeTrue();
    }

    [TestMethod]
    public async Task MultiStoreAndGet_ValueExpired()
    {
        var key = Guid.NewGuid().ToString();
        string value = "test";

        await Client.MultiStoreAsync(
            new Dictionary<string, string>()
            {
                [key] = value
            },
            TimeSpan.FromSeconds(CacheItemExpirationSeconds),
            CancellationToken.None);

        var getValue = await Client.MultiGetAsync<string>(new[] {key}, CancellationToken.None);

        getValue.Count.Should().Be(1);
        
        await Task.Delay(TimeSpan.FromSeconds(CacheItemExpirationSeconds * 2));

        getValue = await Client.MultiGetAsync<string>(new[]{key}, CancellationToken.None);

        getValue.Count.Should().Be(0);
    }
    
    [TestMethod]
    public async Task StoreAndGet_EmptyString()
    {
        var key = Guid.NewGuid().ToString();
        string value = string.Empty;

        await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        var getValue = await Client.GetAsync<string>(key, CancellationToken.None);

        getValue.Result.Should().BeNull(value);
        getValue.Success.Should().BeTrue();
        getValue.IsEmptyResult.Should().BeFalse();
    }
    
    [TestMethod]
    public async Task MultiStoreAndGet_NullAsString()
    {
        var keyValues = new Dictionary<string, string>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = null;
        }
    
        await Client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
    
        var getValues = await Client.MultiGetAsync<string>(keyValues.Keys, CancellationToken.None);
    
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
    
        await Client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
    
        var getValues = await Client.MultiGetAsync<string>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeNull();
        }
    }

    [TestMethod]
    public async Task StoreAndGet_SimpleObject()
    {
        var key = Guid.NewGuid().ToString();
        var value = Fixture.Create<SimpleObject>();

        await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        var getValue = await Client.GetAsync<SimpleObject>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value);
        getValue.Success.Should().BeTrue();
        getValue.IsEmptyResult.Should().BeFalse();
    }
    
    [TestMethod]
    public async Task MultiStoreAndGet_SimpleObject()
    {
        var keyValues = new Dictionary<string, SimpleObject>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = Fixture.Create<SimpleObject>();
        }
    
        await Client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
    
        var getValues = await Client.MultiGetAsync<SimpleObject>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeEquivalentTo(keyValues[keyValue.Key]);
        }
    }

    [TestMethod]
    public async Task MultiStoreAndGetBatched_InvalidBatchSize()
    {
        Func<Task> act = async () => await Client.MultiGetAsync<string>(
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

        await Client.MultiStoreAsync(
            keyValues,
            TimeSpan.FromSeconds(CacheItemExpirationSeconds),
            CancellationToken.None,
            batchingOptions: new BatchingOptions());

        var getValues = await Client.MultiGetAsync<string>(
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
        var value = Fixture.Create<ObjectWithCollections>();

        await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        var getValue = await Client.GetAsync<ObjectWithCollections>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value);
        getValue.Success.Should().BeTrue();
        getValue.IsEmptyResult.Should().BeFalse();
    }
    
    [TestMethod]
    public async Task MultiStoreAndGet_ObjectWithCollections()
    {
        var keyValues = new Dictionary<string, ObjectWithCollections>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = Fixture.Create<ObjectWithCollections>();
        }
    
        await Client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
    
        var getValues = await Client.MultiGetAsync<ObjectWithCollections>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeEquivalentTo(keyValues[keyValue.Key]);
        }
    }
    
    [TestMethod]
    public async Task StoreAndGet_ObjectWithEmbeddedObject()
    {
        var key = Guid.NewGuid().ToString();
        var value = Fixture.Create<ObjectWithEmbeddedObject>();

        await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        var getValue = await Client.GetAsync<ObjectWithEmbeddedObject>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value);
        getValue.Success.Should().BeTrue();
        getValue.IsEmptyResult.Should().BeFalse();
    }
    
    [TestMethod]
    public async Task MultiStoreAndGet_ObjectWithEmbeddedObject()
    {
        var keyValues = new Dictionary<string, ObjectWithEmbeddedObject>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = Fixture.Create<ObjectWithEmbeddedObject>();
        }
    
        await Client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
    
        var getValues = await Client.MultiGetAsync<ObjectWithEmbeddedObject>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeEquivalentTo(keyValues[keyValue.Key]);
        }
    }

    [TestMethod]
    public async Task StoreAndGet_RecursiveModel()
    {
        var key = Guid.NewGuid().ToString();
        var value = new RecursiveModel
        {
            Embedded = new RecursiveModel
            {
                Embedded = new RecursiveModel
                {
                    Embedded = new RecursiveModel
                    {
                        X = 10,
                        Y = 20
                    }
                }
            }
        };

        await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        var getValue = await Client.GetAsync<RecursiveModel>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value);
        getValue.Success.Should().BeTrue();
        getValue.IsEmptyResult.Should().BeFalse();
    }
    
    [TestMethod]
    public async Task StoreAndGet_ObjectWithNested_IgnoreReferenceLoopHandling()
    {
        var key = Guid.NewGuid().ToString();
        var value = new ObjectWithNested { X = 1, Y = 2 };
        value.Nested = value;

        await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        var getValue = await Client.GetAsync<RecursiveModel>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value, options => options.Excluding(o => o.Nested));
        getValue.Success.Should().BeTrue();
        getValue.IsEmptyResult.Should().BeFalse();
    }

    [TestMethod]
    public async Task Store_KeyIsTooLong_ValueNotStored_ExceptionLogged()
    {
        var hashCalculator = new HashCalculator();
        var nodeLocator = new HashRing<Pod>(hashCalculator);

        nodeLocator.AddNodes(
            new Pod("localhost")
        );
        
        var loggerMock = Substitute.For<ILogger<CommandExecutor<Pod>>>();

        var config = new MemcachedConfiguration();
        var authProvider = new DefaultAuthenticationProvider(
            new OptionsWrapper<MemcachedConfiguration.AuthenticationCredentials>(config.MemcachedAuth));

        var expirationCalculator = new ExpirationCalculator(hashCalculator, new OptionsWrapper<MemcachedConfiguration>(config));

        var client = new MemcachedClient<Pod>(
            nodeLocator,
            new CommandExecutor<Pod>(
                new OptionsWrapper<MemcachedConfiguration>(config),
                authProvider,
                loggerMock,
                nodeLocator),
            expirationCalculator,
            cacheSynchronizer: null
        );

        var key = new string('*', 251); // this key is too long to be stored
        var value = Guid.NewGuid().ToString();

        await client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        var getValue = await client.GetAsync<string>(key, CancellationToken.None);

        getValue.Result.Should().BeNull();
        getValue.Success.Should().BeFalse();
        getValue.IsEmptyResult.Should().BeTrue();

        var t = loggerMock.ReceivedCalls();
        
        loggerMock
            .Received(2)
            .Log(
                Arg.Is(LogLevel.Error),
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception: Arg.Any<Exception>(),
                formatter: Arg.Any<Func<object, Exception, string>>()
            );
    }

    private async Task StoreAndGet_CheckType<T>()
    {
        var key = Guid.NewGuid().ToString();
        var value = Fixture.Create<T>();

        await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        var getValue = await Client.GetAsync<T>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value);
        getValue.Success.Should().BeTrue();
        getValue.IsEmptyResult.Should().BeFalse();
    }

    private async Task MultiStoreAndGet_CheckType<T>(bool withReplicas)
    {
        var numberOfValues = 5;
        var keyValues = new Dictionary<string, T>();
    
        foreach (var _ in Enumerable.Range(0, numberOfValues))
        {
            keyValues[Guid.NewGuid().ToString()] = Fixture.Create<T>();
        }
    
        await Client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None, replicationFactor: (uint)(withReplicas ? 5 : 0));
    
        var getValues = await Client.MultiGetAsync<T>(keyValues.Keys, CancellationToken.None, replicationFactor: 1);

        getValues.Count.Should().Be(numberOfValues);
        
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeEquivalentTo(keyValues[keyValue.Key]);
        }
    }
    
    [TestMethod]
    public async Task Flush_RemoveAllItems()
    {
        var keys = await MultiStoreAndGetKeys();
        var getValues = await Client.MultiGetAsync<string>(keys, CancellationToken.None);
        getValues.Count.Should().Be(keys.Length);
        
        await Client.FlushAsync(CancellationToken.None);
        getValues = await Client.MultiGetAsync<string>(keys, CancellationToken.None);
        getValues.Should().BeEmpty();
    }
    
    [TestMethod]
    public async Task Delete_Successful()
    {
        var key = Guid.NewGuid().ToString();
        var value = Fixture.Create<SimpleObject>();

        await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        var getValue = await Client.GetAsync<SimpleObject>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value);

        await Client.DeleteAsync(key, CancellationToken.None);
        
        getValue = await Client.GetAsync<SimpleObject>(key, CancellationToken.None);
        getValue.Success.Should().BeTrue();
        getValue.Result.Should().BeNull();
    }
    
    [TestMethod]
    public async Task MultiDelete_OneKey_Successful()
    {
        var key = Guid.NewGuid().ToString();
        var value = Fixture.Create<SimpleObject>();

        await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        var getValue = await Client.GetAsync<SimpleObject>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(value);

        await Client.MultiDeleteAsync(new [] { key }, CancellationToken.None);
        
        getValue = await Client.GetAsync<SimpleObject>(key, CancellationToken.None);
        getValue.Success.Should().BeTrue();
        getValue.Result.Should().BeNull();
    }
    
    [TestMethod]
    public async Task MultiDelete_MultipleKeys_Successful()
    {
        var keyValues = new Dictionary<string, SimpleObject>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = Fixture.Create<SimpleObject>();
        }
    
        await Client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
    
        var getValues = await Client.MultiGetAsync<SimpleObject>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeEquivalentTo(keyValues[keyValue.Key]);
        }
        
        await Client.MultiDeleteAsync(keyValues.Keys, CancellationToken.None);
        
        getValues = await Client.MultiGetAsync<SimpleObject>(keyValues.Keys, CancellationToken.None);

        getValues.Count.Should().Be(0);
    }
    
    [TestMethod]
    public async Task MultiDelete_MultipleKeys_WithBatching_Successful()
    {
        var keyValues = new Dictionary<string, SimpleObject>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = Fixture.Create<SimpleObject>();
        }
    
        await Client.MultiStoreAsync(keyValues, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
    
        var getValues = await Client.MultiGetAsync<SimpleObject>(keyValues.Keys, CancellationToken.None);
    
        foreach (var keyValue in keyValues)
        {
            getValues[keyValue.Key].Should().BeEquivalentTo(keyValues[keyValue.Key]);
        }
        
        await Client.MultiDeleteAsync(keyValues.Keys, CancellationToken.None, new BatchingOptions());
        
        getValues = await Client.MultiGetAsync<SimpleObject>(keyValues.Keys, CancellationToken.None);

        getValues.Count.Should().Be(0);
    }
    
    [TestMethod]
    public async Task Incr_Successful()
    {
        var key = Guid.NewGuid().ToString();
        ulong initialValue = 0;

        var incrValue = await Client.IncrAsync(
            key, 
            1, 
            initialValue, 
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        incrValue.Success.Should().BeTrue();
        incrValue.Result.Should().Be(initialValue);
        
        incrValue = await Client.IncrAsync(
            key, 
            15, 
            initialValue, 
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
        
        incrValue.Success.Should().BeTrue();
        incrValue.Result.Should().Be(15);
    }
    
    [TestMethod]
    public async Task Incr_InitialValueNotZero_Successful()
    {
        var key = Guid.NewGuid().ToString();
        ulong initialValue = 15;

        var incrValue = await Client.IncrAsync(
            key, 
            1, 
            initialValue, 
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        incrValue.Success.Should().BeTrue();
        incrValue.Result.Should().Be(initialValue);
        
        incrValue = await Client.IncrAsync(
            key, 
            1, 
            initialValue, 
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
        
        incrValue.Success.Should().BeTrue();
        incrValue.Result.Should().Be(initialValue + 1);
        
        incrValue = await Client.IncrAsync(
            key, 
            1, 
            initialValue, 
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
        
        incrValue.Success.Should().BeTrue();
        incrValue.Result.Should().Be(initialValue + 2);
    }
    
    [TestMethod]
    public async Task Decr_Successful()
    {
        var key = Guid.NewGuid().ToString();
        ulong initialValue = 15;

        var incrValue = await Client.DecrAsync(
            key, 
            1, 
            initialValue, 
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        incrValue.Success.Should().BeTrue();
        incrValue.Result.Should().Be(initialValue);
        
        incrValue = await Client.DecrAsync(
            key, 
            2, 
            initialValue, 
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
        
        incrValue.Success.Should().BeTrue();
        incrValue.Result.Should().Be(initialValue - 2);
    }
    
    [TestMethod]
    public async Task Decr_ToLessThanZero_DecrementedToZero()
    {
        var key = Guid.NewGuid().ToString();
        ulong initialValue = 5;

        var incrValue = await Client.DecrAsync(
            key, 
            1, 
            initialValue, 
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        incrValue.Success.Should().BeTrue();
        incrValue.Result.Should().Be(initialValue);
        
        incrValue = await Client.DecrAsync(
            key, 
            10, 
            initialValue, 
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);
        
        incrValue.Success.Should().BeTrue();
        incrValue.Result.Should().Be(0);
    }

    private async Task<string[]> MultiStoreAndGetKeys()
    {
        var keyValues = Enumerable.Range(0, 10).Select(_ => (key: Guid.NewGuid().ToString("N"), value: Guid.NewGuid().ToString("N")))
            .ToDictionary(x => x.key, x => x.value);

        await Client.MultiStoreAsync(keyValues, TimeSpan.FromMinutes(10), CancellationToken.None);
        
        return keyValues.Keys.ToArray();
    }

    private async Task Get_CheckType<T>()
    {
        var key = Guid.NewGuid().ToString();

        var getValue = await Client.GetAsync<T>(key, CancellationToken.None);

        getValue.Result.Should().BeEquivalentTo(default(T));
        getValue.Success.Should().BeTrue();
        getValue.IsEmptyResult.Should().BeTrue();
    }

    private async Task MultiGet_CheckType<T>(bool withReplicas)
    {
        var keyValues = new Dictionary<string, T>();
    
        foreach (var _ in Enumerable.Range(0, 5))
        {
            keyValues[Guid.NewGuid().ToString()] = Fixture.Create<T>();
        }

        var getValues = await Client.MultiGetAsync<T>(keyValues.Keys, CancellationToken.None, replicationFactor: (uint)(withReplicas ? 1 : 0));
        getValues.Count.Should().Be(0);
    }
}