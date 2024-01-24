using Aer.Memcached.Client.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
// ReSharper disable once InconsistentNaming
public class MemcachedClientTests_MessagePackSerializer : MemcachedClientMethodsTestsBase
{
	public MemcachedClientTests_MessagePackSerializer() : base(
		binarySerializerType: ObjectBinarySerializerType.MessagePack)
	{ }
}
