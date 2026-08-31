import StatusBadge from './StatusBadge'
import './MerchantTable.css'

/**
 * A null score means "not screened yet", which is different from a score of
 * zero - so it must not render as 0.
 */
function RiskScore({ score }) {
  if (score === null || score === undefined) {
    return <span className="risk-score risk-score--none">Not screened</span>
  }

  // Matches HighRiskScoreThreshold in the API.
  const isHigh = score >= 70

  return (
    <span className={`risk-score ${isHigh ? 'risk-score--high' : ''}`}>
      {score}
    </span>
  )
}

function MerchantTable({ merchants }) {
  return (
    <div className="table-wrapper">
      <table className="merchant-table">
        <thead>
          <tr>
            <th>Business name</th>
            <th>Country</th>
            <th>Status</th>
            <th className="numeric">Risk score</th>
          </tr>
        </thead>
        <tbody>
          {merchants.map((merchant) => (
            <tr key={merchant.id}>
              <td>
                <span className="business-name">{merchant.businessName}</span>
                <span className="email">{merchant.email}</span>
              </td>
              <td>{merchant.country}</td>
              <td>
                <StatusBadge status={merchant.status} />
              </td>
              <td className="numeric">
                <RiskScore score={merchant.riskScore} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export default MerchantTable
