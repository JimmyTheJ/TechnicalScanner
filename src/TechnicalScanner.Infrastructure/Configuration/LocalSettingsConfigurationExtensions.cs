using Microsoft.Extensions.Configuration;

namespace TechnicalScanner.Infrastructure.Configuration;

public static class LocalSettingsConfigurationExtensions
{
    public const string LocalSettingsFileName = "appsettings.Local.json";

    public static IConfigurationBuilder AddLocalAppSettings(this IConfigurationBuilder configuration)
        => configuration.AddJsonFile(LocalSettingsFileName, optional: true, reloadOnChange: true);
}
