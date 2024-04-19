using Aer.Memcached.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Tests.Infrastructure.Logging;

internal class TestMemcachedMaintainerLoggerWrapper : TestLoggerWrapperBase, ILogger<MemcachedMaintainer<Pod>>
{
	public TestMemcachedMaintainerLoggerWrapper(ILogger logger) : base(logger)
	{ }
}
