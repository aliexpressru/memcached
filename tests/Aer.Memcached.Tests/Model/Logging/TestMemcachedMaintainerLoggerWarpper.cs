using Aer.Memcached.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Tests.Model.Logging;

internal class TestMemcachedMaintainerLoggerWarpper : TestLoggerWarpperBase, ILogger<MemcachedMaintainer<Pod>>
{
	public TestMemcachedMaintainerLoggerWarpper(ILogger logger) : base(logger)
	{ }
}
