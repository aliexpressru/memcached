using Aer.Memcached.Client.Models;
using Aer.Memcached.Tests.Base;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class ReplicationTests : MemcachedClientTestsBase
{
	private readonly BatchingOptions _batchingOptions = new()
	{
		MaxDegreeOfParallelism = Environment.ProcessorCount,
		BatchSize = 2
	};

	/// <summary>
	/// Generates unique test items for each test invocation to avoid conflicts between parallel tests
	/// </summary>
	private static Dictionary<string, string> GenerateTestItems()
	{
		var guid = Guid.NewGuid().ToString("N");
		return new Dictionary<string, string>
		{
			[$"{guid}_key_1"] = "test value 1",
			[$"{guid}_key_2"] = "test value 2",
			[$"{guid}_key_3"] = "test value 3",
			[$"{guid}_key_4"] = "test value 4",
			[$"{guid}_key_5"] = "test value 5",
			[$"{guid}_key_6"] = "test value 6",
			[$"{guid}_key_7"] = "test value 7",
			[$"{guid}_key_8"] = "test value 8",
			[$"{guid}_key_9"] = "test value 9",
			[$"{guid}_key_10"] = "test value 10",
		};
	}
	
	public ReplicationTests() : base(isSingleNodeCluster: false)
	{ }


	[TestMethod]
	public async Task MultiGet_WithoutStore_WithReplication()
	{
		var testItems = GenerateTestItems();
		
		var getValues = await Client.MultiGetAsync<string>(
			testItems.Keys,
			CancellationToken.None,
			replicationFactor: 3);

		getValues.Count.Should().Be(0);
	}

	[DataTestMethod]
	[DataRow(1U)]
	[DataRow(2U)] // entire cluster size
	[DataRow(5U)] // more than entire cluster size
	public async Task MultiStore_WithoutReplication_Get_WithReplication(uint replicationFactor)
	{
		var testItems = GenerateTestItems();
		
		await Client.MultiStoreAsync(
			testItems,
			TimeSpan.FromSeconds(CacheItemExpirationSeconds),
			CancellationToken.None,
			replicationFactor: 0); // explicitly set replication factor to 0

		var getValues = await Client.MultiGetAsync<string>(
			testItems.Keys,
			CancellationToken.None,
			replicationFactor: replicationFactor);

		getValues.Count.Should().Be(testItems.Count);

		foreach (var (expectedKey, expectedValue) in testItems)
		{
			getValues[expectedKey].Should().Be(expectedValue);
		}
	}

	[DataTestMethod]
	[DataRow(1U, false)] 
	[DataRow(2U, false)] // entire cluster size
	[DataRow(5U, false)] // more than entire cluster size
	[DataRow(1U, true)] 
	[DataRow(2U, true)] // entire cluster size
	[DataRow(5U, true)] // more than entire cluster size
	public async Task MultiStoreAndGet_WithReplication(uint replicationFactor, bool isUseBatching)
	{
		var testItems = GenerateTestItems();
		
		await Client.MultiStoreAsync(
			testItems,
			TimeSpan.FromSeconds(CacheItemExpirationSeconds),
			CancellationToken.None,
			replicationFactor: replicationFactor);

		IDictionary<string, string> getValues;
		
		if (isUseBatching)
		{
			getValues = await Client.MultiGetAsync<string>(
				testItems.Keys,
				CancellationToken.None,
				batchingOptions : _batchingOptions,
				replicationFactor: replicationFactor);
		}
		else
		{
			getValues = await Client.MultiGetAsync<string>(
				testItems.Keys,
				CancellationToken.None,
				batchingOptions: null,
				replicationFactor: replicationFactor);
		}

		getValues.Count.Should().Be(testItems.Count);
		
		foreach (var (expectedKey, expectedValue) in testItems)
		{
			getValues[expectedKey].Should().Be(expectedValue);
		}
	}

	[TestMethod]
	public async Task MultiStoreDeleteAndGet_WithReplication()
	{
		uint storeReadReplicationFactor = 2;

		uint deleteReplicationFactor = 1; // delete from original node and 1 replica but leave one replica intact

		var itemsToStore = new Dictionary<string, string>()
		{
			[Guid.NewGuid().ToString()] = "test_value"
		};

		await Client.MultiStoreAsync(
			itemsToStore,
			TimeSpan.FromSeconds(CacheItemExpirationSeconds),
			CancellationToken.None,
			replicationFactor: storeReadReplicationFactor);

		// delete value but not from all replicas
		await Client.MultiDeleteAsync(
			itemsToStore.Keys,
			CancellationToken.None,
			replicationFactor: deleteReplicationFactor);

		var getValues = await Client.MultiGetAsync<string>(
			itemsToStore.Keys,
			CancellationToken.None,
			batchingOptions: _batchingOptions,
			replicationFactor: storeReadReplicationFactor);

		getValues.Count.Should().Be(itemsToStore.Count);

		foreach (var (expectedKey, expectedValue) in itemsToStore)
		{
			getValues[expectedKey].Should().Be(expectedValue);
		}
	}
}
