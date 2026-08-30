using System.Text.Json;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed class DatabaseConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public DatabaseConfigService()
    {
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShopHoa",
            "database_config.json");
    }

    public DatabaseConfig Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new DatabaseConfig();
            }

            var json = File.ReadAllText(_filePath);

            var config = JsonSerializer.Deserialize<DatabaseConfig>(
                json,
                JsonOptions);

            return config ?? new DatabaseConfig();
        }
        catch
        {
            return new DatabaseConfig();
        }
    }

    public void Save(DatabaseConfig config)
    {
        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);

        File.WriteAllText(_filePath, json);
    }

    public string GetConfigPath()
    {
        return _filePath;
    }
}