using Aer.ConsistentHash.Abstractions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.ConsistentHash.Tests.TestClasses;

[TestClass]
public class HashCalculatorTests
{
	[TestMethod]
	public void TestDigestKeyValue()
	{
		IHashCalculator hasher = new HashCalculator();
		var testKey = "Test";

		var digestedValue = hasher.DigestValue(testKey);

		digestedValue.Length.Should().Be(32);
	}
}
