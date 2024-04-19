using System.Diagnostics;
using Aer.ConsistentHash;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Diagnostics;
using Aer.Memcached.Diagnostics.Listeners;
using Aer.Memcached.Infrastructure;
using Aer.Memcached.Tests.Infrastructure;
using Aer.Memcached.Tests.Infrastructure.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class MemcachedMaintainerTests
{
	private (
		MemcachedMaintainer<Pod> Maintainer,
		TestMemcachedMaintainerLoggerWrapper MaintainerLogger,
		TestNodeHealthCheckerLoggerWrapper NodeHealthCheckerLogger)
		GetMaintainerAndLoggers(bool useSocketPoolForNodeHealthChecks)
	{
		var hashCalculator = new HashCalculator();

		var nodeLocator = new HashRing<Pod>(hashCalculator);

		nodeLocator.AddNodes(
			new Pod("localhost", 11211),
			new Pod("localhost", 11212),
			new Pod("localhost", 11213)
		);

		var nodeProvider = new TestNodeProvider(nodeLocator.GetAllNodes());

		using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

		var commandExecutorLogger = loggerFactory.CreateLogger<CommandExecutor<Pod>>();
		
		var memcachedMaintainerLogger = new TestMemcachedMaintainerLoggerWrapper(
			loggerFactory.CreateLogger<
				MemcachedMaintainer<Pod>>());

		var nodeHealthCheckerLogger = new TestNodeHealthCheckerLoggerWrapper(
			loggerFactory.CreateLogger<
				NodeHealthChecker<Pod>>());

		var maintainerConfig = MemcachedConfiguration.MaintainerConfiguration.DefaultConfiguration();

		maintainerConfig.UseSocketPoolForNodeHealthChecks = useSocketPoolForNodeHealthChecks;

		var config = new MemcachedConfiguration()
		{
			Diagnostics = new MemcachedConfiguration.MemcachedDiagnosticsSettings()
			{
				DisableDiagnostics = false,
				DisableRebuildNodesStateLogging = false,
				DisableSocketPoolDiagnosticsLogging = false,
				SocketPoolDiagnosticsLoggingEventLevel = LogLevel.Information
			},
			MemcachedMaintainer = maintainerConfig
		};

		var options = new OptionsWrapper<MemcachedConfiguration>(config);

		var authProvider = new DefaultAuthenticationProvider(
			new OptionsWrapper<MemcachedConfiguration.AuthenticationCredentials>(config.MemcachedAuth));

		var commandExecutor = new CommandExecutor<Pod>(
			new OptionsWrapper<MemcachedConfiguration>(config),
			authProvider,
			commandExecutorLogger,
			nodeLocator);

		var healthChecker = new NodeHealthChecker<Pod>(options, nodeHealthCheckerLogger, commandExecutor);

		var maintainer = new MemcachedMaintainer<Pod>(
			nodeProvider,
			nodeLocator,
			healthChecker,
			commandExecutor,
			options,
			memcachedMaintainerLogger);

		// enable diagnostics
		var loggingListener = new LoggingMemcachedDiagnosticListener(
			loggerFactory.CreateLogger<LoggingMemcachedDiagnosticListener>(),
			options);
		DiagnosticListener diagnosticSource = MemcachedDiagnosticSource.Instance;
		
		diagnosticSource.SubscribeWithAdapter(loggingListener);

		return (maintainer, memcachedMaintainerLogger, nodeHealthCheckerLogger);
	}

	[TestMethod]
	public void RunMaintainer_UseSocketPoolForNodeHealthChecks()
	{
		var (maintainer, maintainerLogger, healthChecketLogger) =
			GetMaintainerAndLoggers(useSocketPoolForNodeHealthChecks: true);

		maintainer.RunOnce();
		maintainer.RunOnce();

		maintainerLogger.ErrorCount.Should().Be(0);
		maintainerLogger.WarningCount.Should().Be(0);

		healthChecketLogger.ErrorCount.Should().Be(0);
		healthChecketLogger.WarningCount.Should().Be(0);
	}

	[TestMethod]
	public void RunMaintainer_DontUseSocketPoolForNodeHealthChecks()
	{
		var (maintainer, maintainerLogger, healthChecketLogger) =
			GetMaintainerAndLoggers(useSocketPoolForNodeHealthChecks: false);

		maintainer.RunOnce();
		maintainer.RunOnce();

		maintainerLogger.ErrorCount.Should().Be(0);
		maintainerLogger.WarningCount.Should().Be(0);

		healthChecketLogger.ErrorCount.Should().Be(0);
		healthChecketLogger.WarningCount.Should().Be(0);
	}
}
