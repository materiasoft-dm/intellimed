import { useCallback, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import FullCalendar from '@fullcalendar/react';
import type { DatesSetArg, EventClickArg, EventContentArg, EventDropArg, EventInput, EventMountArg } from '@fullcalendar/core';
import dayGridPlugin from '@fullcalendar/daygrid';
import timeGridPlugin from '@fullcalendar/timegrid';
import interactionPlugin, { type DateClickArg } from '@fullcalendar/interaction';
import type { DateSelectArg } from '@fullcalendar/core';
import { AppointmentApi, PractitionerApi } from '@/api/appointments';
import { AuthApi } from '@/api/auth';
import { ClinicSettingsApi } from '@/api/settings';
import { AppointmentStatus, type AppointmentDto, type PractitionerDto } from '@/api/types.gen';
import { AppointmentDetailsModal } from '@/components/AppointmentDetails';
import { ContextMenu, type ContextMenuItem } from '@/components/ContextMenu';
import { Modal } from '@/components/Modal';
import { NewAppointmentForm, type AppointmentPrefill } from '@/components/NewAppointmentForm';
import { LoadingState } from '@/components/States';
import { addDays, dateParam, formatDate, minutesToSpan, toDate, toIsoLocal } from '@/lib/dates';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import './AppointmentCalendar.css';

// (background, text) pairs chosen for contrast — the status colours double as an at-a-glance legend
const STATUS_COLORS: Record<AppointmentStatus, [string, string]> = {
  [AppointmentStatus.Scheduled]: ['#8b95a5', '#fff'],
  [AppointmentStatus.Confirmed]: ['#3aa8c1', '#fff'],
  [AppointmentStatus.Arrived]: ['#f5b400', '#1a1a1a'],
  [AppointmentStatus.InProgress]: ['#4272d7', '#fff'],
  [AppointmentStatus.Completed]: ['#0f9d6e', '#fff'],
  [AppointmentStatus.Cancelled]: ['#4b5563', '#e5e7eb'],
  [AppointmentStatus.NoShow]: ['#c2434f', '#fff'],
};

const DAY_START = '07:00:00';
const DAY_END = '19:00:00';

function startOf(a: AppointmentDto): Date {
  const d = toDate(a.appointmentDate)!;
  const [h, m] = a.startTime.split(':').map(Number);
  return new Date(d.getFullYear(), d.getMonth(), d.getDate(), h, m);
}
function endOf(a: AppointmentDto): Date {
  const d = toDate(a.appointmentDate)!;
  const [h, m] = a.endTime.split(':').map(Number);
  return new Date(d.getFullYear(), d.getMonth(), d.getDate(), h, m);
}

export default function AppointmentCalendar() {
  usePageTitle('Appointment Calendar - IntelliMed');
  const navigate = useNavigate();
  const calendar = useRef<FullCalendar>(null);
  const [practitioners, setPractitioners] = useState<PractitionerDto[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [appointments, setAppointments] = useState<AppointmentDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [slotMinutes, setSlotMinutes] = useState(15);
  const [range, setRange] = useState<{ start: Date; end: Date } | null>(null);
  const [jumpDate, setJumpDate] = useState(dateParam(new Date()));
  const [selectedAppointment, setSelectedAppointment] = useState<number | null>(null);
  const [status, setStatus] = useState<{ message: string; error: boolean } | null>(null);
  // the right-click "Add Appointment" dialog, prefilled with the slot
  const [booking, setBooking] = useState<AppointmentPrefill | null>(null);
  const [menu, setMenu] = useState<{ x: number; y: number; items: ContextMenuItem[] } | null>(null);
  // a click or drag on empty slots, remembered until the user right-clicks to add an appointment there
  const pendingSlot = useRef<{ start: Date; end: Date } | null>(null);
  const loadSeq = useRef(0);

  useMountEffect(async (alive) => {
    const [p, profile, clinicSettings] = await Promise.all([PractitionerApi.active(), AuthApi.getProfile(), ClinicSettingsApi.get()]);
    if (!alive()) return;
    setPractitioners(p);
    setSelectedIds(new Set(p.map((x) => x.id)));
    setSlotMinutes(profile?.defaultAppointmentTimeslotMinutes ?? clinicSettings?.minimumTimeslotMinutes ?? 15);
  });

  const loadRange = useCallback(async (start: Date, end: Date) => {
    const mine = ++loadSeq.current;
    setLoading(true);
    try {
      const result = await AppointmentApi.search({ fromDate: toIsoLocal(start), toDate: toIsoLocal(addDays(end, -1)), pageSize: 500 });
      if (mine === loadSeq.current) setAppointments(result.items);
    } finally {
      if (mine === loadSeq.current) setLoading(false);
    }
  }, []);

  const onDatesSet = (arg: DatesSetArg) => {
    setRange({ start: arg.start, end: arg.end });
    loadRange(arg.start, arg.end);
  };
  const reload = () => { if (range) loadRange(range.start, range.end); };

  // Derived, so it is right whichever of appointments / practitioners arrives first (that ordering once
  // left the Blazor calendar empty until a reload).
  const events = useMemo<EventInput[]>(() => appointments
    .filter((a) => selectedIds.has(a.practitionerId))
    .map((a) => {
      const [bg, fg] = STATUS_COLORS[a.status] ?? STATUS_COLORS[AppointmentStatus.Scheduled];
      return {
        id: String(a.id),
        start: startOf(a),
        end: endOf(a),
        title: `${a.startTimeFormatted} ${a.clientName} (${a.practitionerName})`,
        backgroundColor: bg,
        borderColor: bg,
        textColor: fg,
        extendedProps: { appointment: a },
      };
    }), [appointments, selectedIds]);

  const togglePractitioner = (id: number, on: boolean) => {
    setSelectedIds((s) => {
      const next = new Set(s);
      if (on) next.add(id); else next.delete(id);
      return next;
    });
  };

  const newAppointmentAt = (start: Date, end: Date) => {
    const practitionerId = selectedIds.size === 1 ? [...selectedIds][0] : null;
    let duration = Math.round((end.getTime() - start.getTime()) / 60000);
    if (duration <= 0) duration = slotMinutes;
    // booked in a dialog over the calendar, so the user stays on the week they were looking at
    setBooking({ date: dateParam(start), time: formatDate(start, 'HH:mm'), duration, practitionerId });
  };

  const onEventDrop = async (arg: EventDropArg) => {
    const a = arg.event.extendedProps.appointment as AppointmentDto;
    // snap to the dropped slot and keep the original duration
    const duration = endOf(a).getTime() - startOf(a).getTime();
    const start = arg.event.start!;
    const end = new Date(start.getTime() + duration);
    const { ok, error } = await AppointmentApi.reschedule(a.id, {
      appointmentDate: toIsoLocal(new Date(start.getFullYear(), start.getMonth(), start.getDate())),
      startTime: minutesToSpan(start.getHours() * 60 + start.getMinutes()),
      endTime: minutesToSpan(end.getHours() * 60 + end.getMinutes()),
    });
    if (ok) {
      setStatus({ message: 'Appointment rescheduled.', error: false });
      reload();
    } else {
      arg.revert();
      setStatus({ message: error ?? 'Failed to reschedule.', error: true });
    }
  };

  // Right-click: on an appointment → view/edit it; on an empty slot → add one there (or across the
  // range the user dragged out first).
  const onContextMenu = (e: React.MouseEvent) => {
    const target = e.target as HTMLElement;
    const apptEl = target.closest<HTMLElement>('[data-appt-id]');
    if (apptEl) {
      e.preventDefault();
      const id = Number(apptEl.dataset.apptId);
      setMenu({ x: e.clientX, y: e.clientY, items: [{ label: 'View / Edit Appointment', icon: 'bi-pencil', onSelect: () => setSelectedAppointment(id) }] });
      return;
    }
    const slot = slotAt(e.clientX, e.clientY);
    if (!slot) return;
    e.preventDefault();
    let { start, end } = slot;
    const p = pendingSlot.current;
    if (p && start >= p.start && start < p.end) ({ start, end } = p);
    setMenu({ x: e.clientX, y: e.clientY, items: [{ label: 'Add Appointment', icon: 'bi-plus-lg', onSelect: () => newAppointmentAt(start, end) }] });
  };

  // the day column and time lane under the pointer
  const slotAt = (x: number, y: number): { start: Date; end: Date } | null => {
    const stack = document.elementsFromPoint(x, y) as HTMLElement[];
    const lane = stack.find((el) => el.matches?.('.fc-timegrid-slot-lane[data-time]'));
    const col = stack.find((el) => el.matches?.('.fc-timegrid-col[data-date], .fc-daygrid-day[data-date]'));
    if (!col) return null;
    const day = toDate(col.dataset.date!)!;
    if (!lane) return { start: day, end: addDays(day, 1) };
    const [h, m] = lane.dataset.time!.split(':').map(Number);
    const start = new Date(day.getFullYear(), day.getMonth(), day.getDate(), h, m);
    return { start, end: new Date(start.getTime() + slotMinutes * 60000) };
  };

  return (
    <>
      <div className="imed-calendar-header">
        <h4><i className="bi bi-calendar3" /> Appointment Calendar</h4>
        <div className="d-flex flex-wrap gap-2 align-items-center">
          <input type="date" className="form-control form-control-sm" style={{ width: 'auto' }} title="Jump to date" aria-label="Jump to date"
                 value={jumpDate} onChange={(e) => {
                   setJumpDate(e.target.value);
                   if (e.target.value) calendar.current?.getApi().gotoDate(toDate(e.target.value)!);
                 }} />
          <button className="btn btn-sm btn-outline-secondary" onClick={() => navigate(`/appointments/print?date=${dateParam(range?.start ?? new Date())}`)}>
            <i className="bi bi-printer" /> Print Day List
          </button>
          <button className="btn btn-sm btn-outline-secondary" onClick={() => navigate('/appointments')}><i className="bi bi-list-ul" /> List View</button>
          <button className="btn btn-sm btn-primary" onClick={() => navigate('/appointments/new')}><i className="bi bi-plus-lg" /> New Appointment</button>
        </div>
      </div>

      <div className="card imed-practitioner-bar mb-3">
        <div className="card-body py-2 d-flex flex-wrap align-items-center">
          <span className="small text-muted me-2"><i className="bi bi-people-fill me-1" />Practitioners:</span>
          {practitioners.map((p) => (
            <div key={p.id} className="form-check form-check-inline">
              <input className="form-check-input" type="checkbox" id={`prac${p.id}`} checked={selectedIds.has(p.id)}
                     onChange={(e) => togglePractitioner(p.id, e.target.checked)} />
              <label className="form-check-label small" htmlFor={`prac${p.id}`}>{p.fullName}</label>
            </div>
          ))}
          <span className="small text-muted ms-auto d-none d-xl-inline">
            <i className="bi bi-mouse2 me-1" />Right-click a time to add an appointment · drag an appointment to move it
          </span>
        </div>
      </div>

      <div className="card imed-calendar-card">
        <div className="card-body p-0 position-relative" onContextMenu={onContextMenu}>
          {loading && <div className="imed-calendar-loading"><LoadingState text="Loading appointments..." /></div>}
          <FullCalendar
            ref={calendar}
            plugins={[timeGridPlugin, dayGridPlugin, interactionPlugin]}
            initialView="timeGridWeek"
            headerToolbar={{ left: 'prev,next today', center: 'title', right: 'timeGridDay,timeGridWeek,dayGridMonth' }}
            buttonText={{ today: 'Today', day: 'Day', week: 'Week', month: 'Month' }}
            height="calc(100vh - 280px)"
            firstDay={1}
            slotMinTime={DAY_START}
            slotMaxTime={DAY_END}
            slotDuration={minutesToSpan(slotMinutes)}
            slotLabelInterval="01:00"
            scrollTime="08:00:00"
            nowIndicator
            allDaySlot={false}
            dayHeaderFormat={{ weekday: 'short', month: 'short', day: 'numeric', omitCommas: true }}
            eventTimeFormat={{ hour: 'numeric', minute: '2-digit', meridiem: 'short' }}
            events={events}
            editable
            eventDurationEditable={false}
            selectable
            selectMirror
            unselectAuto={false}
            datesSet={onDatesSet}
            select={(arg: DateSelectArg) => { pendingSlot.current = { start: arg.start, end: arg.end }; }}
            dateClick={(arg: DateClickArg) => {
              pendingSlot.current = { start: arg.date, end: new Date(arg.date.getTime() + slotMinutes * 60000) };
            }}
            eventClick={(arg: EventClickArg) => setSelectedAppointment(Number(arg.event.id))}
            eventDrop={onEventDrop}
            eventDidMount={(arg: EventMountArg) => {
              arg.el.dataset.apptId = arg.event.id;
              const a = arg.event.extendedProps.appointment as AppointmentDto;
              arg.el.title = `${a.timeRangeFormatted} · ${a.clientName} · ${a.practitionerName} · ${a.statusName}`;
            }}
            eventContent={(arg: EventContentArg) => (
              <div className="imed-cal-event">
                <span className="imed-cal-event-text">{arg.event.title}</span>
              </div>
            )}
          />
        </div>
      </div>

      {status && <div className={`alert ${status.error ? 'alert-danger' : 'alert-success'} mt-2 py-2 small`}>{status.message}</div>}

      <div className="imed-calendar-legend small text-muted mt-2">
        {Object.entries(STATUS_COLORS).map(([s, [bg]]) => (
          <span key={s} className="me-3 text-nowrap"><span className="imed-color-swatch me-1" style={{ background: bg }} />{AppointmentStatus[Number(s)]}</span>
        ))}
      </div>

      {booking && (
        <Modal title="New Appointment" size="lg" onClose={() => setBooking(null)}>
          <NewAppointmentForm prefill={booking} onCancel={() => setBooking(null)}
                              onSaved={(saved) => {
                                setBooking(null);
                                setStatus({ message: 'seriesIds' in saved ? `${saved.seriesIds.length} appointments booked.` : 'Appointment booked.', error: false });
                                reload();
                              }} />
        </Modal>
      )}
      <AppointmentDetailsModal appointmentId={selectedAppointment} onClose={() => setSelectedAppointment(null)} onChanged={reload} />
      {menu && <ContextMenu x={menu.x} y={menu.y} items={menu.items} onClose={() => setMenu(null)} />}
    </>
  );
}
