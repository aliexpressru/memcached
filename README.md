# Aer Memcached Client

[![Build Status](https://img.shields.io/endpoint.svg?url=https%3A%2F%2Factions-badge.atrox.dev%2Faliexpressru%2Fmemcached%2Fbadge%3Fref%3Dmain&style=flat)](https://actions-badge.atrox.dev/aliexpressru/memcached/goto?ref=main)
[![NuGet Release][package-image]][package-nuget-url]

## Features
- **Distributed Caching:** Easily scale your cache across multiple servers.
- **High Performance:** Efficiently handles large amounts of data with low latency.
- **Scalability:** Seamlessly increase cache capacity as your application grows.
- **Ease of Use:** Simple setup and configuration.
- **Multi-Target Support:** Supports both .NET 8.0 and .NET 10.0.

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
- `expirationTime` : the absolute expiration time for the key-value entry. If not set or set to `TimeSpan.Zero` or `TimeSpan.MaxValue`, cached value never expires
- `token` : the cancellation token
- `storeMode` : the mode under which the store operation is performed

```c#
Task MultiStoreAsync<T>(
   Dictionary<string, T> keyValues,
   TimeSpan? expirationTime,
   CancellationToken token,
   StoreMode storeMode = StoreMode.Set,
   BatchingOptions batchingOptions = null,
   IDictionary<string, TimeSpan?> expirationMap = null);
```

- `keyValues` : the entry key-value pairs to store
- `expirationTime` : the absolute expiration time for the key-value entry. If not set or set to `TimeSpan.Zero` or `TimeSpan.MaxValue`, cached value never expires
- `token` : the cancellation token
- `storeMode` : the mode under which the store operation is performed
- `batchingOptions` : optional batching options. The batching will be covered in the later part of this documentation
- `expirationMap` : individual key expirations that will be used instead expirationTime if provided

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

### GetAndTouch

Gets the value for the specified key and updates the expiration time of the entry stored in the cache. If value is not found by the key - `IsEmptyResult` is `true`

```c#
Task<MemcachedClientGetResult<T>> GetAndTouchAsync<T>(
    string key,
    TimeSpan? expirationTime,
    CancellationToken token);
```

- `key` : the key to get the value for
- `expirationTime` : the absolute expiration time for the key-value entry. If not set or set to `TimeSpan.Zero` or `TimeSpan.MaxValue`, cached value never expires
- `token` : the cancellation token

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

### Error logging

This library internally logs all exceptions that occur during memcached operations. Also both `MemcachedClientResult` and `MemcachedClientValueResult<T>` have a public property `Success` which is set to `false` if any error occured during command execution. You can inspect the `ErrorMessage` property to get error message.
To simplify the error logging, an extension method exist for each of the aforementioned return types.

```csharp
public static void LogErrorIfAny(
        this MemcachedClientResult target,
        ILogger logger,
        [CallerMemberName] string operationName = null)

public static void LogErrorIfAny<T>(
    this MemcachedClientValueResult<T> target,
    ILogger logger,
    int? cacheKeysCount,
    [CallerMemberName] string operationName = null)
```

#### Cancellation logging

By default cancellations and therefore `OperationCancelledException` occurrences are treated as any other exceptions and logged accordingly. But there are times when one just need to know that cancellation heppened and doesn't need a full stacktrace for it.

It's possible to switch on the _terse cancellaiton logging_ by setting `MemcachedConfiguration.IsTerseCancellationLogging` config key to `true`.

In that case only the operation name and a short message will be logged upon cancellation.

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

Be careful when using this parameter as it increases workload by `x replicationFactor`. You also should consider some tunings for memcached - see [Memcached tuning](#memcached-tuning) part of the README.

**Please note that `replicationFactor` parameter only present on multi-key versions of store, get and delte operations.**

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

Here is the example of custom serializer that uses MessagePack as an underlying serializer.

```csharp
internal class MyObjectBinarySerializer : IObjectBinarySerializer
{
    private int _serializationCount;
    private int _deserializationCount;

    public int SerializationsCount => _serializationCount;
    public int DeserializationsCount => _deserializationCount;

    public byte[] Serialize<T>(T value)
    {
        var data = MessagePackSerializer.Typeless.Serialize(value);

        Interlocked.Increment(ref _serializationCount);

        return data;
    }

    public T Deserialize<T>(byte[] serializedObject)
    {
        var deserializedObject = (T) MessagePackSerializer.Typeless.Deserialize(serializedObject);

        Interlocked.Increment(ref _deserializationCount);

        return deserializedObject;
    }
}
```

We register this serializer in DI as singleton.

```csharp
sc.AddSingleton<IObjectBinarySerializer, MyObjectBinarySerializer>();
```

And set the `BinarySerializerType` to `Custom`.

```json
{
  "MemcachedConfiguration": {
    "BinarySerializerType" : "Custom"
  }
}
````

#### Binary serializer change

When changing binary serializer type, previously serialized values might fail to deserialize. There is a transition period solution : delete undeserializable keys from memcached to refresh data. This option is not enabled by default to not conseal errorneous deployment consequnces.

To enable this option set the following configuration key.

```json
{
  "MemcachedConfiguration": {
    "IsDeleteMemcachedKeyOnDeserializationFail" : true
  }
}
```

Do not forget to set this option back to `false` or to delte it after the transition period is over.

## Restrictions && Limitations

Key must be less than 250 characters and value must be less than the value `-I` configured during memcached instance start.

The key length restriction can be lifted, see [Long keys support](#long-keys-support) section for the details.

If the value is greater than the configured `-I` value the memcached client won't throw an exception - it simply won't store a key without any notification.
Therefore, when trying to read such key - nothing will be returned.

Also, please note that this library utilizes a memcached item `flags` to store item type information to simplify and make deserialization faster.
This implies that one is not advised to use this library to read the data from memcached that you didn't put there using this library - it might be deserialized incorrectly or not deserialized at all.

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

`MemcachedClient` writes RT and RPS metrics to either Prometheus or OpenTelemetry diagnostics. To disable metrics specify:

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

You can select which metrics provider will be used by setting `MetricsProviderName` to either `Prometheus` or `OpenTelemetry` as follows:

```json
{
  "MemcachedConfiguration": {
    "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local",
    "Diagnostics":{    
      "MetricsProviderName" : "Prometheus"
    }
  }
}
```

**`MetricsProviderName` values**

- `Prometheus`: use Prometheus metrics
- `OpenTelemetry`: use OpenTelemetry metrics

This library exposes the following memcached metrics

- `memcached_command_duration_seconds` - memcached command duration in seconds per command
- `memcached_socket_pool_used_sockets` - number of used socket pool sockets per endpoint
- `memcached_commands_total` - total executed memcached commands number
- `memcached_socket_pool_exhausted_state` - socket pool exhaustion state per endpoint (1 = exhausted, absent = ok). **Note:** This metric is only available for OpenTelemetry. When a socket pool reaches its maximum capacity and cannot serve new requests, this metric is set to 1. Once the pool recovers and can serve requests again, the metric is removed.
- `memcached_socket_unread_data_detected_total` - counter of unread data detections on sockets per endpoint. Indicates sockets left in invalid state (e.g., after timeout or error). Available for both OpenTelemetry and Prometheus.

#### Distributed Tracing

OpenTelemetry distributed tracing provides visibility into memcached operations. Tracing is disabled by default.

**Enable tracing in `appsettings.json`:**

```json
{
  "MemcachedConfiguration": {
    "Diagnostics": {
      "EnableTracing": true
    }
  }
}
```

**Configure OpenTelemetry exporter:**

```csharp
builder.Services.AddMemcached(builder.Configuration);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("your-service-name"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            });
    });
