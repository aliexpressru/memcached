using System.Net.Mime;
using System.Text;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Extensions;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Polly;
using Polly.Retry;

namespace Aer.Memcached.Client.CacheSync;

internal class CacheSyncClient: ICacheSyncClient
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Converters = new List<JsonConverter>(new[] {new StringEnumConverter()}),
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        }
    };
    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MemcachedConfiguration _config;
    private readonly ILogger<CacheSyncClient> _logger;
    private readonly RetryPolicy _retryPolicy;

    public CacheSyncClient(
        IHttpClientFactory httpClientFactory, 
        IOptions<MemcachedConfiguration> config,
        ILogger<CacheSyncClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
        _logger = logger;

        _retryPolicy = Policy.Handle<Exception>()
            .Retry(_config.SyncSettings?.RetryCount ?? 3);
    }

    /// <inheritdoc />
    public async Task SyncAsync(
        MemcachedConfiguration.SyncServer syncServer,
        CacheSyncModel data, 
        CancellationToken token)
    {
        try
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(data, JsonSettings),
                Encoding.UTF8,
                MediaTypeNames.Application.Json);
            
            var baseUri = new Uri(syncServer.Address);
            var endpointUri = new Uri(baseUri, _config.SyncSettings.SyncEndpoint + TypeExtensions.GetTypeName<byte>());

            await RequestAsync(content, endpointUri, token);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to sync data to {SyncServerAddress}. Check configured sync server URL and Startup class configuration", syncServer.Address);

            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        MemcachedConfiguration.SyncServer syncServer,
        IEnumerable<string> keys, 
        CancellationToken token)
    {
        try
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(keys, JsonSettings),
                Encoding.UTF8,
                MediaTypeNames.Application.Json);
            
            var baseUri = new Uri(syncServer.Address);
            var endpointUri = new Uri(baseUri, _config.SyncSettings.DeleteEndpoint);

            await RequestAsync(content, endpointUri, token);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to delete data on {SyncServerAddress}", syncServer.Address);

            throw;
        }
    }
    
    private async Task RequestAsync(StringContent content, Uri endpointUri, CancellationToken token)
    {
        await _retryPolicy.Execute(async () =>
        {
            var httpClient = _httpClientFactory.CreateClient();

            var response = await httpClient.PostAsync(endpointUri, content, token);

            response.EnsureSuccessStatusCode();
        });
    }
}