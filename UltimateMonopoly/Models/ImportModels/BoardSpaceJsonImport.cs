namespace UltimateMonopoly.Models.ImportModels;

public class BoardSpaceJsonImport
{
    public string Name { get; set; }
    
    public ushort Index { get; set; }
    public string SpaceType { get; set; }
    
    public ushort? PurchaseCost { get; set; }
    public ushort[]? Rents { get; set; }
    
    public ushort? BuildCost { get; set; }
    public ushort? Tax { get; set; }
}