using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Samples.Shared;
using Aer.Memcached.Samples.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aer.Memcached.Samples.WepApiToSync.Controllers;

[ApiController]
[Route("[controller]")]
public class MemcachedController : ControllerBase
{
    private readonly IMemcachedClient _memcachedClient;

    public MemcachedController(IMemcachedClient memcachedClient)
    {
        _memcachedClient = memcachedClient;
    }

    [HttpPost("store-dictionary")]
    public async Task<ActionResult<MultiStoreResponse>> StoreComplexDictionary(StoreComplexRequest request)
    {
        var result = await _memcachedClient.StoreAsync(
            request.Key,
            request.DataToStore ?? new Dictionary<ComplexDictionaryKey, ComplexModel>()
            {
                [ComplexDictionaryKey.Create("a", "b")] = new()
                {
                    TestValues = new Dictionary<string, long>()
                    {
                        ["aa"] = 1,
                        ["bb"] = 2
                    }
                }
            },
            request.ExpirationTime ?? TimeSpan.FromSeconds(15),
            CancellationToken.None);
        
        return Ok(
            new MultiStoreComplexResponse
            {
                SyncSuccess = result.SyncSuccess
            });
    }

    [HttpPost("multi-store")]
    public async Task<ActionResult<MultiStoreResponse>> Get(MultiStoreRequest request)
    {
        var result = await _memcachedClient.MultiStoreAsync(
            request.KeyValues,
            request.ExpirationTime,
            CancellationToken.None);

        return Ok(
            new MultiStoreResponse
            {
                SyncSuccess = result.SyncSuccess
            });
    }

    [HttpPost("multi-get")]
    public async Task<ActionResult<MultiGetResponse>> Get(MultiGetRequest request)
    {
        var result = 
            await _memcachedClient.MultiGetAsync<string>(request.Keys, CancellationToken.None);

        return Ok(
            new MultiGetResponse()
            {
                KeyValues = result
            });
    }

    [HttpPost("multi-store-complex")]
    public async Task<ActionResult<MultiStoreResponse>> Get(MultiStoreComplexRequest request)
    {
        var result = await _memcachedClient.MultiStoreAsync(
            request.KeyValues,
            request.ExpirationTime,
            CancellationToken.None);

        return Ok(
            new MultiStoreComplexResponse
            {
                SyncSuccess = result.SyncSuccess
            });
    }

    [HttpPost("multi-get-complex")]
    public async Task<ActionResult<MultiGetComplexResponse>> Get(MultiGetComplexRequest request)
    {
        var result = await _memcachedClient.MultiGetAsync<ComplexModel>(request.Keys, CancellationToken.None);

        return Ok(
            new MultiGetComplexResponse()
            {
                KeyValues = result
            });
    }

    [HttpPost("multi-delete")]
    public async Task<ActionResult<MultiDeleteResponse>> Get(MultiDeleteRequest request)
    {
        var result = await _memcachedClient.MultiDeleteAsync(request.Keys, CancellationToken.None);

        return Ok(
            new MultiDeleteResponse()
            {
                SyncSuccess = result.SyncSuccess
            });
    }
}