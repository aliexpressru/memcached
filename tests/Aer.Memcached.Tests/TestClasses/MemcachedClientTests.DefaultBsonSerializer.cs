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
public class MemcachedClientTests_DefaultBsonSerializer : MemcachedClientMethodsTestsBase
{
	public MemcachedClientTests_DefaultBsonSerializer() 
		: base(binarySerializerType: ObjectBinarySerializerType.Bson)
	{ }
}
