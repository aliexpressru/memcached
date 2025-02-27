using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Samples.Shared;
using Aer.Memcached.Samples.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aer.Memcached.Samples.WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class MemcachedController : ControllerBase
{
    private readonly IMemcachedClient _memcachedClient;

    public MemcachedController(IMemcachedClient memcachedClient)
    {
        _memcachedClient = memcachedClient;
    }

    [HttpPost("multi-store")]
    public async Task<ActionResult<MultiStoreResponse>> MultiStore(MultiStoreRequest request)
    {
        if (request.ExpirationTime == null)
        {
            var result = await _memcachedClient.MultiStoreAsync(request.KeyValues, request.TimeSpan, CancellationToken.None, expirationMap: request.ExpirationMapWithDateTimeSpan);
            
            return Ok(new MultiStoreResponse
            {
                SyncSuccess = result.SyncSuccess
            });
        }
        else
        {
            var result = await _memcachedClient.MultiStoreAsync(request.KeyValues, request.ExpirationTime, CancellationToken.None, expirationMap: request.ExpirationMapWithDateTimeOffset);
            
            return Ok(new MultiStoreResponse
            {
                SyncSuccess = result.SyncSuccess
            });
        }
    }
    
    [HttpPost("multi-get")]
    public async Task<ActionResult<MultiGetResponse>> Get(MultiGetRequest request)
    {
        var result = await _memcachedClient.MultiGetAsync<string>(request.Keys, CancellationToken.None);

        return Ok(new MultiGetResponse()
        {
            KeyValues = result
        });
    }
    
    [HttpPost("multi-store-complex")]
    public async Task<ActionResult<MultiStoreResponse>> MultiStoreComplex(MultiStoreComplexRequest request)
    {
        var result = await _memcachedClient.MultiStoreAsync(request.KeyValues, request.ExpirationTime, CancellationToken.None, expirationMap: request.ExpirationMapWithDateTimeOffset);

        return Ok(new MultiStoreComplexResponse
        {
            SyncSuccess = result.SyncSuccess
        });
    }
    
    [HttpPost("multi-get-complex")]
    public async Task<ActionResult<MultiGetComplexResponse>> Get(MultiGetComplexRequest request)
    {
        var result = await _memcachedClient.MultiGetAsync<ComplexModel>(request.Keys, CancellationToken.None);

        return Ok(new MultiGetComplexResponse()
        {
            KeyValues = result
        });
    }
    
    [HttpPost("multi-delete-client")]
    public async Task<ActionResult<MultiDeleteResponse>> Delete(MultiDeleteRequest request)
    {
        var result = await _memcachedClient.MultiDeleteAsync(request.Keys, CancellationToken.None);

        return Ok(new MultiDeleteResponse()
        {
            SyncSuccess = result.SyncSuccess
        });
    }
}