"""Rule-based risk scoring for merchant screening.

Kept separate from the HTTP client so the scoring rules can be read,
reviewed and tested on their own - which is the whole point of scoring
this way rather than asking a model for a number.
"""

from dataclasses import dataclass, field

# Countries subject to sanctions or heightened AML scrutiny. Mirrors the
# API's own list; the API flags at onboarding, this job scores.
HIGH_RISK_COUNTRIES = {
    "IR": "Iran",
    "KP": "North Korea",
    "SY": "Syria",
    "CU": "Cuba",
    "AF": "Afghanistan",
}

HIGH_RISK_COUNTRY_POINTS = 40

# Business descriptions that warrant a closer look. Each keyword carries its
# own weight so a reviewer can see which words drove a score.
#
# Matching is plain substring matching, so "cryptography" matches "crypto".
# That is a deliberate trade-off: this job only raises merchants for human
# review, never rejects them, so a false positive costs a few minutes of a
# reviewer's time while a false negative lets a risky merchant through.
KEYWORD_POINTS = {
    "crypto": 20,
    "cryptocurrency": 20,
    "bitcoin": 20,
    "casino": 25,
    "gambling": 25,
    "betting": 20,
    "adult": 15,
    "escort": 25,
    "weapons": 30,
    "firearms": 30,
    "ammunition": 25,
    "tobacco": 10,
    "vape": 10,
    "pharmacy": 15,
    "pharmaceutical": 15,
    "supplements": 10,
    "forex": 20,
    "binary options": 25,
    "offshore": 20,
    "shell company": 30,
    "cash intensive": 20,
    "money transfer": 20,
    "remittance": 15,
    "prepaid cards": 15,
}

# A merchant that gives no idea what it does is itself a small risk signal.
MISSING_DESCRIPTION_POINTS = 10

MAX_SCORE = 100

# Must match HighRiskScoreThreshold in MerchantService; the API applies it,
# this is only used to explain the outcome in the job's output.
FLAG_THRESHOLD = 70


@dataclass
class RiskAssessment:
    """A score plus the reasons behind it."""

    score: int
    reasons: list[str] = field(default_factory=list)

    @property
    def would_flag(self) -> bool:
        return self.score >= FLAG_THRESHOLD


def score_merchant(merchant: dict) -> RiskAssessment:
    """Score one merchant, returning the score and why it got that score.

    Points accumulate and are capped at 100. Every rule that fires adds a
    human-readable reason, so a compliance officer can audit the decision.
    """
    points = 0
    reasons: list[str] = []

    country = (merchant.get("country") or "").strip().upper()
    if country in HIGH_RISK_COUNTRIES:
        points += HIGH_RISK_COUNTRY_POINTS
        reasons.append(
            f"High-risk country: {HIGH_RISK_COUNTRIES[country]} ({country}) "
            f"+{HIGH_RISK_COUNTRY_POINTS}"
        )

    description = (merchant.get("description") or "").strip()
    if not description:
        points += MISSING_DESCRIPTION_POINTS
        reasons.append(f"No business description provided +{MISSING_DESCRIPTION_POINTS}")
    else:
        haystack = description.lower()
        # Sorted for deterministic output: the same merchant always produces
        # the same reason list, which matters for auditability.
        for keyword in sorted(KEYWORD_POINTS):
            if keyword in haystack:
                weight = KEYWORD_POINTS[keyword]
                points += weight
                reasons.append(f"Description mentions '{keyword}' +{weight}")

    capped = min(points, MAX_SCORE)
    if capped != points:
        reasons.append(f"Total capped at {MAX_SCORE} (raw score {points})")

    if not reasons:
        reasons.append("No risk indicators matched")

    return RiskAssessment(score=capped, reasons=reasons)
