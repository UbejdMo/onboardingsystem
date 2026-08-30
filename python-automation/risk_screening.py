"""Risk screening job.

Fetches merchants awaiting a decision, scores them against explainable
rules, and writes each score back to the API. Intended to run on a
schedule (cron, Task Scheduler) or on demand.

Usage:
    python risk_screening.py
    python risk_screening.py --api-url http://localhost:5119 --dry-run
"""

from __future__ import annotations

import argparse
import logging
import sys

import requests

from risk_rules import score_merchant

DEFAULT_API_URL = "http://localhost:5119"
DEFAULT_TIMEOUT = 10

# Statuses worth screening. Approved and Rejected merchants have already had
# a human decision, so re-scoring them would only create noise.
SCREENABLE_STATUSES = ("Pending", "Flagged")

logger = logging.getLogger("risk_screening")


class ApiError(RuntimeError):
    """The API could not be reached or returned an unexpected response."""


def fetch_merchants(session: requests.Session, api_url: str, status: str) -> list[dict]:
    """Fetch merchants with the given status."""
    url = f"{api_url}/api/merchants"
    try:
        response = session.get(
            url, params={"status": status}, timeout=DEFAULT_TIMEOUT
        )
        response.raise_for_status()
        return response.json()
    except requests.RequestException as exc:
        raise ApiError(f"Failed to fetch {status} merchants from {url}: {exc}") from exc
    except ValueError as exc:
        raise ApiError(f"API returned invalid JSON when listing {status} merchants: {exc}") from exc


def submit_score(
    session: requests.Session, api_url: str, merchant_id: int, score: int
) -> dict:
    """Write a merchant's risk score back to the API."""
    url = f"{api_url}/api/merchants/{merchant_id}/risk-score"
    try:
        response = session.put(url, json={"riskScore": score}, timeout=DEFAULT_TIMEOUT)
        response.raise_for_status()
        return response.json()
    except requests.RequestException as exc:
        raise ApiError(f"Failed to submit score for merchant {merchant_id}: {exc}") from exc
    except ValueError as exc:
        raise ApiError(f"API returned invalid JSON for merchant {merchant_id}: {exc}") from exc


def collect_merchants(session: requests.Session, api_url: str) -> list[dict]:
    """Gather every merchant awaiting screening, de-duplicated by id."""
    merchants: dict[int, dict] = {}
    for status in SCREENABLE_STATUSES:
        for merchant in fetch_merchants(session, api_url, status):
            merchant_id = merchant.get("id")
            if merchant_id is not None:
                merchants[merchant_id] = merchant
    return [merchants[key] for key in sorted(merchants)]


def screen_merchants(api_url: str, dry_run: bool = False) -> int:
    """Score every screenable merchant. Returns the number of failures."""
    failures = 0

    with requests.Session() as session:
        merchants = collect_merchants(session, api_url)

        if not merchants:
            logger.info("No merchants awaiting screening.")
            return 0

        logger.info("Screening %d merchant(s).", len(merchants))

        for merchant in merchants:
            merchant_id = merchant.get("id")
            name = merchant.get("businessName", "<unknown>")
            assessment = score_merchant(merchant)

            logger.info(
                "Merchant %s (%s) scored %d%s",
                merchant_id,
                name,
                assessment.score,
                " - would be flagged" if assessment.would_flag else "",
            )
            for reason in assessment.reasons:
                logger.info("    %s", reason)

            if dry_run:
                continue

            # One merchant failing must not abandon the rest of the batch.
            try:
                updated = submit_score(session, api_url, merchant_id, assessment.score)
                logger.info(
                    "    saved: status is now %s", updated.get("status", "<unknown>")
                )
            except ApiError as exc:
                failures += 1
                logger.error("    %s", exc)

    return failures


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Score pending merchants and write the results back to the API."
    )
    parser.add_argument(
        "--api-url",
        default=DEFAULT_API_URL,
        help=f"Base URL of the merchant API (default: {DEFAULT_API_URL})",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Score and report without writing anything back.",
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Show debug output.",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)

    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(asctime)s %(levelname)-7s %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )

    api_url = args.api_url.rstrip("/")

    if args.dry_run:
        logger.info("Dry run: no scores will be written back.")

    try:
        failures = screen_merchants(api_url, dry_run=args.dry_run)
    except ApiError as exc:
        logger.error("%s", exc)
        return 1

    if failures:
        logger.error("Finished with %d failure(s).", failures)
        return 1

    logger.info("Screening complete.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
