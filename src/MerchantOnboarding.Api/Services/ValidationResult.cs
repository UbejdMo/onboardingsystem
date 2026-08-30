namespace MerchantOnboarding.Api.Services;

/// <summary>
/// Outcome of validating an onboarding request. Carries every problem found
/// rather than just the first, so a caller can fix their submission in one go.
/// </summary>
public class ValidationResult
{
    private readonly List<string> _errors = new();

    public IReadOnlyList<string> Errors => _errors;

    public bool IsValid => _errors.Count == 0;

    public void AddError(string message) => _errors.Add(message);
}
