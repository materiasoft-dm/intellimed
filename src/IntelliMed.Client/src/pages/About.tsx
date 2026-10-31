import { Link } from 'react-router';
import { AppVersion } from '@/components/AppVersion';
import { usePageTitle } from '@/lib/hooks';

// The sidebar has always linked "About IntelliMed" to /about, but the Blazor app had no such page (it fell
// through to Not Found). This is that page: what the product is, its version, and the data-source notices.
export default function About() {
  usePageTitle('About IntelliMed');
  return (
    <>
      <h4 className="mb-1">About IntelliMed</h4>
      <p className="text-muted small mb-4">Practice management for Australian medical practices.</p>

      <div className="row g-3">
        <div className="col-lg-6">
          <div className="card h-100">
            <div className="card-body d-flex gap-3">
              <span className="mdi-logo-mark flex-shrink-0" style={{ width: 48, height: 48, fontSize: '1.1rem' }}>IM</span>
              <div>
                <h6 className="mb-1">IntelliMed</h6>
                <div className="mb-2"><AppVersion /></div>
                <p className="small text-muted mb-2">
                  Clients and records, appointments, billing and Medicare claiming, prescribing, results and practice
                  administration in one place.
                </p>
                <Link to="/help" className="small">Read the User Guide <i className="bi bi-arrow-right" /></Link>
              </div>
            </div>
          </div>
        </div>
        <div className="col-lg-6">
          <div className="card h-100">
            <div className="card-body">
              <h6 className="mb-2">Medicine data sources</h6>
              <ul className="small text-muted mb-0 ps-3">
                <li className="mb-1">
                  Pharmaceutical Benefits Scheme data — © Commonwealth of Australia. Used and redistributed with permission;
                  the content is reproduced unmodified.
                </li>
                <li className="mb-1">
                  TGA ARTG scheduling data — © Commonwealth of Australia, on the same terms.
                </li>
                <li>Ingredient enrichment from Wikidata (CC0).</li>
              </ul>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
