import { useNavigate, useParams } from 'react-router';
import { AppointmentDetailsPanel } from '@/components/AppointmentDetails';
import { usePageTitle } from '@/lib/hooks';

export default function AppointmentEdit() {
  usePageTitle('Appointment - IntelliMed');
  const { id } = useParams();
  const navigate = useNavigate();
  return (
    <>
      <h5 className="mb-3">Appointment Details</h5>
      <AppointmentDetailsPanel appointmentId={Number(id)} onClosed={() => navigate('/appointments')} />
      <button type="button" className="btn btn-secondary" onClick={() => navigate('/appointments')}>Back to List</button>
    </>
  );
}
