using System.Text;
using Aer.Memcached.Client.Config;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Client.Authentication;

public class DefaultAuthenticationProvider: IAuthenticationProvider
{
    private readonly byte[] _authData;
    
    public bool AuthRequired { get; }
    
    public DefaultAuthenticationProvider(IOptions<MemcachedConfiguration.AuthenticationCredentials> config)
    {
        var configValue = config.Value;
        AuthRequired = configValue != null && !string.IsNullOrEmpty(configValue.Username) && !string.IsNullOrEmpty(configValue.Password);

        if (AuthRequired)
        {
            _authData = Encoding.UTF8.GetBytes("" + "\0" + configValue.Username + "\0" + configValue.Password);    
        }
    }

    public byte[] GetAuthData()
    {
        return _authData;
    }
}