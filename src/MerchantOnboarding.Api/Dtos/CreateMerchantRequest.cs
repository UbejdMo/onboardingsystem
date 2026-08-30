namespace MerchantOnboarding.Api.Dtos;

/// <summary>
/// Fields a caller supplies when onboarding a merchant.
/// Separate from the Merchant entity so a caller cannot set
/// server-controlled fields such as Status, RiskScore or Id.
/// </summary>
public class CreateMerchantRequest
{
    public string? BusinessName { get; set; }

    public string? Email { get; set; }

    public string? Country { get; set; }

    public string? Description { get; set; }
}
