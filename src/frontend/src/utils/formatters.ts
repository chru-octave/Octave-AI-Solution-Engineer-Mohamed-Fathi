import type { SubmissionStatus } from '../types';

export function statusLabel(status: SubmissionStatus): string {
  switch (status) {
    case 'Processed': return 'Processed';
    case 'NeedsReview': return 'Needs Review';
    case 'Failed': return 'Failed';
    case 'Pending': return 'Pending';
    default: return status;
  }
}

export function statusColor(status: SubmissionStatus): string {
  switch (status) {
    case 'Processed': return 'success';
    case 'NeedsReview': return 'warning';
    case 'Failed': return 'error';
    case 'Pending': return 'default';
    default: return 'default';
  }
}

export function confidenceColor(confidence: number): string {
  if (confidence >= 0.8) return '#2e7d32';
  if (confidence >= 0.7) return '#f57c00';
  return '#c62828';
}

export function formatCurrency(value: number | null | undefined): string {
  if (value == null) return '—';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(value);
}

export function formatDate(dateStr: string | null | undefined): string {
  if (!dateStr) return '—';
  return new Date(dateStr).toLocaleDateString();
}
