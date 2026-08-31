# Merchant Onboarding & Risk Screening Platform

A small fintech-style fullstack app for onboarding merchants and tracking
their compliance status: an ASP.NET Core Web API backed by MySQL, a React
frontend for reviewing and deciding on merchants, and a Python automation job
that scores pending merchants against explainable risk rules and writes the
results back through the API.

It mirrors a real payments-company workflow — merchant onboarding, compliance
screening, an internal REST API, a review UI, and a scheduled screening job.

## Architecture

```
   ┌──────────────────────────┐   ┌──────────────────────────┐
   │  React frontend (Vite)   │   │  Python screening job    │
   │  list · onboard · decide │   │  risk_screening.py       │
   └───────────┬──────────────┘   └───────────┬──────────────┘
      GET / POST / PUT                GET pending/flagged
      (browser, needs CORS)           PUT risk scores
               │                                │
               └────────────────┬───────────────┘
                                │
                  ┌─────────────▼────────────┐
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

Both the frontend and the Python job are *clients*. Neither owns any business
rule: the job computes scores but the API decides what a score means, and the
UI renders decisions but the API decides what is valid. Validation lives in one
place, so the rules cannot drift apart between callers.

## Tech stack

| Component | Choice |
|---|---|
| API | ASP.NET Core Web API (.NET 10, controller-based) |
| ORM | EF Core 9 + Pomelo.EntityFrameworkCore.MySql 9.0.0 |
| Database | MySQL 8 |
| Frontend | React 19 + Vite, plain `fetch` (no state library) |
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

## Quick start with Docker

The fastest way to get a working stack - no .NET SDK or MySQL install needed:

```bash
docker compose up -d --build
```

That starts MySQL 8 and the API together. The API waits for the database to
report *healthy* (not merely started) and applies the EF Core migrations on
boot, so the schema is ready without any extra step.

The API is then on `http://localhost:8080`:

```bash
curl http://localhost:8080/api/merchants
```

### Then start the frontend

`docker compose` runs the backend only - the frontend runs from its own dev
server, which is what gives you hot reloading while working on it:

```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173` for the merchant review UI. It is preconfigured
to call the API on port 8080, so with the stack up it works with no further
setup.

Run the screening job against it:

```bash
cd python-automation
pip install -r requirements.txt
python risk_screening.py --api-url http://localhost:8080
```

Stop the stack with `docker compose down`, or `docker compose down -v` to
delete the database volume as well.

The MySQL password defaults to `devpassword` and can be overridden with the
`MYSQL_ROOT_PASSWORD` environment variable (or a `.env` file).

### Running the tests in Docker

```bash
docker build -f Dockerfile.test -t merchant-tests .
docker run --rm merchant-tests
```

Useful on any machine whose security policy blocks running locally-built
binaries - Windows Smart App Control, for instance, blocks unsigned freshly
compiled assemblies, which stops `dotnet test` from loading the test DLL.

## Running locally without Docker

### Prerequisites

- .NET 10 SDK
- MySQL 8 (running locally, or via Docker)
- Node.js 20+ (for the frontend)
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

### 5. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`. The dev server port is pinned in
`vite.config.js` - it has to match the origin the API allows through CORS, so
Vite is configured to fail rather than silently move to another port.

By default the frontend calls `http://localhost:8080` (the Docker API). To
point it somewhere else - the local `dotnet run` on port 5119, say - create
`frontend/.env.local`:

```
VITE_API_URL=http://localhost:5119
```

Whichever origin serves the frontend must be listed under
`Cors:AllowedOrigins` in `appsettings.json`, or the browser will block every
request.

### 6. Run the screening job

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

## The review UI

`frontend/` is a React app for the people who actually make onboarding
decisions. Three things, matching the compliance workflow:

- **The merchant queue** - business name, country, status badge and risk
  score, newest first.
- **An onboarding form** - submits a new merchant and shows the API's
  validation messages if it is rejected.
- **Approve / Reject** on each row, writing the decision back through
  `PUT /api/merchants/{id}/status`.

It deliberately holds no business logic. Validation errors are the API's own
messages rather than rules reimplemented in JavaScript, so the two can never
disagree - the email field is even a plain text input, so the browser's
built-in check cannot pre-empt the server's answer.

Three details worth knowing:

- **A null risk score renders as "Not screened", never `0`.** The distinction
  the database and DTOs protect would be lost if the UI printed a raw value.
- **Status badges always show the status word**, not colour alone - red and
  green are the pair most affected by colour blindness, and here they mean
  rejected and approved.
- **Decisions are not applied optimistically.** The badge changes only after
  the API confirms the write. If the request fails the row is left alone and
  the error names the merchant, so nobody is told a decision was recorded when
  it was not.

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
- **Migrations apply at startup only when `ApplyMigrationsAtStartup` is set**,
  which docker-compose does. It keeps the container self-contained; a real
  deployment would run migrations as a separate, deliberate step rather than
  on every boot.
- **The API image runs as a non-root user** and carries only the ASP.NET
  runtime - the SDK, compiler and source stay in the discarded build stage.
- **CORS is restricted to the frontend's origin**, not a wildcard. The browser
  blocks cross-origin calls unless the API opts in; a wildcard would let any
  site a user visits call this API from their browser.
- **The Vite dev port is pinned with `strictPort`.** Vite would otherwise move
  to another port when 5173 is busy, and every API call would then be blocked
  by CORS with a confusing error. Failing loudly is better.

## Known limitations

Deliberate scope choices for a demonstration project, not oversights:

- **No authentication.** Every endpoint is open, and the UI has no login, so
  anyone who can reach it can approve a merchant. Real deployment needs
  authentication and role-based authorisation — writing a risk score should be
  restricted to the screening job's identity, and approving a merchant to a
  compliance officer.
- **The frontend has no automated tests.** The backend service layer is
  covered by xUnit; the UI was verified by hand.
- **No pagination in the UI** either — it renders whatever the API returns.
- **No pagination** on `GET /api/merchants`.
- **The high-risk country list and keyword weights are hardcoded.** In
  production these belong in configuration so compliance staff can change them
  without a redeploy.
- **Country validation is a format check**, not a lookup against the real ISO
  3166 list, so `ZZ` is accepted while `USA` is rejected.
- **The screening threshold is duplicated** between the API and the Python job.
  The API is authoritative; the job's copy only annotates its own output.
