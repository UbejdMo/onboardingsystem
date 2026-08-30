using MerchantOnboarding.Api.Models;

namespace MerchantOnboarding.Api.Dtos;

/// <summary>
/// Shape returned to callers. Keeping this separate from the entity means
/// adding an internal column later does not silently change the public API.
/// </summary>
public class MerchantResponse
{
    public int Id { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? RiskScore { get; set; }

    /// <summary>Serialised as its name ("Pending"), not its numeric value.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public static MerchantResponse FromMerchant(Merchant merchant) => new()
    {
        Id = merchant.Id,
        BusinessName = merchant.BusinessName,
        Email = merchant.Email,
        Country = merchant.Country,
        Description = merchant.Description,
        RiskScore = merchant.RiskScore,
        Status = merchant.Status.ToString(),
        CreatedAt = merchant.CreatedAt
    };
}
