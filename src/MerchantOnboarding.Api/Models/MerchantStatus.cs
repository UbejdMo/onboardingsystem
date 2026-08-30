namespace MerchantOnboarding.Api.Models;

/// <summary>
/// Where a merchant sits in the compliance workflow.
/// </summary>
public enum MerchantStatus
{
    /// <summary>Onboarded, awaiting a risk decision.</summary>
    Pending = 0,

    /// <summary>Cleared to trade.</summary>
    Approved = 1,

    /// <summary>Denied onboarding.</summary>
    Rejected = 2,

    /// <summary>Held for manual review by a compliance officer.</summary>
    Flagged = 3
}
