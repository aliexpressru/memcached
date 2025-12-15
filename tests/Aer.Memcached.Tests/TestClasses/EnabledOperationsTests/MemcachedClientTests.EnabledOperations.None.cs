using Aer.Memcached.Client.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses.EnabledOperationsTests;

[TestClass]
public class MemcachedClientTests_EnabledOperations_None : MemcachedClientTests_EnabledOperations_Base
{
    public MemcachedClientTests_EnabledOperations_None() : base(EnabledOperations.None)
    {
    }
}