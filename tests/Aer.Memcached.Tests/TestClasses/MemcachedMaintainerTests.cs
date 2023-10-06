using Aer.ConsistentHash;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Infrastructure;
using Aer.Memcached.Tests.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

public class MemcachedMaintainerTests
{
	private MemcachedMaintainer<Pod> _maintainer;
	
	public MemcachedMaintainerTests()
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
		var memcachedMaintainerLogger = loggerFactory.CreateLogger<MemcachedMaintainer<Pod>>();
		var nodeHealthCheckerLogger = loggerFactory.CreateLogger<NodeHealthChecker<Pod>>();

		var maintainerConfig = MemcachedConfiguration.MaintainerConfiguration.DefaultConfiguration();

		maintainerConfig.UseSocketPoolForNodeHealthChecks = true;
		
		var config = new MemcachedConfiguration()
		{
			Diagnostics = new MemcachedConfiguration.MemcachedDiagnosticsSettings()
			{
				DisableDiagnostics = true,
				DisableRebuildNodesStateLogging = true,
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

		_maintainer = new MemcachedMaintainer<Pod>(
			nodeProvider,
			nodeLocator,
			healthChecker,
			commandExecutor,
			options,
			memcachedMaintainerLogger);
	}

	[TestMethod]
	public void RunOnce()
	{ 
		_maintainer.RunOnce();
	}
}
