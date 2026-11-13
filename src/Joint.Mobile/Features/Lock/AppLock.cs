using System.Globalization;
using System.Security.Cryptography;

namespace Joint.Mobile.Features.Lock;

/// <summary>
/// The optional lock in front of the app: a PIN, and biometrics as the quicker way in.
/// It guards this device only and is separate from the Google session; signing out
/// removes it, which is also the way back in for someone who forgot the PIN.
/// </summary>
public class AppLock
{
    public const int PinLength = 4;

    /// <summary>Wrong PINs in a row before the app signs out.</summary>
    public const int MaxAttempts = 5;

    private const string PinKey = "lock_pin";
    private const string BiometricsKey = "lock_biometrics";
    private const string FailuresKey = "lock_failures";
    private const int Iterations = 100_000;

    public async Task<bool> IsEnabledAsync() => await SecureStorage.Default.GetAsync(PinKey) is not null;

    public async Task<bool> UsesBiometricsAsync() => await SecureStorage.Default.GetAsync(BiometricsKey) is not null;

    /// <summary>Turns the lock on, or changes the PIN of one that is already on.</summary>
    public async Task SetPinAsync(string pin)
    {
        // Only a salted, slow hash is kept, inside the device's secure storage.
        var salt = RandomNumberGenerator.GetBytes(16);
        await SecureStorage.Default.SetAsync(
            PinKey, $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(Hash(pin, salt))}");
        SecureStorage.Default.Remove(FailuresKey);
    }

    public async Task SetBiometricsAsync(bool enabled)
    {
        if (enabled)
        {
            await SecureStorage.Default.SetAsync(BiometricsKey, "1");
        }
        else
        {
            SecureStorage.Default.Remove(BiometricsKey);
        }
    }

    /// <summary>
    /// Checks a PIN and counts failures across app restarts, so closing the app does not
    /// buy more guesses.
    /// </summary>
    /// <returns>Whether it matched, and how many attempts remain when it did not.</returns>
    public async Task<(bool Matched, int AttemptsLeft)> CheckPinAsync(string pin)
    {
        var parts = (await SecureStorage.Default.GetAsync(PinKey))?.Split(':');
        if (parts is { Length: 2 }
            && CryptographicOperations.FixedTimeEquals(
                Hash(pin, Convert.FromBase64String(parts[0])), Convert.FromBase64String(parts[1])))
        {
            SecureStorage.Default.Remove(FailuresKey);
            return (true, MaxAttempts);
        }

        int.TryParse(await SecureStorage.Default.GetAsync(FailuresKey), CultureInfo.InvariantCulture, out var failures);
        failures++;
        await SecureStorage.Default.SetAsync(FailuresKey, failures.ToString(CultureInfo.InvariantCulture));
        return (false, Math.Max(0, MaxAttempts - failures));
    }

    /// <summary>Turns the lock off and forgets the PIN.</summary>
    public void Reset()
    {
        SecureStorage.Default.Remove(PinKey);
        SecureStorage.Default.Remove(BiometricsKey);
        SecureStorage.Default.Remove(FailuresKey);
    }

    private static byte[] Hash(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, 32);
}
