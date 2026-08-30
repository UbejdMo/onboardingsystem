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

    /// <summary>The inclusive score at or above which a merchant is held for review.</summary>
    int HighRiskScoreThreshold { get; }

    /// <summary>Whether a risk score is within the accepted 0-100 range.</summary>
    bool IsValidRiskScore(int riskScore);

    /// <summary>
    /// Applies a screening result to a merchant: records the score and holds
    /// the merchant for review if it reaches the high-risk threshold.
    /// </summary>
    void ApplyRiskScore(Merchant merchant, int riskScore);
}
