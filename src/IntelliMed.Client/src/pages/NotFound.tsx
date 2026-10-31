import { Link } from 'react-router';
import { usePageTitle } from '@/lib/hooks';

export default function NotFound() {
  usePageTitle('Not Found - IntelliMed');
  return (
    <div className="imed-empty-state py-5">
      <i className="bi bi-signpost-2" aria-hidden="true" />
      <h3>Not Found</h3>
      <p className="text-muted">Sorry, the content you are looking for does not exist.</p>
      <Link to="/" className="btn btn-primary btn-sm"><i className="bi bi-house me-1" />Back to the dashboard</Link>
    </div>
  );
}
