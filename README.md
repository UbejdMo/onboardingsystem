# Merchant Onboarding & Risk Screening API

A small fintech-style backend for onboarding merchants and tracking their
compliance status: an ASP.NET Core Web API backed by MySQL, plus a Python
automation job that scores pending merchants against explainable risk rules
and writes the results back through the API.

It mirrors a real payments-company workflow — merchant onboarding, compliance
screening, an internal REST API, and a scheduled screening job.

## Architecture

```
                  ┌──────────────────────────┐
                  │  Python screening job    │
                  │  risk_screening.py       │
                  └───────────┬──────────────┘
                     GET pending/flagged
                     PUT risk scores
                              │
                  ┌───────────▼──────────────┐
                  │  ASP.NET Core Web API    │
                  │  MerchantsController     │  ← HTTP, status codes
                  ├──────────────────────────┤
                  │  MerchantService         │  ← business rules
                  ├──────────────────────────┤
                  │  AppDbContext (EF Core)  │  ← persistence
                  └───────────┬──────────────┘
                              │
                     ┌────────▼────────┐
                     │     MySQL 8     │
                     └─────────────────┘
```

The layers are kept deliberately separate:

- **Controller** — translates HTTP to business calls and back. It decides
  status codes, not policy.
- **Service** — owns every onboarding rule: what makes a merchant valid, what
  status it starts in, and what a risk score means. It touches no database,
  which is what lets the unit tests run with no infrastructure at all.
- **DbContext** — persistence only.

The Python job is a *client*. It computes scores but does not decide what they
mean; the API owns the flagging threshold.

## Tech stack

| Component | Choice |
|---|---|
| API | ASP.NET Core Web API (.NET 10, controller-based) |
| ORM | EF Core 9 + Pomelo.EntityFrameworkCore.MySql 9.0.0 |
| Database | MySQL 8 |
| Tests | xUnit |
| Automation | Python 3 + `requests` |

> **Note on versions:** the API targets .NET 10, but the EF Core packages are
> pinned to 9.x. Pomelo, the MySQL provider, has no .NET 10 release yet and
> requires EF Core 9. Letting EF Core float to 10 breaks the restore.

## Data model

`Merchant`: `Id`, `BusinessName`, `Email`, `Country` (2-letter ISO code),
`Description`, `RiskScore` (nullable), `Status`, `CreatedAt`.

`Status` is one of `Pending`, `Approved`, `Rejected`, `Flagged`, stored as a
string so rows stay readable in the database and adding a status later cannot
renumber existing records.

`RiskScore` is **nullable on purpose**. "Not yet screened" and "screened and
scored zero" are different facts, and a non-nullable `int` would silently
collapse them into `0`.

## Getting started

### Prerequisites

- .NET 10 SDK
- MySQL 8 (running locally, or via Docker)
- Python 3.10+

### 1. Configure the database

Set your MySQL password in `src/MerchantOnboarding.Api/appsettings.json`
(the committed value is a placeholder):

```json
"ConnectionStrings": {
  "DefaultConnection": "server=localhost;port=3306;database=merchant_onboarding;user=root;password=CHANGE_ME"
}
```

For real work, prefer user secrets over editing the file:

```bash
cd src/MerchantOnboarding.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "server=localhost;port=3306;database=merchant_onboarding;user=root;password=yourpassword"
```

### 2. Create the schema

```bash
dotnet tool restore                       # installs dotnet-ef at the pinned version
dotnet ef database update --project src/MerchantOnboarding.Api
```

### 3. Run the API

```bash
dotnet run --project src/MerchantOnboarding.Api
```

Listens on `http://localhost:5119`. In Development the OpenAPI document is
served at `/openapi/v1.json`.

### 4. Run the tests

```bash
dotnet test
```

### 5. Run the screening job

```bash
cd python-automation
python -m venv .venv
source .venv/bin/activate        # Windows: .venv\Scripts\activate
pip install -r requirements.txt

python risk_screening.py --dry-run     # score and report, write nothing
python risk_screening.py               # score and write back
```

Options: `--api-url` (default `http://localhost:5119`), `--dry-run`,
`--verbose`. Exits `0` on success and `1` on failure, so it can be scheduled
with cron or Task Scheduler and alerted on.

## API endpoints

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/merchants` | Onboard a merchant (validates the submission) |
| `GET` | `/api/merchants` | List merchants, newest first; `?status=` filters |
| `GET` | `/api/merchants/{id}` | Fetch one merchant |
| `PUT` | `/api/merchants/{id}/status` | Record a compliance decision |
| `PUT` | `/api/merchants/{id}/risk-score` | Write back a computed score |

`src/MerchantOnboarding.Api/MerchantOnboarding.Api.http` holds ready-made
requests for every endpoint, including the error cases.

### Onboarding a merchant

```bash
curl -X POST http://localhost:5119/api/merchants \
  -H "Content-Type: application/json" \
  -d '{
    "businessName": "Acme Payments Ltd",
    "email": "ops@acmepayments.com",
    "country": "DE",
    "description": "Online electronics retailer"
  }'
