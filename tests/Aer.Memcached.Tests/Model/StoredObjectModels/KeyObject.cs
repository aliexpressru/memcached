namespace Aer.Memcached.Tests.Model.StoredObjectModels;

/// <summary>
/// Represents a shard heap table name and version pair.
/// </summary>
public class KeyObject
{
	public string StringField1 { get; set; }
	
	public string StringField2 { get; set; }

	// public override string ToString()
	// 	=> $"Name:{StringField1}_Version:{StringField2}";

	public bool Equals(KeyObject other)
	{
		return StringField1 == other.StringField1
			&& StringField2 == other.StringField2;
	}

	public override bool Equals(object obj)
	{
		return obj is KeyObject other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(StringField1, StringField2);
	}
}
