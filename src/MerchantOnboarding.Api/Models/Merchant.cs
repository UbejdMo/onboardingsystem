using System.ComponentModel.DataAnnotations;

namespace MerchantOnboarding.Api.Models;

/// <summary>
/// A business applying to process payments through the platform.
/// </summary>
public class Merchant
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string BusinessName { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Two-letter ISO 3166-1 alpha-2 country code, stored uppercase.</summary>
    [Required]
    [MaxLength(2)]
    public string Country { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Risk score from 0-100, written back by the screening job.
    /// Null until the merchant has been screened.
    /// </summary>
    public int? RiskScore { get; set; }

    public MerchantStatus Status { get; set; } = MerchantStatus.Pending;

    public DateTime CreatedAt { get; set; }
}
