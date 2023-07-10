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

For `MultiStoreAsync` and `MultiGetAsync` method there is an optional argument `batchingOptions`. If this argument is specified the store and get operations split input key or key-value collection into batches an processe every batch on every memcached node in parallel with specified maximum degree of parallelism (`Environment.ProcessorCount` by default).
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
    "HeadlessServiceAddress": "my-memchached-service-headless.namespace.svc.cluster.local"
  },
  "MemcachedAuth": {
      "Username": "mmchdadmin",
      "Password": "pass"
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

# Memcached tuning

Consider setting memory limit to have all your data in cache and avoid unneccessary evictions.
Also make sure that number of connections is enough, it will spike in moment of redeployment.

```
# MaxMemoryLimit, this should be less than the resources.limits.memory, or memcached will crash.  Default is 64MB
- -m 8192 
# Specify the maximum number of simultaneous connections to the memcached service. The default is 1024. 
- -c 20000
```

In multi key client methods of the library there is a default `BatchSize = 15`, if you want to change it consider tuning the following parameter:
```
- -R 40
The command-line parameter -R is in charge of the maximum number of requests per 
network IO event (default value is 20). The application should adopt its batch 
size according to this parameter. Please note that the requests limit does not 
affect multi-key reads, or the number of keys per get request.
```

Each multi key client method requires addition noop operation so you need to set `-R` parameter as `BatchSize + 1`.

Otherwise you can encounter the limit:
```
STAT conn_yields 126672162
Number of times any connection yielded to another due to hitting the -R limit
```