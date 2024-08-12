namespace Aer.Memcached.Samples.Shared;

public class ComplexDictionaryKey
{
	public string KeyProperty1 { get; init; }
	
	public string KeyProperty2 { get; init; }

	// serializer ctor
	public ComplexDictionaryKey()
	{ }

	private ComplexDictionaryKey(string keyProperty1, string keyProperty2)
	{
		KeyProperty1 = keyProperty1;
		KeyProperty2 = keyProperty2;
	}

	public static ComplexDictionaryKey Create(string keyProperty1, string keyProperty2)
	{
		return new ComplexDictionaryKey(keyProperty1, keyProperty2);
	}

	protected bool Equals(ComplexDictionaryKey other)
	{
		return KeyProperty1 == other.KeyProperty1 && KeyProperty2 == other.KeyProperty2;
	}

	public override bool Equals(object obj)
	{
		if (ReferenceEquals(null, obj))
		{
			return false;
		}

		if (ReferenceEquals(this, obj))
		{
			return true;
		}

		if (obj.GetType() != this.GetType())
		{
			return false;
		}

		return Equals((ComplexDictionaryKey) obj);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(KeyProperty1, KeyProperty2);
	}
}
