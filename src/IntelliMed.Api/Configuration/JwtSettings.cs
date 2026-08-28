using System.Security.Cryptography;

namespace IntelliMed.Api.Configuration;

/// <summary>
/// The single resolved source of JWT signing settings, registered as a singleton in Program.cs and
/// injected wherever a token is issued (AuthController, DbAdminController) or validated.
///
/// This type exists because the key used to be read independently in three places, each with an
/// `?? "IntelliMed_SuperSecretKey_AtLeast32Characters!"` fallback — a value that was also committed
/// in appsettings.json and shipped in the published artifact. Anything holding that key could forge
/// a staff token for any role, and (because DbAdminController signs with the same key) a
/// scope=dbadmin token granting raw SQL access. There is deliberately no fallback here: a missing
/// key now fails loudly at startup outside Development instead of silently signing with a value
/// anyone can read.
/// </summary>
public sealed class JwtSettings
{
    /// <summary>The key that leaked. Rejected outright wherever it appears, in every environment,
    /// so it can't be reintroduced by pasting an old config back in.</summary>
    private const string BurnedKey = "IntelliMed_SuperSecretKey_AtLeast32Characters!";

    /// <summary>HS256 requires a key of at least 256 bits.</summary>
    private const int MinimumKeyBytes = 32;

    public string Key { get; }
    public string Issuer { get; }
    public string Audience { get; }

    /// <summary>Direct construction, for tests and any explicit wiring. Production goes through
    /// <see cref="Resolve"/>, which is where the "no committed/weak/absent key" rules live.</summary>
    public JwtSettings(string key, string issuer, string audience)
    {
        Key = key;
        Issuer = issuer;
        Audience = audience;
    }

    public static JwtSettings Resolve(IConfiguration configuration, IHostEnvironment environment, ILogger logger)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "IntelliMed";
        var audience = configuration["Jwt:Audience"] ?? "IntelliMed.Client";
        var key = configuration["Jwt:Key"];

        if (string.Equals(key, BurnedKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Jwt:Key is set to the publicly-known default that was previously committed to this " +
                "repository. Generate a new key and supply it via the Jwt__Key environment variable " +
                "or an untracked appsettings.Production.json.");
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            if (System.Text.Encoding.UTF8.GetByteCount(key) < MinimumKeyBytes)
            {
                throw new InvalidOperationException(
                    $"Jwt:Key must be at least {MinimumKeyBytes} bytes for HS256 signing.");
            }
            return new JwtSettings(key, issuer, audience);
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Jwt:Key is not configured. Set it via the Jwt__Key environment variable or an " +
                "untracked appsettings.Production.json — it is intentionally absent from the " +
                "committed appsettings.json.");
        }

        // Development convenience only: a per-run random key, so a dev machine needs no setup but
        // also never signs with a shared/committed secret. Tokens do not survive a restart, which is
        // the intended trade-off — it makes an accidental reliance on a fixed dev key obvious.
        var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        logger.LogWarning(
            "Jwt:Key is not configured; generated an ephemeral key for this Development run. " +
            "Tokens will be invalidated on restart. Configure Jwt__Key to make sessions persist.");
        return new JwtSettings(generated, issuer, audience);
    }
}
