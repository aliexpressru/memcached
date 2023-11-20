using Aer.ConsistentHash;
using Aer.Memcached;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var hashCalculator = new HashCalculator();
var nodeLocator = new HashRing<Pod>(hashCalculator);
nodeLocator.AddNodes(new Pod("localhost"));

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var commandExecutorLogger = loggerFactory.CreateLogger<CommandExecutor<Pod>>();

var config = new MemcachedConfiguration();
var authProvider = new DefaultAuthenticationProvider(new OptionsWrapper<MemcachedConfiguration.AuthenticationCredentials>(config.MemcachedAuth));

var expirationCalculator = new ExpirationCalculator(hashCalculator, new OptionsWrapper<MemcachedConfiguration>(config));

var client = new MemcachedClient<Pod>(
    nodeLocator,
    new CommandExecutor<Pod>(
        new OptionsWrapper<MemcachedConfiguration>(config),
        authProvider,
        commandExecutorLogger,
        nodeLocator),
    expirationCalculator,
    null);

try
{
    var key = new string('*', 251);
    await client.StoreAsync(key, "testMaxLength", TimeSpan.FromSeconds(10), CancellationToken.None);

    var getValue = await client.GetAsync<string>(key, CancellationToken.None);
    Console.WriteLine(getValue.Result);
}
catch (Exception e)
{
    Console.WriteLine(e);
}

var keyForBigValue = Guid.NewGuid().ToString();
var bigValue = new string('1', 1024);

await client.StoreAsync(keyForBigValue, bigValue, TimeSpan.FromSeconds(10), CancellationToken.None);

var getBigValue = await client.GetAsync<string>(keyForBigValue, CancellationToken.None);
Console.WriteLine(getBigValue.Result);

var keyValues = new Dictionary<string, string>();

foreach (var _ in Enumerable.Range(0, 5))
{
    keyValues[Guid.NewGuid().ToString()] = Guid.NewGuid().ToString();
}

await client.MultiStoreAsync(keyValues, TimeSpan.FromMinutes(10), CancellationToken.None);
var result = await client.MultiGetAsync<string>(keyValues.Keys.ToArray(), CancellationToken.None);

foreach (var keyValue in keyValues)
{
    if (!result.ContainsKey(keyValue.Key))
    {
        Console.WriteLine($"value with key: {keyValue.Key} not found.");
    }
}

var keyValues2 = new Dictionary<string, string>();

foreach (var _ in Enumerable.Range(0, 100))
{
    keyValues2[Guid.NewGuid().ToString()] = Guid.NewGuid().ToString();
}

Parallel.ForEach(keyValues2,  keyValue =>
{
    client.StoreAsync(keyValue.Key, keyValue.Value, TimeSpan.FromMinutes(10), CancellationToken.None)
        .GetAwaiter().GetResult();
});

Parallel.ForEach(keyValues2, keyValue =>
{
    var value = client.GetAsync<string>(keyValue.Key, CancellationToken.None)
        .GetAwaiter().GetResult();

    if (string.IsNullOrEmpty(value.Result))
    {
        Console.WriteLine($"value with key: {keyValue.Key} not found.");
    }
});

Console.WriteLine("Done.");
Console.ReadLine();