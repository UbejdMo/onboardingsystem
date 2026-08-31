import './StatusBadge.css'

// Maps to the API's MerchantStatus values. Unknown values still render, so a
// status added to the backend shows up as plain text rather than vanishing.
const STATUS_CLASSES = {
  Pending: 'badge--pending',
  Approved: 'badge--approved',
  Rejected: 'badge--rejected',
  Flagged: 'badge--flagged',
}

function StatusBadge({ status }) {
  const modifier = STATUS_CLASSES[status] ?? 'badge--unknown'

  return <span className={`badge ${modifier}`}>{status}</span>
}

export default StatusBadge