```

**Features:**
- Follows OpenTelemetry semantic conventions for database operations
- Traces all memcached commands with operation name, server address, and replica information
- Works with Jaeger, Zipkin, Azure Application Insights, AWS X-Ray, and other OTLP-compatible backends

#### TracingOptions - Per-Operation Tracing Control

All memcached operations support optional `TracingOptions` parameter for granular tracing control.

**Example:**

```csharp
// Disable tracing for fire-and-forget operation
_ = Task.Run(async () => 
{
    await _memcachedClient.MultiStoreAsync(
        keyValues: data,
        expirationTime: TimeSpan.FromMinutes(30),
        token: CancellationToken.None,
        tracingOptions: TracingOptions.Disabled
    );
});
```

**Options:**
- `TracingOptions.Enabled` - Force enable tracing
- `TracingOptions.Disabled` - Force disable tracing
- `null` (default) - Use global `EnableTracing` setting

**Note:** Global `EnableTracing = false` disables tracing regardless of `TracingOptions`.

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
      "NodesHealthCheckPeriod": "00:01:00",
      "NodeHealthCheckEnabled": true,
      "MaxDegreeOfParallelism": -1,
      "UseSocketPoolForNodeHealthChecks" : true,
      "MaintainerCyclesToCloseSocketAfter" : 2,
      "NumberOfSocketsToClosePerPool" : 2
    }
  }
}
```

**`MemcachedMaintainer` settings**

