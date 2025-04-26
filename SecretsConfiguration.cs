using Microsoft.Extensions.Configuration;

namespace FluentRun.Console;

public static class SecretsConfiguration
{
    public static IConfigurationBuilder AddCustomUserSecrets(this IConfigurationBuilder builder)
    {
        // Construct path to secrets file in user's home directory: ~/.fluentrun/Secrets/secrets.json
        var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var secretsPath = Path.Combine(userFolder, ".fluentrun", "Secrets", "secrets.json");

        // Add secrets.json to configuration if it exists
        if (File.Exists(secretsPath))
        {
            builder.AddJsonFile(secretsPath, true, true);
        }

        return builder;
    }
}