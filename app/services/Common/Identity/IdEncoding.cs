namespace Common.Identity;

/// <summary>
/// Crockford Base32 encoding for the long ids that reach a URL or a response body.
/// </summary>
/// <remarks>
/// The alphabet omits I, L, O and U, so a decoded id survives being read aloud or retyped from a
/// support ticket. Decoding accepts the ambiguous characters and folds them onto the digits they
/// resemble, which is why <see cref="TryDecode"/> is the only entry point the endpoints use: an id
/// arriving from a URL is untrusted input, and a malformed one is a 404 rather than a 500.
/// </remarks>
public static class IdEncoding
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private const int EncodedLength = 13;

    public static string Encode(long id)
    {
        var value = unchecked((ulong)id);
        var buffer = new char[EncodedLength];
        for (var i = EncodedLength - 1; i >= 0; i--)
        {
            buffer[i] = Alphabet[(int)(value & 0x1F)];
            value >>= 5;
        }

        return new string(buffer);
    }

    public static bool TryDecode(string? encoded, out long id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length != EncodedLength)
        {
            return false;
        }

        ulong value = 0;
        foreach (var character in encoded)
        {
            var digit = DigitOf(character);
            if (digit < 0)
            {
                return false;
            }

            value = (value << 5) | (uint)digit;
        }

        id = unchecked((long)value);
        return true;
    }

    private static int DigitOf(char character) => char.ToUpperInvariant(character) switch
    {
        'I' or 'L' => 1,
        'O' => 0,
        var c => Alphabet.IndexOf(c),
    };
}
