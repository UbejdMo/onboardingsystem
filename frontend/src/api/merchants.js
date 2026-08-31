// All backend calls live here so components deal in data, not URLs and
// response parsing.

const API_BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:8080'

/**
 * Error carrying the API's validation messages, so a form can show the
 * specific problems rather than a generic failure.
 */
export class ApiError extends Error {
  constructor(message, { status, validationErrors = [] } = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.validationErrors = validationErrors
  }
}

/**
 * The API returns RFC 7807 ProblemDetails on failure. Pull the useful parts
 * out of it, falling back to something readable if the body is not JSON.
 */
async function toApiError(response) {
  let body = null
  try {
    body = await response.json()
  } catch {
    // A non-JSON error body (a proxy error page, say) is not worth failing on.
  }

  if (!body) {
    return new ApiError(`Request failed (${response.status})`, {
      status: response.status,
    })
  }

  // ValidationProblemDetails groups messages by field; flatten them.
  const validationErrors = body.errors
    ? Object.values(body.errors).flat()
    : []

  return new ApiError(body.title ?? `Request failed (${response.status})`, {
    status: response.status,
    validationErrors,
  })
}

async function request(path, options = {}) {
  let response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      headers: { 'Content-Type': 'application/json' },
      ...options,
    })
  } catch (err) {
    // An abort is a cancelled render, not a network failure - rethrow it so
    // the caller can ignore it rather than showing a misleading error.
    if (err.name === 'AbortError') throw err

    // fetch only rejects when the request never completed - the API is down,
    // the network is gone, or CORS blocked it before a response arrived.
    throw new ApiError(
      'Could not reach the API. Check that it is running and try again.',
    )
  }

  if (!response.ok) {
    throw await toApiError(response)
  }

  return response.status === 204 ? null : response.json()
}

export function fetchMerchants(signal) {
  return request('/api/merchants', { signal })
}

export function createMerchant(merchant) {
  return request('/api/merchants', {
    method: 'POST',
    body: JSON.stringify(merchant),
  })
}
