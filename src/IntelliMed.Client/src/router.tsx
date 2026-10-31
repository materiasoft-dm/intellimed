import { lazy, Suspense, type ComponentType } from 'react';
import { createBrowserRouter, type RouteObject } from 'react-router';
import { MainLayout, PublicLayout } from './layout/MainLayout';
import { RouteErrorDisplay } from './components/ErrorBoundary';
import { Splash } from './components/Splash';

/**
 * A lazily-loaded page: its code is fetched the first time the route is visited. React.lazy (rather than the
 * route's own `lazy`) so the URL changes the moment a link is clicked, as it did in the Blazor app — a route
 * `lazy` holds the URL back until the chunk arrives, which reads as a dead click on a slow connection.
 */
const page = (load: () => Promise<{ default: ComponentType }>): Pick<RouteObject, 'element'> => {
  const Page = lazy(load);
  return {
    element: (
      <Suspense fallback={<div className="imed-route-progress" role="progressbar" aria-label="Loading page" />}>
        <Page />
      </Suspense>
    ),
  };
};

// The same URLs as the Blazor app, so bookmarks, notification links and the UI tests keep working.
export const routes: RouteObject[] = [
  {
    errorElement: <RouteErrorDisplay />,
    HydrateFallback: Splash,
    children: [
      {
        element: <PublicLayout />,
        children: [
          { path: 'login', ...page(() => import('./pages/Login')) },
          { path: 'forgot-password', ...page(() => import('./pages/ForgotPassword')) },
          { path: 'set-password', ...page(() => import('./pages/SetPassword')) },
        ],
      },
      {
        element: <MainLayout />,
        children: [
          { index: true, ...page(() => import('./pages/Home')) },

          { path: 'clients/search', ...page(() => import('./pages/ClientSearch')) },
          { path: 'clients/add', ...page(() => import('./pages/AddClient')) },
          { path: 'clients/edit/:id', ...page(() => import('./pages/AddClient')) },
          { path: 'clients/:clientId/prescriptions/new', ...page(() => import('./pages/PrescribeNew')) },
          { path: 'clients/:clientId/progress-notes/new', ...page(() => import('./pages/ProgressNoteEditor')) },
          { path: 'clients/:clientId/progress-notes/:noteId', ...page(() => import('./pages/ProgressNoteEditor')) },
          { path: 'address-book', ...page(() => import('./pages/AddressBook')) },
          { path: 'results', ...page(() => import('./pages/Results')) },

          { path: 'appointments', ...page(() => import('./pages/AppointmentList')) },
          { path: 'appointments/new', ...page(() => import('./pages/NewAppointment')) },
          { path: 'appointments/edit/:id', ...page(() => import('./pages/AppointmentEdit')) },
          { path: 'appointments/calendar', ...page(() => import('./pages/AppointmentCalendar')) },
          { path: 'appointments/print', ...page(() => import('./pages/AppointmentPrint')) },
          { path: 'appointments/waiting-room', ...page(() => import('./pages/WaitingRoom')) },

          { path: 'invoices', ...page(() => import('./pages/InvoiceSearch')) },
          { path: 'invoices/new', ...page(() => import('./pages/AddInvoice')) },
          { path: 'invoices/edit/:id', ...page(() => import('./pages/InvoiceDetail')) },
          { path: 'invoices/payments', ...page(() => import('./pages/InvoicePayments')) },
          { path: 'invoices/print/:id', ...page(() => import('./pages/InvoicePrint')) },
          { path: 'invoices/receipt/:receiptId', ...page(() => import('./pages/ReceiptPrint')) },
          { path: 'invoices/split/:id', ...page(() => import('./pages/SplitInvoice')) },
          { path: 'fee-schedules', ...page(() => import('./pages/FeeSchedules')) },
          { path: 'derived-item-configs', ...page(() => import('./pages/DerivedItemConfigs')) },

          { path: 'prescriptions/print/:issueId', ...page(() => import('./pages/PrescriptionPrint')) },

          { path: 'admin', ...page(() => import('./pages/Administration')) },
          { path: 'admin/users', ...page(() => import('./pages/UserManagement')) },
          { path: 'admin/roles', ...page(() => import('./pages/RoleConfiguration')) },
          { path: 'admin/settings', ...page(() => import('./pages/SystemSettings')) },
          { path: 'admin/appointment-types', ...page(() => import('./pages/AppointmentTypeSettings')) },
          { path: 'admin/database-backups', ...page(() => import('./pages/DatabaseBackups')) },
          { path: 'admin/medicines', ...page(() => import('./pages/MedicinesAdmin')) },
          { path: 'admin/drug-interactions', ...page(() => import('./pages/DrugInteractionsAdmin')) },
          { path: 'admin/email-templates', ...page(() => import('./pages/EmailTemplateList')) },
          { path: 'admin/email-templates/edit', ...page(() => import('./pages/EmailTemplateEdit')) },
          { path: 'admin/email-templates/edit/:id', ...page(() => import('./pages/EmailTemplateEdit')) },
          { path: 'admin/search-actions', ...page(() => import('./pages/SearchActionList')) },
          { path: 'admin/search-actions/edit', ...page(() => import('./pages/SearchActionEdit')) },
          { path: 'admin/search-actions/edit/:id', ...page(() => import('./pages/SearchActionEdit')) },
          { path: 'clinic-manager', ...page(() => import('./pages/ClinicManager')) },
          { path: 'clinic-settings', ...page(() => import('./pages/ClinicSettings')) },

          { path: 'profile', ...page(() => import('./pages/ProfileSettings')) },
          { path: 'help', ...page(() => import('./pages/Help')) },
          { path: 'about', ...page(() => import('./pages/About')) },
          { path: '*', ...page(() => import('./pages/NotFound')) },
        ],
      },
    ],
  },
];

export const router = createBrowserRouter(routes);
