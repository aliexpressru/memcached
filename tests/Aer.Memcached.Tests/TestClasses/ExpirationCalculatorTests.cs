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
}