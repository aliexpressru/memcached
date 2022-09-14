namespace Aer.Memcached.Client.Commands.Enums;

/// <summary>
/// Indicates the mode how the items are stored in Memcached.
/// </summary>
public enum StoreMode
{
    /// <summary>
    /// Store the data, but only if the server does not already hold data for a given key
    /// </summary>
    Add = 1,
    /// <summary>
    /// Store the data, but only if the server does already hold data for a given key
    /// </summary>
    Replace,
    /// <summary>
    /// Store the data, overwrite if already exists
    /// </summary>
    Set
};