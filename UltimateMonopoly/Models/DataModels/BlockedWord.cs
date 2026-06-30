using System.ComponentModel.DataAnnotations;
using JC.Core.Models.Auditing;

namespace UltimateMonopoly.Models.DataModels;

public class BlockedWord : AuditModel
{
    [Key]
    public string NormalisedWord { get; set; }
    
    [Required]
    public string Word { get; set; }
}