using MerchantOnboarding.Api.Dtos;
using MerchantOnboarding.Api.Services;

namespace MerchantOnboarding.Tests;

/// <summary>
/// Covers the onboarding rules. MerchantService touches no database, so
/// these run against a plain instance with no fixtures or mocks.
/// </summary>
public class MerchantServiceValidationTests
{
    private readonly MerchantService _service = new();

    /// <summary>A submission that should pass; individual tests spoil one field.</summary>
    private static CreateMerchantRequest ValidRequest() => new()
    {
        BusinessName = "Acme Payments Ltd",
        Email = "ops@acmepayments.com",
        Country = "DE",
        Description = "Online electronics retailer"
    };

    [Fact]
    public void Validate_ValidMerchant_Passes()
    {
        var result = _service.Validate(ValidRequest());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingEmail_Fails()
    {
        var request = ValidRequest();
        request.Email = null;

        var result = _service.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("user@localhost")]
    [InlineData("spaces in@example.com")]
    [InlineData("@example.com")]
    public void Validate_InvalidEmail_Fails(string email)
    {
        var request = ValidRequest();
        request.Email = email;

        var result = _service.Validate(request);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("USA")]  // three letters
    [InlineData("D")]    // one letter
    [InlineData("12")]   // digits
    [InlineData("D1")]   // mixed
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_InvalidCountryCode_Fails(string country)
    {
        var request = ValidRequest();
        request.Country = country;

        var result = _service.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Country", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("DE")]
    [InlineData("de")]
    [InlineData("Gb")]
    public void Validate_CountryCodeIsCaseInsensitive_Passes(string country)
    {
        var request = ValidRequest();
        request.Country = country;

        Assert.True(_service.Validate(request).IsValid);
    }

    [Fact]
    public void Validate_MissingBusinessName_Fails()
    {
        var request = ValidRequest();
        request.BusinessName = "   ";

        var result = _service.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Business name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MissingDescription_IsAllowed()
    {
        var request = ValidRequest();
        request.Description = null;

        Assert.True(_service.Validate(request).IsValid);
    }

    [Fact]
    public void Validate_OverLongBusinessName_Fails()
    {
        var request = ValidRequest();
        request.BusinessName = new string('x', 201);

        Assert.False(_service.Validate(request).IsValid);
    }

    [Fact]
    public void Validate_OverLongDescription_Fails()
    {
        var request = ValidRequest();
        request.Description = new string('x', 2001);

        Assert.False(_service.Validate(request).IsValid);
    }

    /// <summary>
    /// Every problem is reported at once so a caller can fix a submission
    /// in a single round trip rather than one field at a time.
    /// </summary>
    [Fact]
    public void Validate_MultipleProblems_ReportsAllOfThem()
    {
        var request = new CreateMerchantRequest
        {
            BusinessName = null,
            Email = null,
            Country = null
        };

        var result = _service.Validate(request);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }
}
