using Aer.Memcached.Client.Extensions;
using Aer.Memcached.Client.Models;
using Aer.Memcached.Tests.Infrastructure.Logging;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class MemcachedClientResultExtensionsTests
{
	private readonly DummyMicrosoftLogger _logger = new ();

	[DataTestMethod]
	[DataRow("test message")]
	[DataRow(null)]
	public void TestLogging_UntypedResult(string customMessage)
	{
		var errorResult = MemcachedClientResult.Unsuccessful("Some error");
		errorResult.LogErrorIfAny(_logger, customErrorMessage: customMessage);

		_logger.WrittenEvents.Count.Should().Be(1);
		var firstMessage = _logger.WrittenEvents[0].message; 

		if (string.IsNullOrEmpty(customMessage))
		{
			firstMessage.Should().Contain("Error happened during memcached");
		}
		else
		{
			firstMessage.Should().Contain(customMessage);
		}
	}

	[DataTestMethod]
	[DataRow(null, null)]
	[DataRow(null, 2)]
	[DataRow("test message", null)]
	[DataRow("test message", 2)]
	public void TestLogging_TypedResult(string customMessage, int? keyCount)
	{
		var errorResult = MemcachedClientValueResult<int>.Unsuccessful("Some error", -1);
		
		errorResult.LogErrorIfAny(_logger, cacheKeysCount: keyCount, customErrorMessage: customMessage);

		_logger.WrittenEvents.Count.Should().Be(1);
		var firstMessage = _logger.WrittenEvents[0].message; 
		
		switch (keyCount, customMessage)
		{
			case (_, {Length: > 0} customErrorMessage):
			{
				firstMessage.Should().Contain(customErrorMessage);
				break;
			}
			case ({ } keysCount, null):
			{
				firstMessage.Should().Contain("with cache keys count");
				firstMessage.Should().Contain(keysCount.ToString());
				break;
			}
			case (null, null):

				firstMessage.Should().Contain("Error happened during memcached");
				break;
		}
	}
}
