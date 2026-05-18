namespace UltimateMonopoly.Services;

public class FilePathProvider
{
    private readonly string _basePath;
    public const string ConfigFileType = ".json";
    
    public FilePathProvider(IConfiguration config)
    {
        _basePath = config["BasePath"] 
                    ?? throw new ArgumentNullException(nameof(_basePath), "BasePath is not set in configuration");
    }
    
    public enum ConfigType
    {
        Card,
        Board
    }

    public string GetFilePath(ConfigType type)
    {
        var path = type switch
        {
            ConfigType.Card => "cards",
            ConfigType.Board => "boards",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        
        path = Path.Combine(_basePath, path);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        
        return path;
    }
    
    public async Task<string> ReadFileAsync(string path)
        => await File.ReadAllTextAsync(path);
    
    public async Task<string> ReadFileAsync(string path, string name)
        => await File.ReadAllTextAsync(Path.Combine(path, name));
}