namespace LocalKnowledgeBase.Core.Services;

/// <summary>
/// Manages configuration settings for the knowledge base,
/// including the user-selected data directory path.
/// </summary>
public class ConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LocalKnowledgeBase");
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "data_directory.txt");

    public static string? GetDataDirectory()
    {
        if (File.Exists(ConfigFile))
        {
            var path = File.ReadAllText(ConfigFile).Trim();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                return path;
            }
        }
        return null;
    }

    public static void SetDataDirectory(string path)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigFile, path);
    }

    public static bool HasDataDirectory()
    {
        return !string.IsNullOrEmpty(GetDataDirectory());
    }
}
