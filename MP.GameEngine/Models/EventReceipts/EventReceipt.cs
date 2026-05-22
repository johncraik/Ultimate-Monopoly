using System.Text.Json.Serialization;

namespace MP.GameEngine.Models.EventReceipts;

[JsonPolymorphic]
[JsonDerivedType(typeof(DiceRollReceipt), "DiceRoll")]
[JsonDerivedType(typeof(PlayerMovedReceipt), "PlayerMoved")]
[JsonDerivedType(typeof(PlayerDirectionChangedReceipt), "PlayerDirectionChanged")]
[JsonDerivedType(typeof(PlayerLeftJailReceipt), "PlayerLeftJail")]
[JsonDerivedType(typeof(PlayerSwappedReceipt), "PlayerSwapped")]
[JsonDerivedType(typeof(PlayerBankruptedReceipt), "PlayerBankrupted")]
[JsonDerivedType(typeof(CardTakenReceipt), "CardTaken")]
[JsonDerivedType(typeof(CardPlayedReceipt), "CardPlayed")]
[JsonDerivedType(typeof(FinancialTransactionReceipt), "FinancialTransaction")]
[JsonDerivedType(typeof(PropertyTransactionReceipt), "PropertyTransaction")]
[JsonDerivedType(typeof(FreeParkingReceipt), "FreeParking")]
public abstract class EventReceipt
{
    public string PlayerId { get; set; }
}


//TODO Might be used later?? 
public enum EventReceiptType
{
    DiceRoll,
    PlayerMoved,
    PlayerDirectionChanged,
    PlayerLeftJail,
    PlayerSwapped,
    PlayerBankrupted,
    CardTaken,
    CardPlayed,
    FinancialTransaction,
    PropertyTransaction,
    FreeParking
}