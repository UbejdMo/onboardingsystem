using MerchantOnboarding.Api.Data;
using MerchantOnboarding.Api.Dtos;
using MerchantOnboarding.Api.Models;
using MerchantOnboarding.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MerchantOnboarding.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MerchantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMerchantService _merchantService;
    private readonly ILogger<MerchantsController> _logger;

    public MerchantsController(
        AppDbContext db,
        IMerchantService merchantService,
        ILogger<MerchantsController> logger)
    {
        _db = db;
        _merchantService = merchantService;
        _logger = logger;
    }

    /// <summary>Onboards a merchant after validating the submission.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MerchantResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MerchantResponse>> Create(
        [FromBody] CreateMerchantRequest request,
        CancellationToken cancellationToken)
    {
        var validation = _merchantService.Validate(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Merchant validation failed.",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["merchant"] = validation.Errors.ToArray() }
            });
        }

        var merchant = _merchantService.CreateMerchant(request);

        _db.Merchants.Add(merchant);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Onboarded merchant {MerchantId} from {Country} with status {Status}",
            merchant.Id, merchant.Country, merchant.Status);

        return CreatedAtAction(
            nameof(GetById),
            new { id = merchant.Id },
            MerchantResponse.FromMerchant(merchant));
    }

    /// <summary>Lists merchants, newest first, optionally filtered by status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MerchantResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<MerchantResponse>>> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        // AsNoTracking: these are read-only reads, so EF need not keep
        // change-tracking snapshots of every row it returns.
        var query = _db.Merchants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseStatus(status, out var parsed))
            {
                return BadRequest(ProblemForInvalidStatus(status));
            }

            query = query.Where(m => m.Status == parsed);
        }

        // Materialise first: FromMerchant is a plain C# method and EF Core
        // cannot translate it into SQL inside the query.
        var merchants = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken);

        return Ok(merchants.Select(MerchantResponse.FromMerchant).ToList());
    }

    /// <summary>Fetches a single merchant.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MerchantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MerchantResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var merchant = await _db.Merchants
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (merchant is null)
        {
            return NotFound(ProblemForMissingMerchant(id));
        }

        return Ok(MerchantResponse.FromMerchant(merchant));
    }

    /// <summary>Records a compliance decision against a merchant.</summary>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(MerchantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MerchantResponse>> UpdateStatus(
        int id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Status is required.",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["status"] = new[] { "A status value must be supplied." } }
            });
        }

        if (!TryParseStatus(request.Status, out var newStatus))
        {
            return BadRequest(ProblemForInvalidStatus(request.Status));
        }

        var merchant = await _db.Merchants
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (merchant is null)
        {
            return NotFound(ProblemForMissingMerchant(id));
        }

        var previousStatus = merchant.Status;
        merchant.Status = newStatus;
        await _db.SaveChangesAsync(cancellationToken);

        // A status change is a compliance decision, so leave an audit trail.
        _logger.LogInformation(
            "Merchant {MerchantId} status changed from {PreviousStatus} to {NewStatus}",
            merchant.Id, previousStatus, newStatus);

        return Ok(MerchantResponse.FromMerchant(merchant));
    }

    /// <summary>
    /// Records a computed risk score. Called by the screening job, which
    /// owns the scoring rules; the API owns what the score means.
    /// </summary>
    [HttpPut("{id:int}/risk-score")]
    [ProducesResponseType(typeof(MerchantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MerchantResponse>> UpdateRiskScore(
        int id,
        [FromBody] UpdateRiskScoreRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.RiskScore is null)
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Risk score is required.",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["riskScore"] = new[] { "A risk score must be supplied." } }
            });
        }

        var riskScore = request.RiskScore.Value;

        if (!_merchantService.IsValidRiskScore(riskScore))
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Invalid risk score.",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["riskScore"] = new[] { "Risk score must be between 0 and 100." } }
            });
        }

        var merchant = await _db.Merchants
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (merchant is null)
        {
            return NotFound(ProblemForMissingMerchant(id));
        }

        var previousStatus = merchant.Status;
        _merchantService.ApplyRiskScore(merchant, riskScore);
        await _db.SaveChangesAsync(cancellationToken);

        if (merchant.Status != previousStatus)
        {
            _logger.LogInformation(
                "Merchant {MerchantId} auto-flagged: risk score {RiskScore} reached threshold {Threshold}",
                merchant.Id, riskScore, _merchantService.HighRiskScoreThreshold);
        }
        else if (riskScore >= _merchantService.HighRiskScoreThreshold)
        {
            // High score on a merchant a human has already decided on. The
            // decision stands, but this must not pass silently.
            _logger.LogWarning(
                "Merchant {MerchantId} scored {RiskScore} (at or above threshold {Threshold}) " +
                "but keeps existing status {Status} set by a reviewer",
                merchant.Id, riskScore, _merchantService.HighRiskScoreThreshold, merchant.Status);
        }
        else
        {
            _logger.LogInformation(
                "Merchant {MerchantId} scored {RiskScore}, status unchanged at {Status}",
                merchant.Id, riskScore, merchant.Status);
        }

        return Ok(MerchantResponse.FromMerchant(merchant));
    }

    /// <summary>
    /// Parses a status name. Enum.TryParse alone would accept numeric input
    /// such as "99" and produce an undefined status, so the result is checked
    /// against the values actually declared.
    /// </summary>
    private static bool TryParseStatus(string? value, out MerchantStatus status)
    {
        status = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out status)
            && Enum.IsDefined(status);
    }

    private static ValidationProblemDetails ProblemForInvalidStatus(string status) => new()
    {
        Title = "Invalid status value.",
        Status = StatusCodes.Status400BadRequest,
        Errors =
        {
            ["status"] = new[]
            {
                $"'{status}' is not a valid status. Valid values: " +
                string.Join(", ", Enum.GetNames<MerchantStatus>()) + "."
            }
        }
    };

    private static ProblemDetails ProblemForMissingMerchant(int id) => new()
    {
        Title = "Merchant not found.",
        Detail = $"No merchant exists with id {id}.",
        Status = StatusCodes.Status404NotFound
    };
}
