using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Aer.Memcached.Samples.Shared.Models;

namespace Aer.Memcached.Samples.Shared;

public class MemcachedWebApiClient
{
    private readonly HttpClient _httpClient;

    public MemcachedWebApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<MultiStoreResponse> MultiStore(MultiStoreRequest request)
    {
        var msg = await PostAsync("Memcached/multi-store", request);

        return await GetResult<MultiStoreResponse>(msg);
    }

    public async Task<MultiGetResponse> MultiGet(MultiGetRequest request)
    {
        var msg = await PostAsync("Memcached/multi-get", request);

        return await GetResult<MultiGetResponse>(msg);
    }
    
    public async Task<MultiStoreComplexResponse> MultiStoreComplex(MultiStoreComplexRequest request)
    {
        var msg = await PostAsync("Memcached/multi-store-complex", request);

        return await GetResult<MultiStoreComplexResponse>(msg);
    }

    public async Task<MultiGetComplexResponse> MultiGetComplex(MultiGetComplexRequest request)
    {
        var msg = await PostAsync("Memcached/multi-get-complex", request);

        return await GetResult<MultiGetComplexResponse>(msg);
    }
    
    public async Task<MultiDeleteResponse> MultiDelete(MultiDeleteRequest request)
    {
        var msg = await PostAsync("Memcached/multi-delete-client", request);

        return await GetResult<MultiDeleteResponse>(msg);
    }

    private async Task<HttpResponseMessage> PostAsync<T>(string requestUri, T data)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(data),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        return await _httpClient.PostAsync(requestUri, content);
    }

    private async Task<T> GetResult<T>(HttpResponseMessage msg)
    {
        var stream = await msg.Content.ReadAsStreamAsync();
        var result = JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return result;
    }
}