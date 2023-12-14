# Aer Memcached Client

[![Build Status](https://img.shields.io/endpoint.svg?url=https%3A%2F%2Factions-badge.atrox.dev%2Faliexpressru%2Fmemcached%2Fbadge%3Fref%3Dmain&style=flat)](https://actions-badge.atrox.dev/aliexpressru/memcached/goto?ref=main)
[![NuGet Release][package-image]][package-nuget-url]

This solution allows to easily add memcached to a service

---

## Configuration

To use the library add `MemcachedConfiguration` section to appsettings.json as follows:

```json
{
  "MemcachedConfiguration": {
    "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local"
  }
}
```

`HeadlessServiceAddress` groups all memcached pods. Using dns lookup all the ip addresses of the pods can be obtained.

Default Memcached port is `11211`, but you can also specify it in config.

```json
{
  "MemcachedConfiguration": {
    "MemcachedPort": 12345
  }
}
```

`MemcachedPort` is an optional setting that specifies which port should each memcached node use. If not set the default value of `11211` used.

For local run or if you have a static amount and setup of pods you can specify `Servers` manually instead of seting the `HeadlessServiceAddress`:

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

Default Memcached port is `11211`, but you can also specify it in config

```json
{
  "MemcachedConfiguration": {
    "Servers": [
      {
        "IpAddress": "1.1.1.1",
        "Port": 12345
      }
    ]
  }
}
```

In case you have only one instance deployed in k8s specify consistent dns name of a pod.

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

Ypu can have both `HeadlessServiceAddress` and `Servers` nodes in configuration. In this case the resulting nodes will be collected from both headless service and static configuration.

## Usage

```c#
public void ConfigureServices(IServiceCollection services)
{
    ...
    
    services.AddMemcached(Configuration);
}
```

Then inject `IMemcachedClient` whenever you need it.

This client supports single-key `get`, `store`, `delete`, `inc`, `decr`, `flush` operaions. Multiple keys counterparts are available for `get`, `store` and `delete` operations.

### Store

Stores specified key-value of multiple key-value pairs to the cache.

```c#
Task<MemcachedClientResult> StoreAsync<T>(
   string key,
   T value,
   TimeSpan? expirationTime,
   CancellationToken token,
   StoreMode storeMode = StoreMode.Set);
```

- `key` : the entry key to store value for
- `value` : the value to store under specified key
- `expirationTime` : the absolute expiration time for the key-value entry
- `token` : the cancellation token
- `storeMode` : the mode under which the store operation is performed

```c#
Task MultiStoreAsync<T>(
   Dictionary<string, T> keyValues,
   TimeSpan? expirationTime,
   CancellationToken token,
   StoreMode storeMode = StoreMode.Set,
   BatchingOptions batchingOptions = null);
```

- `keyValues` : the entry key-value pairs to store
- `expirationTime` : the absolute expiration time for the key-value entry
- `token` : the cancellation token
- `storeMode` : the mode under which the store operation is performed
- `batchingOptions` : optional batching options. The batching will be covered in the later part of this documentation

### Get

Gets the value for the specified key or set of keys from cache. If value is not found by the key - `IsEmptyResult` is `true`
In MultiGet scenarion the key in dictionary is absent.

```c#
Task<MemcachedClientGetResult<T>> GetAsync<T>(
    string key, 
    CancellationToken token);
```

- `key` : the key to get the value for
- `token` : the cancellation token

```c#
Task<IDictionary<string, T>> MultiGetAsync<T>(
    IEnumerable<string> keys, 
    CancellationToken token,
    BatchingOptions batchingOptions = null);
```

- `keys` : the keys to get values for
- `token` : the cancellation token
- `batchingOptions` : optional batching options. The batching will be covered in the later part of this documentation

### Delete

Deletes the value for the specified key or set of keys from cache.

```c#
Task<MemcachedClientResult> DeleteAsync(string key, CancellationToken token);
```

- `key` : the key to delete the value for
- `token` : the cancellation token

```c#
Task MultiDeleteAsync(
    IEnumerable<string> keys,
    CancellationToken token,
    BatchingOptions batchingOptions = null);
```

- `keys` : the keys to delete values for
- `token` : the cancellation token
- `batchingOptions` : optional batching options. The batching will be covered in the later part of this documentation

### Incr/Decr

```c#
Task<MemcachedClientValueResult<ulong>> IncrAsync(
    string key,
    ulong amountToAdd,
    ulong initialValue,
    TimeSpan? expirationTime,
    CancellationToken token);
```

- `key` : the key to increment value for
- `amountToAdd` : amount to add to stored value
- `initialValue` : initial value if value does not exist
- `expirationTime` : the absolute expiration time for the key-value entry
- `token` : the cancellation token

```c#
Task<MemcachedClientValueResult<ulong>> DecrAsync(
    string key,
    ulong amountToSubtract,
    ulong initialValue,
    TimeSpan? expirationTime,
    CancellationToken token);
```

- `key` : the key to increment value for
- `amountToSubtract` : amount to subtract from stored value
- `initialValue` : initial value if value does not exist
- `expirationTime` : the absolute expiration time for the key-value entry
- `token` : the cancellation token

### Flush

Clears the cache on all the memcached cluster nodes.

```c#
Task FlushAsync(CancellationToken token);
```

### Batching

For `MultiStoreAsync` and `MultiGetAsync` methods there is an optional argument `batchingOptions`. If this argument is specified the store and get operations split input key or key-value collection into batches an processe every batch on every memcached node in parallel with specified maximum degree of parallelism (`Environment.ProcessorCount` by default).
Batching is requered to lower the load on memcached nodes in high-load scenarios when keys are long and values are even longer than keys. There is an empyric default value of batch size : `20` which is optimal for the most cases.
By default batching is off for all the operations.

To use the dafault values simply pass the new instance :

```c#
await _client.MultiStoreAsync(
    keyValues,
    TimeSpan.FromSeconds(10),
    CancellationToken.None,
    batchingOptions: new BatchingOptions());
```

To configure the batch size for one physical memcached operation or change the maximum degree of parallelism - change the batchingOptions properties accordingly.

```c#
new BatchingOptions(){
    BatchSize = 100,
    MaxDegreeOfParallelism = 4
}
```

Note that the batch size lower than zero is invalid and `MaxDegreeOfParallelism` lower then zero is equivalent to `Environment.ProcessorCount`.

### Replication

For `MultiStoreAsync`, `MultiGetAsync` and `MultiDeleteAsync` methods there is an optional argument `replicationFactor`. If `replicationFactor > 0` a key will be stored on `replicationFactor` additional physical nodes clockwise from initial one chosen by hash. In case of `MultiGetAsync` if you need replica fallback you can specify `replicationFactor = 1` even if `replicationFactor` for `MultiStoreAsync` is more than 1. While physical node does not respond but still is on HashRing `MultiGetAsync` command will try to fetch data from both initial node and it's replica. When request to initial node is cancelled you still have data from it's replica. In case the broken node is already removed from HashRing you will have some probability to hit it's replica and with higher `replicationFactor` probability is higher as well.

Be careful using this parameter as it increases workload by `x replicationFactor`. You also should consider some tunings for memcached - see [Memcached tuning](#memcached-tuning) part of the README.

### Serialization

Since Memcached requires data to be binary serialized before storing it, we utilize various binary serializers to perform the serialization.

We use hand-written plain binary serializer to serialize primitive types, but with complex user-defined types the matters are a bit more complex.

Currently there is no universal binary serializer that can handle all the possible type heirarchies while not requiring annotating stored types with some kind of attributes (contractless).

Some examples.

The BSON serializer is contractless but can't handle `DateTimeOffset` values or dictionaries with non-primitive keys without writnig custom converters.

The MessagePack serizlizer can be contractless, can handle all kinds of types but can't handle reference loops (when object has a property that is either a direct or an indirect reference to the object itself).

Protobuf serializer can handle reference loops, all types of objects but can't be contractless and creating contracts at run time using reflection is slow.

So we've decided to give the end user the ability to choose the serializer and if she does not like neither of the provided options - add a custom one.

The type of the serializer is configured as follows.

```json
{
  "MemcachedConfiguration": {
    "BinarySerializerType" : "Bson"
  }
}
```

The `ObjectBinarySerializerType` can have the following values.

- `Bson` - The default binary object serializer, it was used since v1 and is pretty good default, unless you need to store dictionaries with non-primitive keys.
- `MessagePack` - Fast, can handle any type, but can't handle reference loops.
- `Custom` - this option indicates that the serializer will be provided by the end user.

If the `Custom` serializer type is selected then the library will search the DI container for a type that implements `IObjectBinarySerializer` and use it as a serializer implementation.

## Restrictions

Key must be less than 250 characters and value must be less than 1MB of data.

## Additional configuration

### SASL

To use SASL specify `MemcachedAuth` section in config:

```json
{
  "MemcachedConfiguration": {
    "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local"
  },
  "MemcachedAuth": {
      "Username": "mmchdadmin",
      "Password": "pass"
  }
}
```

### Metrics && Disagnostics

This library utilizes diagnostic source to write out metrics and diagnostic data. But the diagnostcs data output is switched off by default.

To enable metrics and diagnostics use tho following DI extension method in `Startup` class in `Configure` method.

```c#
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // some previous calls here
        app.EnableMemcachedDiagnostics(configuration);
    }

```

#### Metrics

`MemcachedClient` writes RT and RPS metrics to diagnostics. To disable it specify:

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

#### Disagnostic information

`MemcachedClient` writes memcached nodes rebuild process state to diagnostics. This state includes the nodes that are currently in use and socket pools statistics. To disable this data logging specify:

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

`MemcachedClient` writes socket pool diagnostic data. This data includes events for the socket being created and destroyed. By default this data is written out as `LogLevel.Information` log events. To disable this diagnostics or configure event level use the following settings.

```json
{
  "MemcachedConfiguration": {
    "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local",
    "Diagnostics": {
      "DisableSocketPoolDiagnosticsLogging": true, // this disables socket pool diagnostics entirely
      "SocketPoolDiagnosticsLoggingEventLevel" : "Debug"
    }
  }
}
```

### Socket Pooling

Socket pool has a default configuration.
If you need to tune it, add the following sections in config:

```json
{
  "MemcachedConfiguration": {
    "SocketPool": {
      "ConnectionTimeout": "00:00:01",
      "ReceiveTimeout": "00:00:01",
      "SocketPoolingTimeout": "00:00:00.150",
      "MaxPoolSize": 100
    }
  }
}
```

**`SocketPool` settings**

- `ConnectionTimeout`: Amount of time after which the connection attempt will fail
- `ReceiveTimeout`: Amount of time after which receiving data from the socket will fail
- `SocketPoolingTimeout`: Amount of time to acquire socket from pool
- `MaxPoolSize`: Maximum amount of sockets per memcached instance in the socket pool
- `MaximumSocketCreationAttempts`: Maximum number of attempts to create a socket before the associated SocketPool is considered poisoned and its underlying endpoint broken

### Maintenance background service

Maintainer service has a default configuration.
If you need to tune it, add the following sections in config:

```json
{
  "MemcachedConfiguration": {
    "MemcachedMaintainer": {
      "NodesRebuildingPeriod": "00:00:15",
      "NodesHealthCheckPeriod": "00:00:15",
      "NodeHealthCheckEnabled": true,
      "UseSocketPoolForNodeHealthChecks" : true
    }
  }
}
```

**`MemcachedMaintainer` settings**

- `NodesRebuildingPeriod`: Period to rebuild nodes using dns lookup by Headless Service
- `NodesHealthCheckPeriod`: Period to check if nodes are responsive. If node is not responded during `SocketPool.ConnectionTimeout` it is marked as dead and will be deleted from memcached nodes until it is responsive again
- `UseSocketPoolForNodeHealthChecks` : If set to `true` node health checker mechanism should use socket pool to obtain sockets for nodes health checks. If set to `false`, new non-pooled socket will be created for each node health check

### Jitter

Initial number of seconds is got by last digits of calculated hash. Number of digits depends on `SpreadFactor`, be default it is the remainder of the division by `SpreadFactor` = 2 digits. Then it is multiplied by this factor to get final expiration time. `MultiplicationFactor` = 1 makes jitter in a range of 0 to 99 seconds. `MultiplicationFactor` = 10 makes jitter in a range of 0 to 990 seconds (0, 10, 20, ..., 990) etc.

```json
{
  "MemcachedConfiguration": {
    "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local",
    "ExpirationJitter": {
      "MultiplicationFactor": 1,
      "SpreadFactor": 100
    }
  }
}
```

### Cache sync

In case you need consistent cache across clusters or data centers

```json
{
    "MemcachedConfiguration": {
        "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local",
        "SyncSettings": {
            "SyncServers": [
                {
                    "Address": "http://my-service.cluster1.k8s.net",
                    "ClusterName": "cluster1"
                },
                {
                    "Address": "http://my-service.cluster2.k8s.net",
                    "ClusterName": "cluster2"
                }
            ],
            "ClusterNameEnvVariable": "MY_ENV_VAR_FOR_CLUSTER_NAME",
            "RetryCount": 3,
            "TimeToSync": "00:00:01",
            "CacheSyncCircuitBreaker": {
                "Interval": "00:01:00",
                "MaxErrors": 50,
                "SwitchOffTime": "00:02:00"
            }
        }
    }
}

```

`DefaultSyncServersProvider` is used as default and can be replaced with your own implementation.
By default sync servers are got from `SyncServers` array and filtered by name of a cluster that is specified in `ClusterNameEnvVariable` to avoid requesting service itself.
- `RetryCount` equals `3` by default if it's not specified. Number of retries to sync data to servers.
- `TimeToSync` equals `00:00:01` by default if it's not specified. Time before sync is cancelled.

`CacheSyncCircuitBreaker` allows to switch off synchronization if there are too many errors
- `Interval` time interval to count errors
- `MaxErrors` maximum number of errors in `Interval` by instance
- `SwitchOffTime` time of synchronization switch off

Also you must add sync endpoints
```c#
app.UseEndpoints(endpoints =>
        {
            endpoints.AddMemcachedSyncEndpoint<string>(builder.Configuration);
            endpoints.AddMemcachedSyncEndpoint<ComplexModel>(builder.Configuration);
            endpoints.AddMemcachedEndpoints(builder.Configuration);
            endpoints.MapControllers();
        });
```

`AddMemcachedSyncEndpoint` - to store data
`AddMemcachedEndpoints` - for delete and flush endpoints

## Monitoring

Other than logs check Prometheus metrics.

To check if there are any unsuccesful commands: problems with connection, pool is run out of sockets, etc. :
`sum(rate(memcached_commands_total{app_kubernetes_io_instance="$instance",kube_cluster=~"$cluster",is_successful="0"}[1m])) by (command_name)`

RPS:
`sum(irate(memcached_commands_total{app_kubernetes_io_instance="$instance",kube_cluster=~"$cluster"}[$__interval])) by (command_name)`

RT:
`histogram_quantile(0.9, sum by (le, command_name) (rate(memcached_command_duration_seconds_bucket{app_kubernetes_io_instance=~"$instance",kube_cluster=~"$cluster"}[$__interval])))`

## Docker compose

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

## Memcached tuning

Consider setting memory limit to have all your data in cache and avoid unneccessary evictions.
Also make sure that number of connections is enough, it will spike in moment of redeployment.

```plaintext
# MaxMemoryLimit, this should be less than the resources.limits.memory, or memcached will crash. Default is 64MB
- -m 8192 
# Specify the maximum number of simultaneous connections to the memcached service. The default is 1024. 
- -c 20000
```

In multi key client methods of the library there is a default `BatchSize = 15`, if you want to change it consider tuning the following parameter:

```plaintext
- -R 40
The command-line parameter -R is in charge of the maximum number of requests per 
network IO event (default value is 20). The application should adopt its batch 
size according to this parameter. Please note that the requests limit does not 
affect multi-key reads, or the number of keys per get request.
```

Each multi key client method requires addition noop operation so you need to set `-R` parameter as `BatchSize + 1`.

Otherwise you can encounter the limit:

```plaintext
STAT conn_yields 126672162
Number of times any connection yielded to another due to hitting the -R limit
```

[package-nuget-url]:https://www.nuget.org/packages/Aerx.Memcached.Client/
[package-image]:
https://img.shields.io/nuget/v/Aerx.Memcached.Client.svg