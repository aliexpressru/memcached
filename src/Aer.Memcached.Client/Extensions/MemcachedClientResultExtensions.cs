using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Extensions;

internal static class MemcachedClientResultExtensions
{
    public static MemcachedClientResult WithSyncSuccess(this MemcachedClientResult result, bool syncSuccess)
    {
        result.SyncSuccess = syncSuccess;
        return result;
    }
}