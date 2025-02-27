using Aer.ConsistentHash;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Config;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class ExpirationCalculatorTests
{
    [TestMethod]
    public void Expiration_WithoutJitter_WorksAsExpected()
    {
        var hashCalculator = new HashCalculator();
        var config = new MemcachedConfiguration();

        var expirationCalculator = new ExpirationCalculator(hashCalculator, new OptionsWrapper<MemcachedConfiguration>(config));

        var expirationTime = TimeSpan.FromSeconds(10);
        var expectedExpiration = (uint)(DateTimeOffset.UtcNow + expirationTime).ToUnixTimeSeconds();

        var expiration = expirationCalculator.GetExpiration("test", expirationTime);

        expiration.Should().Be(expectedExpiration);
    }
    
    [TestMethod]
    public void BatchExpiration_WithoutJitter_WorksAsExpected()
    {
        var hashCalculator = new HashCalculator();
        var config = new MemcachedConfiguration();

        var expirationCalculator = new ExpirationCalculator(hashCalculator, new OptionsWrapper<MemcachedConfiguration>(config));

        var expirationTime = TimeSpan.FromSeconds(10);
        var expectedExpiration = (uint)(DateTimeOffset.UtcNow + expirationTime).ToUnixTimeSeconds();

        var testKeys = new[] { "test1", "test2", "test3" };
        var expiration = expirationCalculator.GetExpiration(testKeys, expirationTime);

        foreach (var keyToExpiration in expiration)
        {
            keyToExpiration.Value.Should().Be(expectedExpiration);
        }
    }
    
    [TestMethod]
    public void BatchExpiration_WithKeyExpirationMap_WorksAsExpected()
    {
        var hashCalculator = new HashCalculator();
        var config = new MemcachedConfiguration();

        var expirationCalculator = new ExpirationCalculator(hashCalculator, new OptionsWrapper<MemcachedConfiguration>(config));

        var expirationMap = new Dictionary<string, TimeSpan?>()
        {
            { "test1", TimeSpan.FromSeconds(10) },
            { "test2", TimeSpan.FromDays(2) },
            { "test3", null }
        };
        var expiration = expirationCalculator.GetExpiration(expirationMap);

        var utcNow = DateTimeOffset.UtcNow;
        foreach (var keyToExpiration in expiration)
        {
            var expirationTime = expirationMap[keyToExpiration.Key];
            uint expectedExpiration;
            if (!expirationTime.HasValue)
            {
                expectedExpiration = 0U;
            }
            else
            {
                expectedExpiration = (uint)(utcNow + expirationTime.Value).ToUnixTimeSeconds();
            }

            keyToExpiration.Value.Should().Be(expectedExpiration);
        }
    }
    
    [TestMethod]
    public void BatchExpiration_WithKeyExpirationMap_DateTimeOffset_WorksAsExpected()
    {
        var hashCalculator = new HashCalculator();
        var config = new MemcachedConfiguration();

        var expirationCalculator = new ExpirationCalculator(hashCalculator, new OptionsWrapper<MemcachedConfiguration>(config));

        var utcNow = DateTimeOffset.UtcNow;
        var expirationMap = new Dictionary<string, DateTimeOffset?>()
        {
            { "test1", utcNow.AddSeconds(10) },
            { "test2", utcNow.AddDays(2) },
            { "test3", null }
        };
        
        var expiration = expirationCalculator.GetExpiration(expirationMap);
        foreach (var keyToExpiration in expiration)
        {
            var expirationTime = expirationMap[keyToExpiration.Key];
            uint expectedExpiration;
            if (!expirationTime.HasValue)
            {
                expectedExpiration = 0U;
            }
            else
            {
                expectedExpiration = (uint)(expirationTime.Value).ToUnixTimeSeconds();
            }

            keyToExpiration.Value.Should().Be(expectedExpiration);
        }
    }
    
    [DataTestMethod]
    [DataRow(1)]
    [DataRow(10)]
    [DataRow(15)]
    [DataRow(150)]
    [DataRow(0)]
    public void Expiration_WithJitter_WorksAsExpected(double multiplication)
    {
        var hashCalculator = new HashCalculator();
        
        var config = new MemcachedConfiguration
        {
            ExpirationJitter = new MemcachedConfiguration.ExpirationJitterSettings
            {
                MultiplicationFactor = multiplication
            }
        };

        var expirationCalculator = new ExpirationCalculator(hashCalculator, new OptionsWrapper<MemcachedConfiguration>(config));

        var key = "test";
        var hash = hashCalculator.ComputeHash(key);
        var jitter = (hash % config.ExpirationJitter.SpreadFactor) * multiplication;
        
        var expirationTime = TimeSpan.FromSeconds(10);
        var expectedExpiration = (uint)((DateTimeOffset.UtcNow + expirationTime).ToUnixTimeSeconds() + jitter);

        var expiration = expirationCalculator.GetExpiration(key, expirationTime);

        expiration.Should().Be(expectedExpiration);
    }
    
    [DataTestMethod]
    [DataRow(1)]
    [DataRow(10)]
    [DataRow(15)]
    [DataRow(150)]
    [DataRow(0)]
    public void BatchExpiration_WithJitter_WorksAsExpected(double multiplication)
    {
        var hashCalculator = new HashCalculator();
        
        var config = new MemcachedConfiguration
        {
            ExpirationJitter = new MemcachedConfiguration.ExpirationJitterSettings
            {
                MultiplicationFactor = multiplication
            }
        };

        var expirationCalculator = new ExpirationCalculator(hashCalculator, new OptionsWrapper<MemcachedConfiguration>(config));

        var testKeys = new[] { "test1", "test2", "test3" };
        var keysToExpectedExpirationMap = new Dictionary<string, uint>();

        var expirationTime = TimeSpan.FromSeconds(10);
        foreach (var testKey in testKeys)
        {
            var hash = hashCalculator.ComputeHash(testKey);
            var jitter = (hash % config.ExpirationJitter.SpreadFactor) * multiplication;
            
            var expectedExpiration = (uint)((DateTimeOffset.UtcNow + expirationTime).ToUnixTimeSeconds() + jitter);

            keysToExpectedExpirationMap[testKey] = expectedExpiration;
        }

        var keysToExpirationMap = new Dictionary<string, uint>();
        foreach (var testKey in testKeys)
        {
            var expiration = expirationCalculator.GetExpiration(testKey, expirationTime);
            
            keysToExpirationMap[testKey] = expiration;
        }

        keysToExpectedExpirationMap.Should().BeEquivalentTo(keysToExpirationMap);
    }

    [TestMethod]
    public void ZeroExpiration_WithoutJitter_ResultsInInfiniteExpiration()
    {
        var hashCalculator = new HashCalculator();
        var config = new MemcachedConfiguration();

        var expirationCalculator = new ExpirationCalculator(
            hashCalculator,
            new OptionsWrapper<MemcachedConfiguration>(config));

        var key = "test";

        List<uint> singleKeyExpirations = new()
        {
            expirationCalculator.GetExpiration(key, null),
            expirationCalculator.GetExpiration(key, TimeSpan.Zero),
            expirationCalculator.GetExpiration(key, TimeSpan.MaxValue)
        };

        List<Dictionary<string, uint>> multiKeyExpirations = new()
        {
            expirationCalculator.GetExpiration(new[] {key}, (TimeSpan?) null),
            expirationCalculator.GetExpiration(new[] {key}, TimeSpan.Zero),
            expirationCalculator.GetExpiration(new[] {key}, TimeSpan.MaxValue),
            expirationCalculator.GetExpiration(new[] {key}, (DateTimeOffset?) null),
            expirationCalculator.GetExpiration(new[] {key}, DateTimeOffset.MaxValue)
        };

        singleKeyExpirations.Should().AllSatisfy(e => e.Should().Be(0));

        multiKeyExpirations.Should()
            .AllSatisfy(kv => kv.Values.Should().AllSatisfy(v=>v.Should().Be(0)));
    }

    [TestMethod]
    public void ZeroExpiration_WithJitter_ResultsInInfiniteExpiration()
    {
        var hashCalculator = new HashCalculator();

        var config = new MemcachedConfiguration
        {
            ExpirationJitter = new MemcachedConfiguration.ExpirationJitterSettings
            {
                MultiplicationFactor = 2
            }
        };

        var expirationCalculator = new ExpirationCalculator(
            hashCalculator,
            new OptionsWrapper<MemcachedConfiguration>(config));

        var key = "test";

        List<uint> singleKeyExpirations = new()
        {
            expirationCalculator.GetExpiration(key, null),
            expirationCalculator.GetExpiration(key, TimeSpan.Zero),
            expirationCalculator.GetExpiration(key, TimeSpan.MaxValue)
        };

        List<Dictionary<string, uint>> multiKeyExpirations = new()
        {
            expirationCalculator.GetExpiration(new[] {key}, (TimeSpan?) null),
            expirationCalculator.GetExpiration(new[] {key}, TimeSpan.Zero),
            expirationCalculator.GetExpiration(new[] {key}, TimeSpan.MaxValue),
            expirationCalculator.GetExpiration(new[] {key}, (DateTimeOffset?) null),
            expirationCalculator.GetExpiration(new[] {key}, DateTimeOffset.MaxValue)
        };

        singleKeyExpirations.Should().AllSatisfy(e => e.Should().Be(0));

        multiKeyExpirations.Should()
            .AllSatisfy(kv => kv.Values.Should().AllSatisfy(v => v.Should().Be(0)));
    }
}