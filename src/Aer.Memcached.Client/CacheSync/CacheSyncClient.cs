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

public class CacheSyncClient: ICacheSyncClient
{
    private static readonly JsonSerializerSettings JsonSetting = new()
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
    public async Task SyncAsync<T>(
        MemcachedConfiguration.SyncServer syncServer,
        CacheSyncModel<T> data, 
        CancellationToken token)
    {
        try
        {
            await _retryPolicy.Execute(async () =>
            {
                var httpClient = _httpClientFactory.CreateClient();

                var content = new StringContent(
                    JsonConvert.SerializeObject(data, JsonSetting),
                    Encoding.UTF8,
                    MediaTypeNames.Application.Json);

                var baseUri = new Uri(syncServer.Address);
                var endpointUri = new Uri(baseUri, _config.SyncSettings.SyncEndpoint + TypeExtensions.GetTypeName<T>());

                var response = await httpClient.PostAsync(endpointUri, content, token);

                response.EnsureSuccessStatusCode();
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to sync data to {SyncServerAddress}", syncServer.Address);

            throw;
        }
    }
}