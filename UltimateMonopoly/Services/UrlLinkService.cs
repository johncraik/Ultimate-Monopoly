namespace UltimateMonopoly.Services;

public class UrlLinkService
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IConfiguration _config;
    private readonly string _baseUrl;

    public UrlLinkService(LinkGenerator linkGenerator,
        IConfiguration config)
    {
        _linkGenerator = linkGenerator;
        _config = config;
        _baseUrl = _config["BaseUrl"] ?? "http://localhost:5146/";
    }
    
    public string? GetImgUrl(string? imgName)
    {
        string? imgUrl = null;
        if (!string.IsNullOrEmpty(imgName))
        {
            imgUrl = _linkGenerator.GetPathByPage(
                page: "/Profile/Index",
                handler: "AvatarImage",
                values: new { area = "Identity", name = imgName });
        }
        return imgUrl;
    }

    public string GetUrlLink(string path)
    {
        if (_baseUrl.EndsWith('/') && path.StartsWith('/'))
            path = path[1..];
        else if(!_baseUrl.EndsWith('/') && !path.StartsWith('/'))
            path = $"/{path}";
        return $"{_baseUrl}{path}";
    }
}