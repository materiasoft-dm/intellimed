using IntelliMed.Core.Entities;
using Microsoft.Data.Sqlite;

namespace IntelliMed.Api.Configuration;

/// <summary>
/// Reads the password/lockout policy out of SystemSettings at startup, before Identity is
/// configured.
///
/// Uses a raw SQLite command rather than EF or DI on purpose. At this point in Program.cs the
/// service provider doesn't exist yet, and the obvious alternative — an
/// IConfigureOptions&lt;IdentityOptions&gt; that resolves a repository lazily — deadlocked: options
/// get configured while the host is still being built, and blocking on an async database call there
/// hung `dotnet ef` on every command that builds the host.
///
/// Any failure (no database yet on first run, missing table before the migration applies, unreadable
/// file) returns the shipped defaults. The policy therefore degrades to "the values that used to be
/// hardcoded", never to something weaker.
/// </summary>
public static class SystemSettingsBootstrap
{
    public static SystemSettings Read(string connectionString, ILogger logger)
    {
        var defaults = new SystemSettings();

        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT PasswordMinLength, PasswordRequireDigit, PasswordRequireLowercase,
                       PasswordRequireUppercase, PasswordRequireNonAlphanumeric,
                       LockoutMaxFailedAttempts, LockoutDurationMinutes
                FROM SystemSettings WHERE Id = 1
                """;

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return defaults;

            return new SystemSettings
            {
                PasswordMinLength = reader.GetInt32(0),
                PasswordRequireDigit = reader.GetBoolean(1),
                PasswordRequireLowercase = reader.GetBoolean(2),
                PasswordRequireUppercase = reader.GetBoolean(3),
                PasswordRequireNonAlphanumeric = reader.GetBoolean(4),
                LockoutMaxFailedAttempts = reader.GetInt32(5),
                LockoutDurationMinutes = reader.GetInt32(6)
            };
        }
        catch (Exception ex)
        {
            // Expected on a first run (the table arrives with the migration a few lines later), so
            // this is information, not an error.
            logger.LogInformation(
                "Could not read SystemSettings for the Identity password/lockout policy ({Reason}); " +
                "using built-in defaults for this run.", ex.Message);
            return defaults;
        }
    }
}
