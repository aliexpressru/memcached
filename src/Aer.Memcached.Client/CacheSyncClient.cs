using System.Net.Mime;
using System.Text;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Microsoft.Extensions.Options;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Aer.Memcached.Client;

public class CacheSyncClient: ICacheSyncClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MemcachedConfiguration _config;

    public CacheSyncClient(IHttpClientFactory httpClientFactory, IOptions<MemcachedConfiguration> config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
    }

    public async Task Sync<T>(
        MemcachedConfiguration.SyncServer syncServer,
        CacheSyncModel<T> data, 
        CancellationToken token)
    {
        var httpClient = _httpClientFactory.CreateClient();

        var content = new StringContent(
            JsonSerializer.Serialize(data),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);
        
        var baseUri = new Uri(syncServer.Address);
        var endpointUri = new Uri(baseUri, _config.SyncSettings.SyncEndpoint);

        await httpClient.PostAsync(endpointUri, content, token);
    }
}