```

Validation requires a business name, a well-formed email, and a two-letter
country code. **All** failures are returned at once, so a caller can fix a
submission in one round trip rather than one field at a time. Merchants from a
small hardcoded set of high-risk countries are onboarded as `Flagged` for
manual review; everyone else starts `Pending`.

Errors use the standard `ProblemDetails` format (RFC 7807).

## How risk screening works

The Python job fetches `Pending` and `Flagged` merchants — it skips `Approved`
and `Rejected` ones, which already have a human decision — scores each, and
writes the score back.

Scoring is additive and capped at 100:

| Signal | Points |
|---|---|
| High-risk country (5 countries) | +40 |
| Description keywords (24 terms, individually weighted) | +10 to +30 each |
| No description provided | +10 |

The API flags any merchant scoring **70 or above**.

Every score comes with its reasons:

```
Merchant 2 (Desert Trading) scored 85 - would be flagged
    High-risk country: Iran (IR) +40
    Description mentions 'crypto' +20
    Description mentions 'gambling' +25
```

### Scoring only ever escalates

A high score can flag a merchant, but a low score **never** clears one. If a
compliance officer has already Approved or Rejected a merchant, the automated
job will not overturn that decision — it records the score and, where the score
is high, logs a warning for a human to look at. Letting a nightly script
silently un-flag a merchant a person deliberately flagged is the kind of thing
that becomes a regulatory finding.

## Why the rules are hardcoded, not an LLM call

This is the central design decision, and it is deliberate.

A language model could plausibly read a business description and return a risk
score. It would even be less code. It would also be the wrong tool here:

1. **Explainability is a regulatory requirement, not a nice-to-have.** A
   merchant who is declined can ask why, and a regulator can audit the answer.
   "Iran +40, 'crypto' +20, 'gambling' +25 = 85" is a defensible answer.
   "The model said 85" is not.

2. **The same input must always produce the same output.** Screening the same
   merchant twice must not yield 62 and then 71 — one below the threshold and
   one above. These rules are deterministic; model output is not, and a
   compliance decision that changes between runs is indefensible.

3. **Rules can be unit tested; a model's judgement cannot.** Every rule here
   has a test pinning its exact behaviour.

4. **Changes must be reviewable.** Adjusting a weight is a one-line diff a
   compliance officer can read and approve in a pull request. Changing a
   prompt has effects nobody can fully predict from the diff.

5. **No merchant data leaves the system.** Business descriptions are commercial
   information; scoring locally sends nothing to a third party.

The honest limitation: substring matching produces false positives —
"cryptography software" matches the `crypto` keyword. That is the intended
direction to err. This job only *raises merchants for human review*; it never
rejects one. A false positive costs a reviewer a few minutes; a false negative
lets a risky merchant through.

Where an LLM *would* genuinely help is as a pre-processing step — summarising a
long description, or normalising free text into structured categories — with
the scoring itself left deterministic and auditable.

## Design notes

- **DTOs are separate from entities.** If `POST /api/merchants` bound directly
  to the `Merchant` entity, a caller could submit `{"status": "Approved"}` and
  approve themselves. The request DTO exposes only the four fields a caller may
  set; `Id`, `Status` and `RiskScore` stay server-controlled.
- **The MySQL server version is pinned, not auto-detected.**
  `ServerVersion.AutoDetect` opens a connection at startup, which would stop the
  app from booting — and block migrations — without a live database.
- **`AsNoTracking()` on read endpoints.** EF Core keeps change-tracking
  snapshots by default; read-only queries never need them.
- **Status changes are logged** with the old and new value. A compliance
  decision needs an audit trail.

## Known limitations

Deliberate scope choices for a demonstration project, not oversights:

- **No authentication.** Every endpoint is open. Real deployment needs
  authentication and role-based authorisation — writing a risk score should be
  restricted to the screening job's identity.
- **No pagination** on `GET /api/merchants`.
- **The high-risk country list and keyword weights are hardcoded.** In
  production these belong in configuration so compliance staff can change them
  without a redeploy.
- **Country validation is a format check**, not a lookup against the real ISO
  3166 list, so `ZZ` is accepted while `USA` is rejected.
- **The screening threshold is duplicated** between the API and the Python job.
  The API is authoritative; the job's copy only annotates its own output.
