using Aer.Memcached.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Tests.Infrastructure.Logging;

internal class TestMemcachedMaintainerLoggerWarpper : TestLoggerWarpperBase, ILogger<MemcachedMaintainer<Pod>>
{
	public TestMemcachedMaintainerLoggerWarpper(ILogger logger) : base(logger)
	{ }
}
