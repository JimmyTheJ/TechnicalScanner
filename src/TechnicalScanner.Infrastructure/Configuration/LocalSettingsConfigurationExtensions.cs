using Microsoft.Extensions.Configuration;

namespace TechnicalScanner.Infrastructure.Configuration;

public static class LocalSettingsConfigurationExtensions
{
    public const string LocalSettingsFileName = "appsettings.Local.json";

    public static IConfigurationBuilder AddLocalAppSettings(this IConfigurationBuilder configuration)
    {
        configuration.AddJsonFile(LocalSettingsFileName, optional: true, reloadOnChange: true);
        configuration.AddRepoRootEnvFile();
        configuration.AddEnvironmentVariables();
        return configuration;
    }

    public static IConfigurationBuilder AddEnvFile(this IConfigurationBuilder configuration, string path)
    {
        if (!File.Exists(path))
        {
            return configuration;
        }

        return configuration.AddInMemoryCollection(EnvFileParser.Parse(File.ReadAllText(path)));
    }

    internal static IConfigurationBuilder AddRepoRootEnvFile(this IConfigurationBuilder configuration)
    {
        var path = FindRepoRootEnvFilePath();
        return path is null ? configuration : configuration.AddEnvFile(path);
    }

    internal static string? FindRepoRootEnvFilePath()
    {
        foreach (var start in GetStartDirectories())
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (!File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
                {
                    continue;
                }

                var envPath = Path.Combine(directory.FullName, ".env");
                return File.Exists(envPath) ? envPath : null;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetStartDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var full = Path.GetFullPath(candidate);
            if (seen.Add(full))
            {
                yield return full;
            }
        }
    }
}