- `NodesRebuildingPeriod`: Period to rebuild nodes using dns lookup by Headless Service
- `NodesHealthCheckPeriod`: Period to check if nodes are responsive. If node is not responded during `SocketPool.ConnectionTimeout` it is marked as dead and will be deleted from memcached nodes until it is responsive again. Default value is `00:01:00` (1 minute)
- `NodeHealthCheckEnabled`: Enables health check of nodes to remove dead nodes. Default value is `true`
- `MaxDegreeOfParallelism`: Maximum degree of parallelism for node health checks. Set to `-1` to use `Environment.ProcessorCount`, or specify a positive number to set a fixed thread count. Default value is `-1`
- `UseSocketPoolForNodeHealthChecks`: If set to `true` node health checker mechanism should use socket pool to obtain sockets for nodes health checks. If set to `false`, new non-pooled socket will be created for each node health check
- `MaintainerCyclesToCloseSocketAfter`: Number of memcached maintainer cycles to close `NumberOfSocketsToClosePerPool` (see later) socket connections after. The sockets are going to be destroyed on the next maintainer cycle after the specified number
- `NumberOfSocketsToClosePerPool`: Number of sockets to close per pool on each `MaintainerCyclesToCloseSocketAfter`

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

In case you need consistent cache across clusters or data centers you can use cache synchronization feature of this library.

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
            },
            "SyncEndpointsAuthAllowAnonymous": false
        }
    }
}

```

`DefaultSyncServersProvider` is used as default and can be replaced with your own implementation.
By default sync servers are obtained from `SyncServers` array and filtered by name of a cluster that is specified in `ClusterNameEnvVariable` to prevent a service instnace in one cluster from syncing cache to itself.

- `RetryCount` - number of retries to sync data to servers. Default value is `3`.
- `TimeToSync` - time before sync attempt is cancelled. Default value is `00:00:01`.

`CacheSyncCircuitBreaker` allows to switch off synchronization if there are too many errors

- `Interval` time interval to count errors
- `MaxErrors` maximum number of errors in `Interval` by instance
- `SwitchOffTime` time of synchronization switch off

Also you must add memcached endpoints in `Configure` method of your `Startup` class.

```c#
app.UseEndpoints(endpoints =>
        {
            endpoints.AddMemcachedEndpoints(this.Configuration);
            endpoints.MapControllers();
        });
```

`Configuration` argument here is a property on a `Startup` instance

If you have implicit authorization configured for your service you can allow anonymous access to sync endpoints by setting `MemcachedConfiguration.SyncSettings.SyncEndpointsAuthAllowAnonymous` to `true`.

When using cache synchronization feature, the `MemcachedClientResult.SyncSuccess` property can be inspected to determine whether the sync operation succeeded. When cache synchronization is not used this property is set to `false`.

To check whether the cache synchronization is configured and enabled call the `IMemcachedClient.IsCacheSyncEnabled` method.

#### BatchingOptions support in cache sync

Cache synchronization now supports `BatchingOptions` for `MultiStoreAsync` operations. When `BatchingOptions` is specified, it will be passed to the remote cluster where the actual batching will occur during the `MultiStoreAsync` execution on the target cluster.

This is particularly useful when syncing large numbers of key-value pairs across clusters, as it allows the target cluster to process the data in optimized batches.

```c#
await _client.MultiStoreAsync(
    keyValues,
    TimeSpan.FromSeconds(10),
    CancellationToken.None,
    batchingOptions: new BatchingOptions
    {
        BatchSize = 100,
        MaxDegreeOfParallelism = 4
    });
```

The `BatchingOptions` are automatically included in the `CacheSyncModel` and will be used by the receiving cluster for efficient batch processing.

### Long keys support

Memcached has a by-design restriction on the key length - 250 bytes (or in most cases 250 characters).

This library has a mechanism to circumvent this restriction. If the following option is set

```json
{
  "MemcachedConfiguration": {
    "IsAllowLongKeys" : true
  }
}
```

then on each operation the key byte size is calculated and, if it exceeds the 250-byte limit, the key is hashed using `xxHash128` algorithm to a fixed length string.

Please note that all hashing algorithms, icluding `xxHash128`, has a potential to get a collision - same hash for two different inputs. Enabling aforementioned option might lead to unforeseen consequences, if the long to short keys ratio is considerable. Therefore it is recommended to enable `IsAllowLongKeys` option only if the long keys count is low.

Nonetheless, it worth noting that every case for this option should be considered and tested individually.

## Monitoring

Other than logs check Prometheus \ OpenTelemetry metrics.

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
