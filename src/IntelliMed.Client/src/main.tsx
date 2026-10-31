import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router';

import 'bootstrap/dist/css/bootstrap.min.css';
import 'bootstrap-icons/font/bootstrap-icons.css';
import '@fontsource-variable/inter';
import './styles/app.css';
import './styles/polish.css';

import { router } from './router';
import { ToastProvider } from './components/Toast';
import { LogApi } from './api/auth';

window.addEventListener('unhandledrejection', (e) => LogApi.error(e.reason, 'unhandledrejection'));

createRoot(document.getElementById('app')!).render(
  <StrictMode>
    <ToastProvider>
      <RouterProvider router={router} />
    </ToastProvider>
  </StrictMode>,
);
