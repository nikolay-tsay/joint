using System.Security.Cryptography;
using System.Text;

namespace Joint.Api.Common.Security;

/// <summary>
/// Encrypts provider credentials (API keys, tokens) before they are stored, with
/// AES-256-GCM and a key that lives only in configuration, never in the database.
/// </summary>
public class CredentialProtector
{
    public const string ConfigurationKey = "Credentials:EncryptionKey";

    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public CredentialProtector(byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("The credential encryption key must be 32 bytes.", nameof(key));
        }

        _key = key;
    }

    public static CredentialProtector FromConfiguration(IConfiguration configuration)
    {
        var encoded = configuration[ConfigurationKey]
            ?? throw new InvalidOperationException(
                $"{ConfigurationKey} is not configured. Set it to 32 random bytes, base64-encoded.");
        try
        {
            return new CredentialProtector(Convert.FromBase64String(encoded));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} must be 32 random bytes, base64-encoded.", exception);
        }
    }

    public string Protect(string credential)
    {
        var plaintext = Encoding.UTF8.GetBytes(credential);
        var output = new byte[NonceSize + TagSize + plaintext.Length];
        var nonce = output.AsSpan(0, NonceSize);
        var tag = output.AsSpan(NonceSize, TagSize);
        var ciphertext = output.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return Convert.ToBase64String(output);
    }

    /// <exception cref="CryptographicException">The value was not produced with this key.</exception>
    public string Unprotect(string protectedCredential)
    {
        var input = Convert.FromBase64String(protectedCredential);
        if (input.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("The stored credential is malformed.");
        }

        var plaintext = new byte[input.Length - NonceSize - TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(
            input.AsSpan(0, NonceSize),
            input.AsSpan(NonceSize + TagSize),
            input.AsSpan(NonceSize, TagSize),
            plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }
}
