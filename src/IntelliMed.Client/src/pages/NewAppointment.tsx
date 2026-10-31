import { useSearchParams, useNavigate } from 'react-router';
import { NewAppointmentForm } from '@/components/NewAppointmentForm';
import { usePageTitle } from '@/lib/hooks';

export default function NewAppointment() {
  usePageTitle('New Appointment - IntelliMed');
  const navigate = useNavigate();
  const [params] = useSearchParams();
  // prefill from the calendar (?date&time&duration&practitionerId) or a client record (?clientId)
  const prefill = {
    date: params.get('date'),
    time: params.get('time'),
    duration: Number(params.get('duration')) || null,
    practitionerId: Number(params.get('practitionerId')) || null,
    clientId: Number(params.get('clientId')) || null,
  };

  return (
    <>
      <h4 className="mb-4">New Appointment</h4>
      <div className="row">
        <div className="col-12 col-lg-8">
          <NewAppointmentForm prefill={prefill} onCancel={() => navigate('/appointments')}
                              onSaved={(saved) => navigate('id' in saved ? `/appointments/edit/${saved.id}` : '/appointments')} />
        </div>
      </div>
    </>
  );
}
