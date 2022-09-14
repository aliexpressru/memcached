namespace Aer.Memcached.Client.Authentication;

public interface IAuthenticationProvider
{
    bool AuthRequired { get; }
    
    byte[] GetAuthData();
}