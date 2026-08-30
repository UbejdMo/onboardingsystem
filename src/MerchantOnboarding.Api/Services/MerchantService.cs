using System.Net.Mail;
using MerchantOnboarding.Api.Dtos;
using MerchantOnboarding.Api.Models;

namespace MerchantOnboarding.Api.Services;

/// <inheritdoc />
public class MerchantService : IMerchantService
{
    /// <summary>
    /// Countries that trigger a manual compliance review at onboarding.
    /// Hardcoded deliberately: the list is small, must be auditable, and
    /// a reviewer should be able to see exactly why a merchant was held.
    /// In production this would move to configuration so compliance staff
    /// could change it without a redeploy.
    /// </summary>
    private static readonly HashSet<string> HighRiskCountries =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "IR", // Iran
            "KP", // North Korea
            "SY", // Syria
            "CU", // Cuba
            "AF"  // Afghanistan
        };

    public ValidationResult Validate(CreateMerchantRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(request.BusinessName))
        {
            result.AddError("Business name is required.");
        }
        else if (request.BusinessName.Trim().Length > 200)
        {
            result.AddError("Business name must be 200 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            result.AddError("Email is required.");
        }
        else if (!IsValidEmail(request.Email.Trim()))
        {
            result.AddError("Email is not a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(request.Country))
        {
            result.AddError("Country is required.");
        }
        else if (!IsValidCountryCode(request.Country.Trim()))
        {
            result.AddError("Country must be a two-letter ISO 3166-1 alpha-2 code.");
        }

        if (request.Description is not null && request.Description.Trim().Length > 2000)
        {
            result.AddError("Description must be 2000 characters or fewer.");
        }

        return result;
    }

    public MerchantStatus DetermineInitialStatus(string country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return MerchantStatus.Pending;
        }

        return HighRiskCountries.Contains(country.Trim())
            ? MerchantStatus.Flagged
            : MerchantStatus.Pending;
    }

    public Merchant CreateMerchant(CreateMerchantRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = Validate(request);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "Cannot create a merchant from an invalid request. Call Validate first.");
        }

        var country = request.Country!.Trim().ToUpperInvariant();
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        return new Merchant
        {
            BusinessName = request.BusinessName!.Trim(),
            Email = request.Email!.Trim(),
            Country = country,
            Description = description,
            RiskScore = null,
            Status = DetermineInitialStatus(country),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Two ASCII letters. Deliberately a format check, not a lookup against
    /// the real ISO list - the point is to reject obvious junk like "USA"
    /// or "1" before it reaches the database.
    /// </summary>
    private static bool IsValidCountryCode(string country)
    {
        return country.Length == 2
            && char.IsAsciiLetter(country[0])
            && char.IsAsciiLetter(country[1]);
    }

    private static bool IsValidEmail(string email)
    {
        // MailAddress accepts some things a stricter check would not, but it
        // rejects the malformed input that matters here without shipping a
        // fragile hand-rolled regex.
        if (!MailAddress.TryCreate(email, out var address))
        {
            return false;
        }

        // MailAddress accepts a bare host such as "user@localhost";
        // an onboarding email should have a dotted domain.
        var host = address.Host;
        return host.Contains('.')
            && !host.StartsWith('.')
            && !host.EndsWith('.')
            && address.Address == email;
    }
}
