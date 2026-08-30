namespace MerchantOnboarding.Api.Dtos;

/// <summary>
/// Body the risk screening job posts back after scoring a merchant.
/// </summary>
public class UpdateRiskScoreRequest
{
    /// <summary>
    /// Risk score from 0 to 100. Nullable so a missing field is rejected
    /// as a clear error rather than silently defaulting to 0, which would
    /// read as "screened and clean".
    /// </summary>
    public int? RiskScore { get; set; }
}
