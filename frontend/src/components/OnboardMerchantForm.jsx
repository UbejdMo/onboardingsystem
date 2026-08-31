import { useState } from 'react'
import { createMerchant } from '../api/merchants'
import './OnboardMerchantForm.css'

const EMPTY_FORM = {
  businessName: '',
  email: '',
  country: '',
  description: '',
}

function OnboardMerchantForm({ onMerchantCreated }) {
  const [form, setForm] = useState(EMPTY_FORM)
  const [errors, setErrors] = useState([])
  const [isSubmitting, setIsSubmitting] = useState(false)

  function handleChange(event) {
    const { name, value } = event.target
    setForm((current) => ({ ...current, [name]: value }))
  }

  async function handleSubmit(event) {
    event.preventDefault()

    setIsSubmitting(true)
    setErrors([])

    try {
      const created = await createMerchant({
        businessName: form.businessName,
        email: form.email,
        country: form.country,
        // An untouched description should be absent, not an empty string.
        description: form.description.trim() === '' ? null : form.description,
      })

      setForm(EMPTY_FORM)
      onMerchantCreated(created)
    } catch (err) {
      // The API returns every validation problem at once, so show them all
      // rather than only the first.
      setErrors(
        err.validationErrors?.length
          ? err.validationErrors
          : [err.message ?? 'Something went wrong. Please try again.'],
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form className="onboard-form" onSubmit={handleSubmit} noValidate>
      <h2>Onboard a merchant</h2>

      {errors.length > 0 && (
        <div className="form-errors" role="alert">
          <p className="form-errors__title">Could not onboard this merchant:</p>
          <ul>
            {errors.map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        </div>
      )}

      <div className="form-grid">
        <label className="field">
          <span className="field__label">Business name</span>
          <input
            name="businessName"
            value={form.businessName}
            onChange={handleChange}
            placeholder="Acme Payments Ltd"
            maxLength={200}
            disabled={isSubmitting}
          />
        </label>

        <label className="field">
          <span className="field__label">Email</span>
          <input
            name="email"
            /* type="text", not "email": the API is the single source of truth
               on what a valid address is, and the browser's own bubble would
               otherwise pre-empt the server's message. */
            type="text"
            value={form.email}
            onChange={handleChange}
            placeholder="ops@acmepayments.com"
            maxLength={256}
            disabled={isSubmitting}
          />
        </label>

        <label className="field field--narrow">
          <span className="field__label">Country</span>
          <input
            name="country"
            value={form.country}
            onChange={handleChange}
            placeholder="DE"
            maxLength={2}
            autoComplete="off"
            disabled={isSubmitting}
          />
          <span className="field__hint">Two-letter code</span>
        </label>

        <label className="field field--full">
          <span className="field__label">
            Description <span className="field__optional">(optional)</span>
          </span>
          <textarea
            name="description"
            value={form.description}
            onChange={handleChange}
            placeholder="What does this business do?"
            rows={3}
            maxLength={2000}
            disabled={isSubmitting}
          />
        </label>
      </div>

      <div className="form-actions">
        <button
          type="submit"
          className="button button--primary"
          disabled={isSubmitting}
        >
          {isSubmitting ? 'Onboarding…' : 'Onboard merchant'}
        </button>
      </div>
    </form>
  )
}

export default OnboardMerchantForm
