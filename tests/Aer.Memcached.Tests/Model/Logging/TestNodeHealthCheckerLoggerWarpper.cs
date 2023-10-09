﻿using Aer.Memcached.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Tests.Model.Logging;

internal class TestNodeHealthCheckerLoggerWarpper : TestLoggerWarpperBase, ILogger<NodeHealthChecker<Pod>>
{
	public TestNodeHealthCheckerLoggerWarpper(ILogger logger) : base(logger)
	{ }
}
