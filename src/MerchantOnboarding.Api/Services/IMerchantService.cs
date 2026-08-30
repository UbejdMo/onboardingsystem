using MerchantOnboarding.Api.Dtos;
using MerchantOnboarding.Api.Models;

namespace MerchantOnboarding.Api.Services;

/// <summary>
/// Onboarding rules for merchants: what makes a submission valid, and what
/// status a valid submission starts life in.
/// </summary>
public interface IMerchantService
{
    /// <summary>Checks an onboarding request against the business rules.</summary>
    ValidationResult Validate(CreateMerchantRequest request);

    /// <summary>
    /// Decides the status a newly onboarded merchant starts in:
    /// high-risk countries are held for manual review.
    /// </summary>
    MerchantStatus DetermineInitialStatus(string country);

    /// <summary>
    /// Builds a Merchant from a validated request, applying the initial
    /// status and normalising the stored values.
    /// </summary>
    Merchant CreateMerchant(CreateMerchantRequest request);
}
