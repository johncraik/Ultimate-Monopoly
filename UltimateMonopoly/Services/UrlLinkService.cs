namespace UltimateMonopoly.Services;

public class UrlLinkService
{
    private readonly LinkGenerator _linkGenerator;

    public UrlLinkService(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
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
}