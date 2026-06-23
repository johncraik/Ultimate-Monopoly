using System.Text.Json.Serialization;

namespace MP.GameEngine.Models;

public class TurnTax
{
    /*
     * Example of turn tax:
     * --------------------------
     * Lower: 10% on over 5,000
     * Middle: 30% on over 10,000
     * Upper: 50% on over 20,000
     * --------------------------
     * Balance = 25,000:
     *  > 10% of 25,000-5,000 => 10% of 20,000 = 2,000
     *  > 30% of 25,000-10,000 => 30% of 15,000 = 4,500
     *  > 50% of 25,000-20,000 => 50% of 5,000 = 2,500
     *  > Total tax: 2,000+4,500+2,500 = 9,000
     *  > At start of turn: Balance => 25,000-9,000 = 16,000
     *
     * Balance = 16,000:
     *  > 10% of 16,000-5,000 => 10% of 11,000 = 1,100
     *  > 30% of 16,000-10,000 => 30% of 6,000 = 1,800
     *  > 50% of 16,000-20,000 => 50% of 0 = 0
     *  > Total tax: 1,100+1,800+0 = 2,900
     *  > At start of turn: Balance => 16,000-2,900 = 13,100
     */
    
    [JsonIgnore]
    public bool TurnTaxEnabled => (LowerTaxBracket > 0 && LowerTaxRate > 0) 
                                  || (MiddleTaxBracket > 0 && MiddleTaxRate > 0)
                                  || (UpperTaxBracket > 0 && UpperTaxRate > 0);
    
    public uint TotalTax(uint balance)
        => LowerTaxAmount(balance) + MiddleTaxAmount(balance) + UpperTaxAmount(balance);
    
    //E.g. LowerTaxBracket = 5,000, LowerTaxRate = 0.1f -> 10% tax on over 5,000
    public uint LowerTaxBracket { get; set; } = 0;
    public float LowerTaxRate { get; set; } = 0;

    public uint LowerTaxAmount(uint balance)
        => TaxAmount(balance, LowerTaxBracket, LowerTaxRate);
    
    
    //E.g. MiddleTaxBracket = 10,000, MiddleTaxRate = 0.3f -> 30% tax on over 10,000
    public uint MiddleTaxBracket { get; set; } = 0;
    public float MiddleTaxRate { get; set; } = 0;

    public uint MiddleTaxAmount(uint balance) 
        => TaxAmount(balance, MiddleTaxBracket, MiddleTaxRate);
    
    
    //E.g. UpperTaxBracket = 20,000, UpperTaxRate = 0.5f -> 50% tax on over 20,000
    public uint UpperTaxBracket { get; set; } = 0;
    public float UpperTaxRate { get; set; } = 0;
    
    public uint UpperTaxAmount(uint balance) 
        => TaxAmount(balance, UpperTaxBracket, UpperTaxRate);
    
    
    private uint TaxAmount(uint balance, uint bracket, float rate)
    {
        if(balance <= bracket) return 0;
        
        var amount = balance - bracket;
        return (uint)(amount * rate);
    }
}