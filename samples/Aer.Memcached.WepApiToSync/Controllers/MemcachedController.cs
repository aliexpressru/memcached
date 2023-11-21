using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Shared;
using Aer.Memcached.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aer.Memcached.WepApiToSync.Controllers;

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
    public async Task<ActionResult<MultiStoreResponse>> Get(MultiStoreRequest request)
    {
        await _memcachedClient.MultiStoreAsync(request.KeyValues, request.ExpirationTime, CancellationToken.None);

        return Ok(new MultiStoreResponse());
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
    public async Task<ActionResult<MultiStoreResponse>> Get(MultiStoreComplexRequest request)
    {
        await _memcachedClient.MultiStoreAsync(request.KeyValues, request.ExpirationTime, CancellationToken.None);

        return Ok(new MultiStoreComplexResponse());
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
}