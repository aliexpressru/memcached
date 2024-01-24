using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Tests.Base;
using Aer.Memcached.Tests.Infrastructure;
using Aer.Memcached.Tests.Model.StoredObjects;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
// ReSharper disable once InconsistentNaming
public class MemcachedClientTests_CustomSerializer : MemcachedClientTestsBase
{
	// NOTE: custom TestObjectBinarySerializer serializer is identical to MessagePack serializer
	// Thus these tests do not inherit from MemcachedClientMethodsTestsBase to avoid testing already tested behavior
	
	public MemcachedClientTests_CustomSerializer() 
		: base(isSingleNodeCluster: true, binarySerializerType: ObjectBinarySerializerType.Custom)
	{ }

	[TestMethod]
	public async Task StoreAndGet_CheckCustomSerializerUsed()
	{
		var key = Guid.NewGuid().ToString();
		var value = Fixture.Create<SimpleObject>();

		var testSerializer = (TestObjectBinarySerializer)
			ServiceProvider.GetRequiredService<IObjectBinarySerializer>();
		
		testSerializer.ClearCounts();
		
		await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

		testSerializer.SerializationsCount.Should().Be(1);
		
		var getValue = await Client.GetAsync<SimpleObject>(key, CancellationToken.None);
		
		getValue.Result.Should().BeEquivalentTo(value);
		getValue.Success.Should().BeTrue();
		getValue.IsEmptyResult.Should().BeFalse();

		testSerializer.DeserializationsCount.Should().Be(1);
	}
}
