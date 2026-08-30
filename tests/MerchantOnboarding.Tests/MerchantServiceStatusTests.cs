using MerchantOnboarding.Api.Dtos;
using MerchantOnboarding.Api.Models;
using MerchantOnboarding.Api.Services;

namespace MerchantOnboarding.Tests;

/// <summary>
/// Covers the status a merchant starts in and how a screening result changes it.
/// </summary>
public class MerchantServiceStatusTests
{
    private readonly MerchantService _service = new();

    private static CreateMerchantRequest RequestForCountry(string country) => new()
    {
        BusinessName = "Acme Payments Ltd",
        Email = "ops@acmepayments.com",
        Country = country,
        Description = "Online electronics retailer"
    };

    [Theory]
    [InlineData("IR")]
    [InlineData("KP")]
    [InlineData("SY")]
    [InlineData("CU")]
    [InlineData("AF")]
    public void DetermineInitialStatus_HighRiskCountry_ReturnsFlagged(string country)
    {
        Assert.Equal(MerchantStatus.Flagged, _service.DetermineInitialStatus(country));
    }

    [Theory]
    [InlineData("ir")]
    [InlineData("Kp")]
    [InlineData(" SY ")]
    public void DetermineInitialStatus_HighRiskCountryOddlyCased_ReturnsFlagged(string country)
    {
        Assert.Equal(MerchantStatus.Flagged, _service.DetermineInitialStatus(country));
    }

    [Theory]
    [InlineData("DE")]
    [InlineData("GB")]
    [InlineData("US")]
    [InlineData("XK")]
    public void DetermineInitialStatus_NormalCountry_ReturnsPending(string country)
    {
        Assert.Equal(MerchantStatus.Pending, _service.DetermineInitialStatus(country));
    }

    [Fact]
    public void CreateMerchant_HighRiskCountry_IsFlaggedForReview()
    {
        var merchant = _service.CreateMerchant(RequestForCountry("IR"));

        Assert.Equal(MerchantStatus.Flagged, merchant.Status);
    }

    [Fact]
    public void CreateMerchant_NormalCountry_StartsPendingAndUnscored()
    {
        var merchant = _service.CreateMerchant(RequestForCountry("DE"));

        Assert.Equal(MerchantStatus.Pending, merchant.Status);
        Assert.Null(merchant.RiskScore);
    }

    [Fact]
    public void CreateMerchant_NormalisesStoredValues()
    {
        var request = new CreateMerchantRequest
        {
            BusinessName = "  Acme Payments Ltd  ",
            Email = "  ops@acmepayments.com  ",
            Country = "de",
            Description = "   "
        };

        var merchant = _service.CreateMerchant(request);

        Assert.Equal("Acme Payments Ltd", merchant.BusinessName);
        Assert.Equal("ops@acmepayments.com", merchant.Email);
        Assert.Equal("DE", merchant.Country);
        Assert.Null(merchant.Description);
        Assert.Equal(DateTimeKind.Utc, merchant.CreatedAt.Kind);
    }

    [Fact]
    public void CreateMerchant_InvalidRequest_Throws()
    {
        var request = RequestForCountry("DE");
        request.Email = "not-an-email";

        Assert.Throws<InvalidOperationException>(() => _service.CreateMerchant(request));
    }
}
