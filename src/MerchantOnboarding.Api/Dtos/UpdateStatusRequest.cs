namespace MerchantOnboarding.Api.Dtos;

/// <summary>
/// Body for a compliance decision on a merchant.
/// </summary>
public class UpdateStatusRequest
{
    /// <summary>
    /// Target status: Pending, Approved, Rejected or Flagged.
    /// Parsed as a string so an invalid value returns a clear 400 rather
    /// than binding silently to an out-of-range enum number.
    /// </summary>
    public string? Status { get; set; }
}
