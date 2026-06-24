using System.Text.Json;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services;

namespace UltimateMonopoly.Services.GameEngine;

public class TurnTaxService : ITurnTaxService
{
    private readonly FilePathProvider _filePathProvider;
    private readonly TransactionService _transactionService;
    private const string _taxFileName = "turnTax.json";
    
    private TurnTax _tax = new();

    public TurnTaxService(FilePathProvider filePathProvider,
        TransactionService transactionService)
    {
        _filePathProvider = filePathProvider;
        _transactionService = transactionService;
    }

    public bool Enabled { get; private set; }

    public async Task Import()
    {
        var path = _filePathProvider.GetFilePath(FilePathProvider.FileCategory.Rules);
        path = Path.Combine(path, _taxFileName);
        if(!File.Exists(path))
            return;

        var txt = await _filePathProvider.ReadFileAsync(path);
        var tax = JsonSerializer.Deserialize<TurnTax>(txt);
        if(tax == null) return;

        _tax = tax;
        Enabled = _tax.TurnTaxEnabled;
    }

    public async Task ApplyTax(MP.GameEngine.Services.Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var tax = TotalTax(player.Money);
        if(tax == 0) return;
        
        //Cite rules:
        engine.CiteRule(RuleCode.TurnTax_Spend);
        engine.CiteRule(RuleCode.TurnTax_Pay);
        
        //Pay taxes:
        tax = MoneyHelper.NormaliseAmountToPositive(tax, engine.Cache.RoundingRule, FinancialReason.TurnTax);
        await _transactionService.PayTurnTax(engine, player, tax, ct);
    }

    public uint TotalTax(uint balance)
    {
        if(!_tax.TurnTaxEnabled) return 0;
        return _tax.TotalTax(balance);
    }

    /// <summary>A copy of the current brackets — for the admin editor to read without touching live state.</summary>
    public TurnTax GetTurnTax() => new()
    {
        LowerTaxBracket = _tax.LowerTaxBracket,
        LowerTaxRate = _tax.LowerTaxRate,
        MiddleTaxBracket = _tax.MiddleTaxBracket,
        MiddleTaxRate = _tax.MiddleTaxRate,
        UpperTaxBracket = _tax.UpperTaxBracket,
        UpperTaxRate = _tax.UpperTaxRate
    };

    /// <summary>
    /// Persists new brackets to <c>turnTax.json</c> and refreshes the in-memory copy + <see cref="Enabled"/>
    /// (mirrors <see cref="Import"/>). As this is the singleton the engine reads, the change takes effect
    /// live — in-progress games apply it from their next turn-start (global config, not pinned per game).
    /// </summary>
    public async Task Save(TurnTax tax)
    {
        _tax = tax;
        Enabled = _tax.TurnTaxEnabled;

        var content = JsonSerializer.Serialize(_tax, new JsonSerializerOptions { WriteIndented = true });
        var path = _filePathProvider.GetFilePath(FilePathProvider.FileCategory.Rules);
        await _filePathProvider.WriteFileAsync(path, _taxFileName, content);
    }
}