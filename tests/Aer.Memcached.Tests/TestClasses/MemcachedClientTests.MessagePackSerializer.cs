using Aer.Memcached.Client.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
// ReSharper disable once InconsistentNaming
public class MemcachedClientTests_MessagePackSerializer : MemcachedClientTests
{
	public MemcachedClientTests_MessagePackSerializer() : base(
		binarySerializerType: ObjectBinarySerializerType.MessagePack)
	{ }
}
