using FluentAssertions;
using IntelliMed.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace IntelliMed.Tests.Configuration;

/// <summary>
/// Guards the rules that replaced the old `?? "IntelliMed_SuperSecretKey_AtLeast32Characters!"`
/// fallbacks. That key signed both staff tokens and the /dbadmin scope token while being committed
/// to the repo and shipped in the published artifact, so these tests exist to make sure it (or an
/// absent key) can never silently come back.
/// </summary>
public class JwtSettingsTests
{
    private const string BurnedKey = "IntelliMed_SuperSecretKey_AtLeast32Characters!";
    private const string ValidKey = "a-perfectly-fine-signing-key-of-sufficient-length-1234567890";

    private static IConfiguration ConfigWith(params (string Key, string? Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();

    private static IHostEnvironment Environment(string name)
    {
        var mock = new Mock<IHostEnvironment>();
        mock.SetupGet(e => e.EnvironmentName).Returns(name);
        return mock.Object;
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Development")]
    public void Resolve_WithTheCommittedDefaultKey_Throws_InEveryEnvironment(string environmentName)
    {
        var config = ConfigWith(("Jwt:Key", BurnedKey));

        var act = () => JwtSettings.Resolve(config, Environment(environmentName), NullLogger<JwtSettings>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*publicly-known default*");
    }

    [Fact]
    public void Resolve_WithNoKey_OutsideDevelopment_Throws()
    {
        var config = ConfigWith(("Jwt:Issuer", "IntelliMed"));

        var act = () => JwtSettings.Resolve(config, Environment("Production"), NullLogger<JwtSettings>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public void Resolve_WithNoKey_InDevelopment_GeneratesAnEphemeralKey()
    {
        var config = ConfigWith(("Jwt:Issuer", "IntelliMed"));

        var first = JwtSettings.Resolve(config, Environment("Development"), NullLogger<JwtSettings>.Instance);
        var second = JwtSettings.Resolve(config, Environment("Development"), NullLogger<JwtSettings>.Instance);

        first.Key.Should().NotBeNullOrWhiteSpace();
        first.Key.Should().NotBe(BurnedKey);
        second.Key.Should().NotBe(first.Key, "each run gets a fresh ephemeral key");
    }

    [Fact]
    public void Resolve_WithTooShortKey_Throws()
    {
        var config = ConfigWith(("Jwt:Key", "too-short"));

        var act = () => JwtSettings.Resolve(config, Environment("Production"), NullLogger<JwtSettings>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 32 bytes*");
    }

    [Fact]
    public void Resolve_WithValidKey_UsesItAndTheConfiguredIssuerAudience()
    {
        var config = ConfigWith(
            ("Jwt:Key", ValidKey),
            ("Jwt:Issuer", "MyIssuer"),
            ("Jwt:Audience", "MyAudience"));

        var settings = JwtSettings.Resolve(config, Environment("Production"), NullLogger<JwtSettings>.Instance);

        settings.Key.Should().Be(ValidKey);
        settings.Issuer.Should().Be("MyIssuer");
        settings.Audience.Should().Be("MyAudience");
    }

    [Fact]
    public void Resolve_WithValidKeyButNoIssuerAudience_FallsBackToDefaults()
    {
        var config = ConfigWith(("Jwt:Key", ValidKey));

        var settings = JwtSettings.Resolve(config, Environment("Production"), NullLogger<JwtSettings>.Instance);

        settings.Issuer.Should().Be("IntelliMed");
        settings.Audience.Should().Be("IntelliMed.Client");
    }
}
