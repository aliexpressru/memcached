# Aer Memcached Client

This solution allows to easily add memcached to a service

---

## Configuration

To use the library add MemcachedConfiguration section to appsettings.json as follows:

```json
{
  "MemcachedConfiguration": {
    "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local"
  }
}
```

`HeadlessServiceAddress` groups all memcached pods. Using dns lookup all the ip addresses of the pods can be obtained.

For local settings or if you static amount of pods you can specify `Servers` instead of `HeadlessServiceAddress`:

```json
{
    "MemcachedConfiguration": {
        "Servers": [
            {
                "IpAddress": "1.1.1.1"
            },
            {
                "IpAddress": "2.2.2.2"
            },
            {
                "IpAddress": "3.3.3.3"
            }
        ]
    }
}
```

In case you have only one instance deployed in k8s specify consistent dns name of a pod:
```json
{
  "MemcachedConfiguration": {
    "Servers": [
      {
        "IpAddress": "my-memchached.namespace.svc.cluster.local"
      }
    ]
  }
}
```


# How to use

```c#
public void ConfigureServices(IServiceCollection services)
{
    ...
    
    services.AddMemcached(Configuration);
}
```

Then inject `IMemcachedClient` whenever you need it.

Web API example:
```c#
using Aer.Memcached.Client.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Aer.Memcached.WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class MemcachedController : ControllerBase
{
    private readonly IMemcachedClient _memcachedClient;

    public MemcachedController(IMemcachedClient memcachedClient)
    {
        _memcachedClient = memcachedClient;
    }

    [HttpGet(Name = "GetByKey")]
    public async Task<string> Get(string key)
    {
        var getResult = await _memcachedClient.GetAsync<string>(key, CancellationToken.None);
        var value = getResult.Result;

        if (value == null)
        {
            await _memcachedClient.StoreAsync(
                key, 
                Guid.NewGuid().ToString(), 
                TimeSpan.FromSeconds(30), 
                CancellationToken.None);
        }

        return value;
    }
}
```

Console example:
```c#
var hashCalculator = new HashCalculator();
var nodeLocator = new HashRing<Pod>(hashCalculator);
nodeLocator.AddNodes(new Pod[]
{
    new()
    {
        IpAddress = "localhost"
    }
});

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var commandExecutorLogger = loggerFactory.CreateLogger<CommandExecutor<Pod>>();

var config = new MemcachedConfiguration();
var authProvider = new DefaultAuthenticationProvider(new OptionsWrapper<MemcachedConfiguration.AuthenticationCredentials>(config.MemcachedAuth));

var client = new MemcachedClient<Pod>(
    nodeLocator, 
    new CommandExecutor<Pod>(new OptionsWrapper<MemcachedConfiguration>(config), authProvider, commandExecutorLogger));

await client.MultiStoreAsync(keyValues, TimeSpan.FromMinutes(10), CancellationToken.None);
var result = await client.MultiGetAsync<string>(keyValues.Keys.ToArray(), CancellationToken.None);

foreach (var keyValue in keyValues)
{
    if (!result.ContainsKey(keyValue.Key))
    {
        Console.WriteLine($"value with key: {keyValue.Key} not found.");
    }
}
```

To enable diagnostics:
```c#
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    ...

    app.EnableMemcachedDiagnostics(Configuration);
}
```

# Restrictions

Key must be less than 250 characters and value must be less than 1MB of data.

# Additional configuration


To use SASL specify `MemcachedAuth` section in config:
```json
{
  "MemcachedConfiguration": {
    "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local",
    "MemcachedAuth": {
      "Username": "mmchdadmin",
      "Password": "pass"
    }
  }
}
```


By default `MemcachedClient` writes RT and RPS metrics to diagnostics. To disable it specify:
```json
{
  "MemcachedConfiguration": {
    "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local",
    "Diagnostics": {
      "DisableDiagnostics": true
    }
  }
}
```

By default `MemcachedClient` writes memcached nodes rebuild process state to diagnostics. To disable it specify:

```json
{
  "MemcachedConfiguration": {
    "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local",
    "Diagnostics": {
      "DisableRebuildNodesStateLogging": true
    }
  }
}
```

Socket pool and maintainer have default configuration.
If you need to tune it, add the following sections in config:
```json
{
  "MemcachedConfiguration": {
    "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local",
    "SocketPool": {
      "ConnectionTimeout": "00:00:01",
      "ReceiveTimeout": "00:00:01",
      "SocketPoolingTimeout": "00:00:00.150",
      "MaxPoolSize": 100
    },
    "MemcachedMaintainer": {
      "NodesRebuildingPeriod": "00:00:15",
      "NodesHealthCheckPeriod": "00:00:15",
      "NodeHealthCheckEnabled": true
    }
  }
}
```

SocketPool settings:

`ConnectionTimeout`: Amount of time after which the connection attempt will fail

`ReceiveTimeout`: Amount of time after which receiving data from the socket will fail

`SocketPoolingTimeout`: Amount of time to acquire socket from pool

`MaxPoolSize`: Maximum amount of sockets per memcached instance in the socket pool

MemcachedMaintainer settings:

`NodesRebuildingPeriod`: Period to rebuild nodes using dns lookup by Headless Service

`NodesHealthCheckPeriod`: Period to check if nodes are responsive. If node is not responded during `SocketPool.ConnectionTimeout` it is marked as dead and will be deleted from memcached nodes until it is responsive again.


# Monitoring
Other than logs check Prometheus metrics.

To check if there are any unsuccesful commands: problems with connection, pool is run out of sockets, etc. :
`sum(rate(memcached_commands_total{app_kubernetes_io_instance="$instance",kube_cluster=~"$cluster",is_successful="0"}[1m])) by (command_name)`

RPS:
`sum(irate(memcached_commands_total{app_kubernetes_io_instance="$instance",kube_cluster=~"$cluster"}[$__interval])) by (command_name)`

RT:
` histogram_quantile(0.9, sum by (le, command_name) (rate(memcached_command_duration_seconds_bucket{app_kubernetes_io_instance=~"$instance",kube_cluster=~"$cluster"}[$__interval])))`

# Docker compose

```yaml
version: "3"

services:
  memcached:
    image: memcached
    restart: always
    container_name: memcached
    command: ["-m", "128"]  
    ports:  
      - 11211:11211
```