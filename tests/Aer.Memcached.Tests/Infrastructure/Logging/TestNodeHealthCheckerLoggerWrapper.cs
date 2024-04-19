using Aer.Memcached.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Tests.Infrastructure.Logging;

internal class TestNodeHealthCheckerLoggerWrapper : TestLoggerWrapperBase, ILogger<NodeHealthChecker<Pod>>
{
	public TestNodeHealthCheckerLoggerWrapper(ILogger logger) : base(logger)
	{ }
}
