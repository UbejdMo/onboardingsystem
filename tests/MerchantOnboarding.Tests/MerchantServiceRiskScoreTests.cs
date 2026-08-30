using MerchantOnboarding.Api.Models;
using MerchantOnboarding.Api.Services;

namespace MerchantOnboarding.Tests;

/// <summary>
/// Covers what a screening result does to a merchant.
/// </summary>
public class MerchantServiceRiskScoreTests
{
    private readonly MerchantService _service = new();

    private static Merchant PendingMerchant() => new()
    {
        Id = 1,
        BusinessName = "Acme Payments Ltd",
        Email = "ops@acmepayments.com",
        Country = "DE",
        Status = MerchantStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(69)]
    public void ApplyRiskScore_BelowThreshold_StaysPending(int score)
    {
        var merchant = PendingMerchant();

        _service.ApplyRiskScore(merchant, score);

        Assert.Equal(score, merchant.RiskScore);
        Assert.Equal(MerchantStatus.Pending, merchant.Status);
    }

    [Theory]
    [InlineData(70)]
    [InlineData(85)]
    [InlineData(100)]
    public void ApplyRiskScore_AtOrAboveThreshold_IsFlagged(int score)
    {
        var merchant = PendingMerchant();

        _service.ApplyRiskScore(merchant, score);

        Assert.Equal(score, merchant.RiskScore);
        Assert.Equal(MerchantStatus.Flagged, merchant.Status);
    }

    /// <summary>The threshold is inclusive, so exactly 70 must flag.</summary>
    [Fact]
    public void ApplyRiskScore_ExactlyAtThreshold_IsFlagged()
    {
        var merchant = PendingMerchant();

        _service.ApplyRiskScore(merchant, _service.HighRiskScoreThreshold);

        Assert.Equal(MerchantStatus.Flagged, merchant.Status);
    }

    /// <summary>
    /// Screening escalates but never overturns a human decision: a low score
    /// must not quietly clear a merchant a compliance officer rejected.
    /// </summary>
    [Theory]
    [InlineData(MerchantStatus.Approved)]
    [InlineData(MerchantStatus.Rejected)]
    [InlineData(MerchantStatus.Flagged)]
    public void ApplyRiskScore_LowScore_DoesNotOverturnReviewedStatus(MerchantStatus existing)
    {
        var merchant = PendingMerchant();
        merchant.Status = existing;

        _service.ApplyRiskScore(merchant, 5);

        Assert.Equal(existing, merchant.Status);
        Assert.Equal(5, merchant.RiskScore);
    }

    /// <summary>
    /// A high score on an already-decided merchant records the score but
    /// leaves the decision alone; the API logs a warning for a human instead.
    /// </summary>
    [Fact]
    public void ApplyRiskScore_HighScoreOnApprovedMerchant_KeepsApprovedButRecordsScore()
    {
        var merchant = PendingMerchant();
        merchant.Status = MerchantStatus.Approved;

        _service.ApplyRiskScore(merchant, 95);

        Assert.Equal(MerchantStatus.Approved, merchant.Status);
        Assert.Equal(95, merchant.RiskScore);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ApplyRiskScore_OutOfRange_Throws(int score)
    {
        var merchant = PendingMerchant();

        Assert.Throws<ArgumentOutOfRangeException>(() => _service.ApplyRiskScore(merchant, score));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void IsValidRiskScore_ChecksRange(int score, bool expected)
    {
        Assert.Equal(expected, _service.IsValidRiskScore(score));
    }
}
