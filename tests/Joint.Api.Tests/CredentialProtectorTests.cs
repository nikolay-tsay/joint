using System.Security.Cryptography;
using Joint.Api.Common.Security;
using Microsoft.Extensions.Configuration;

namespace Joint.Api.Tests;

public class CredentialProtectorTests
{
    [Fact]
    public void A_protected_credential_reads_back_and_is_not_stored_in_the_clear()
    {
        var protector = new CredentialProtector(RandomNumberGenerator.GetBytes(32));

        var stored = protector.Protect("lf_secret_key");

        Assert.DoesNotContain("lf_secret_key", stored);
        Assert.Equal("lf_secret_key", protector.Unprotect(stored));
    }

    [Fact]
    public void The_same_credential_encrypts_differently_each_time()
    {
        var protector = new CredentialProtector(RandomNumberGenerator.GetBytes(32));

        Assert.NotEqual(protector.Protect("key"), protector.Protect("key"));
    }

    [Fact]
    public void A_different_key_cannot_read_the_credential()
    {
        var stored = new CredentialProtector(RandomNumberGenerator.GetBytes(32)).Protect("key");
        var other = new CredentialProtector(RandomNumberGenerator.GetBytes(32));

        Assert.ThrowsAny<CryptographicException>(() => other.Unprotect(stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-base64!")]
    [InlineData("c2hvcnQ=")]
    public void A_missing_or_malformed_configured_key_is_reported_clearly(string? configured)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [CredentialProtector.ConfigurationKey] = configured })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => CredentialProtector.FromConfiguration(configuration));
        Assert.Contains(CredentialProtector.ConfigurationKey, exception.Message);
    }
}
