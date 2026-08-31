import { useCallback, useEffect, useState } from 'react'
import { fetchMerchants } from './api/merchants'
import MerchantTable from './components/MerchantTable'
import './App.css'

function App() {
  const [merchants, setMerchants] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState(null)

  const loadMerchants = useCallback(async (signal) => {
    setIsLoading(true)
    setError(null)

    try {
      const data = await fetchMerchants(signal)
      setMerchants(data)
    } catch (err) {
      // An aborted request is a cancelled render, not a failure to report.
      if (err.name === 'AbortError') return
      setError(err.message)
    } finally {
      if (!signal?.aborted) setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    // Abort on unmount so a slow response cannot set state after the
    // component has gone - React's StrictMode mounts twice in development.
    const controller = new AbortController()
    loadMerchants(controller.signal)
    return () => controller.abort()
  }, [loadMerchants])

  return (
    <div className="app">
      <header className="app-header">
        <div>
          <h1>Merchant Onboarding</h1>
          <p className="subtitle">Risk screening &amp; compliance review</p>
        </div>
        <button
          type="button"
          className="button button--secondary"
          onClick={() => loadMerchants()}
          disabled={isLoading}
        >
          {isLoading ? 'Refreshing…' : 'Refresh'}
        </button>
      </header>

      <main className="app-main">
        <section>
          <div className="section-header">
            <h2>Merchants</h2>
            {!isLoading && !error && (
              <span className="count">
                {merchants.length}
                {merchants.length === 1 ? ' merchant' : ' merchants'}
              </span>
            )}
          </div>

          {isLoading && <p className="state-message">Loading merchants…</p>}

          {error && !isLoading && (
            <div className="state-message state-message--error" role="alert">
              <p>{error}</p>
              <button
                type="button"
                className="button button--secondary"
                onClick={() => loadMerchants()}
              >
                Try again
              </button>
            </div>
          )}

          {!isLoading && !error && merchants.length === 0 && (
            <p className="state-message">
              No merchants yet. Onboard one to get started.
            </p>
          )}

          {!isLoading && !error && merchants.length > 0 && (
            <MerchantTable merchants={merchants} />
          )}
        </section>
      </main>
    </div>
  )
}

export default App
