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
    
    public enum FileCategory
    {
        Card,
        Board,
        ProfileImg,
        Rules
    }

    public string GetFilePath(FileCategory type)
    {
        var path = type switch
        {
            FileCategory.Card => "cards",
            FileCategory.Board => "boards",
            FileCategory.ProfileImg => "profile_imgs",
            FileCategory.Rules => "rules",
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
    
    public async Task WriteFileAsync(string path, string name, string content)
        => await File.WriteAllTextAsync(Path.Combine(path, name), content);
}