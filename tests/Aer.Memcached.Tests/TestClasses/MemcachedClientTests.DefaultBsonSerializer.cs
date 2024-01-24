using Aer.Memcached.Client.Config;
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
