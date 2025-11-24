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
		GetMaintainerAndLoggers(
			bool useSocketPoolForNodeHealthChecks, 
			int numberOfMaintainerCyclesToCloseSocketAfter = 0,
			int numberOfSocketsToClosePerPool = 1)
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
		maintainerConfig.MaintainerCyclesToCloseSocketAfter = numberOfMaintainerCyclesToCloseSocketAfter;
		maintainerConfig.NumberOfSocketsToClosePerPool = numberOfSocketsToClosePerPool;

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
	public async Task RunMaintainer_UseSocketPoolForNodeHealthChecks()
	{
		var (maintainer, maintainerLogger, healthCheckerLogger) =
			GetMaintainerAndLoggers(useSocketPoolForNodeHealthChecks: true);

		await maintainer.RunOnceAsync();
		await maintainer.RunOnceAsync();

		maintainerLogger.LoggedMessages.Count(
				m => m.Contains(
					"Going to destroy 1 pooled sockets"))
			.Should().Be(2);

		maintainerLogger.ErrorCount.Should().Be(0);
		maintainerLogger.WarningCount.Should().Be(0);

		healthCheckerLogger.ErrorCount.Should().Be(0);
		healthCheckerLogger.WarningCount.Should().Be(0);
	}

	[TestMethod]
	public async Task RunMaintainer_DontUseSocketPoolForNodeHealthChecks()
	{
		var (maintainer, maintainerLogger, healthCheckerLogger) =
			GetMaintainerAndLoggers(useSocketPoolForNodeHealthChecks: false);

		await maintainer.RunOnceAsync();
		await maintainer.RunOnceAsync();

		maintainerLogger.LoggedMessages.Count(
				m => m.Contains(
					"Going to destroy 1 pooled sockets"))
			.Should().Be(2);

		maintainerLogger.ErrorCount.Should().Be(0);
		maintainerLogger.WarningCount.Should().Be(0);

		healthCheckerLogger.ErrorCount.Should().Be(0);
		healthCheckerLogger.WarningCount.Should().Be(0);
	}

	[TestMethod]
	public async Task SocketClosesAfterMaintainerCycles_DontCloseSockets()
	{
		var numbeOfSocketsToDestroy = 2;
		
		var (maintainer, maintainerLogger, _) =
			GetMaintainerAndLoggers(
				useSocketPoolForNodeHealthChecks: false,
				numberOfMaintainerCyclesToCloseSocketAfter: 2,
				numberOfSocketsToClosePerPool: numbeOfSocketsToDestroy);

		await maintainer.RunOnceAsync();

		maintainerLogger.LoggedMessages
			.All(m => m.Contains($"Going to destroy {numbeOfSocketsToDestroy} pooled sockets"))
			.Should().BeFalse();

		maintainerLogger.ErrorCount.Should().Be(0);
		maintainerLogger.WarningCount.Should().Be(0);
	}

	[TestMethod]
	public async Task SocketClosesAfterMaintainerCycles_ShouldCloseSpecifiedNumberOfSockets()
	{
		var numberOfDestructionCycles = 2;
		var numbeOfSocketsToDestroy = 2;
		var numbeOfCyclesAfterWhichToToDestroySockets = 2;
		
		var (maintainer, maintainerLogger, _) =
			GetMaintainerAndLoggers(
				useSocketPoolForNodeHealthChecks: false,
				numberOfMaintainerCyclesToCloseSocketAfter: numbeOfCyclesAfterWhichToToDestroySockets,
				numberOfSocketsToClosePerPool: numbeOfSocketsToDestroy);

		for (int i = 0; i <= numbeOfSocketsToDestroy * numberOfDestructionCycles + 1; i++)
		{
			await maintainer.RunOnceAsync();
		}
		
		maintainerLogger.LoggedMessages.Count(
			m => m.Contains(
				$"Going to destroy {numbeOfSocketsToDestroy} pooled sockets"))
			.Should().Be(numberOfDestructionCycles);

		maintainerLogger.ErrorCount.Should().Be(0);
		maintainerLogger.WarningCount.Should().Be(0);
	}
}
