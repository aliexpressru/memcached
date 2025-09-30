using Aer.Memcached.Client.Config;
using Microsoft.AspNetCore.Builder;

namespace Aer.Memcached.Helpers;

internal static class EndpointBuilderExtensions
{
    public static RouteHandlerBuilder AllowAnonymousIfConfigured(
        this RouteHandlerBuilder endpointBuilder,
        MemcachedConfiguration configuration)
    {
        if (configuration.SyncSettings == null)
        {
            return endpointBuilder;
        }

        if (configuration.SyncSettings.SyncEndpointsAuthAllowAnonymous)
        {
            endpointBuilder.AllowAnonymous();
        }

        return endpointBuilder;
    }
}
