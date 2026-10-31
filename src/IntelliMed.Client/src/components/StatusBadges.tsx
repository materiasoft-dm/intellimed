import { AppointmentStatus, InvoiceStatus } from '@/api/types.gen';

/** Badge colours for appointment status — the same everywhere in the app (see the User Guide legend). */
export function appointmentStatusClass(status: AppointmentStatus): string {
  switch (status) {
    case AppointmentStatus.Scheduled: return 'bg-secondary';
    case AppointmentStatus.Confirmed: return 'bg-info text-dark';
    case AppointmentStatus.Arrived: return 'bg-warning text-dark';
    case AppointmentStatus.InProgress: return 'bg-primary';
    case AppointmentStatus.Completed: return 'bg-success';
    case AppointmentStatus.Cancelled: return 'bg-dark';
    case AppointmentStatus.NoShow: return 'bg-danger';
    default: return 'bg-secondary';
  }
}

export function AppointmentStatusBadge({ status, className = '' }: { status: AppointmentStatus; className?: string }) {
  return <span className={`badge ${appointmentStatusClass(status)} ${className}`}>{AppointmentStatus[status]}</span>;
}

export function invoiceStatusClass(status: InvoiceStatus): string {
  switch (status) {
    case InvoiceStatus.Paid: return 'bg-success';
    case InvoiceStatus.PartiallyPaid: return 'bg-warning text-dark';
    case InvoiceStatus.Cancelled: return 'bg-dark';
    case InvoiceStatus.Draft: return 'bg-secondary';
    default: return 'bg-primary';
  }
}
