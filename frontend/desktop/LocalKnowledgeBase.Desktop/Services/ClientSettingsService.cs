using System.IO;
using System.Text.Json;

namespace LocalKnowledgeBase.Desktop.Services;

public sealed class ClientSettings
{
    public const string DefaultApiBaseUrl = "https://localhost:7047";

    public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;
}

public static class ClientSettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LocalKnowledgeBase");

    private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "client_settings.json");

    public static ClientSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new ClientSettings();

            var json = File.ReadAllText(SettingsFile);
            var settings = JsonSerializer.Deserialize<ClientSettings>(json);
            if (settings == null ||
                !Uri.TryCreate(settings.ApiBaseUrl, UriKind.Absolute, out var apiUri) ||
                (apiUri.Scheme != Uri.UriSchemeHttp && apiUri.Scheme != Uri.UriSchemeHttps))
            {
                return new ClientSettings();
            }

            return settings;
        }
        catch
        {
            return new ClientSettings();
        }
    }

    public static void Save(ClientSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(SettingsFile, json);
    }
}
