using Aer.Memcached.Client.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Aer.Memcached.WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class MemcachedController : ControllerBase
{
    private readonly IMemcachedClient _memcachedClient;

    public MemcachedController(IMemcachedClient memcachedClient)
    {
        _memcachedClient = memcachedClient;
    }

    [HttpGet(Name = "GetByKey")]
    public async Task<string> Get(string key)
    {
        var getResult = await _memcachedClient.GetAsync<string>(key, CancellationToken.None);
        var value = getResult.Result;

        if (value == null)
        {
            await _memcachedClient.StoreAsync(
                key, 
                Guid.NewGuid().ToString(), 
                TimeSpan.FromSeconds(30), 
                CancellationToken.None);
        }

        return value;
    }
}