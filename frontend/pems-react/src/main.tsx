import {StrictMode} from 'react';
import {createRoot} from 'react-dom/client';
import {BrowserRouter} from 'react-router-dom';
import { AuthProvider } from './shared/auth/AuthContext';
import { ErrorBoundary } from './components/ErrorBoundary';
import App from './App.tsx';
import './index.css';
import './shared/i18n/config';

// Intercept and silence Recharts initial render dimension warnings
const originalWarn = console.warn;
console.warn = (...args: any[]) => {
  if (
    args[0] &&
    typeof args[0] === 'string' &&
    args[0].includes('The width') &&
    args[0].includes('of chart should be greater than 0')
  ) {
    return;
  }
  originalWarn(...args);
};

const originalError = console.error;
console.error = (...args: any[]) => {
  if (
    args[0] &&
    typeof args[0] === 'string' &&
    args[0].includes('The width') &&
    args[0].includes('of chart should be greater than 0')
  ) {
    return;
  }
  originalError(...args);
};

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary>
      <BrowserRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </BrowserRouter>
    </ErrorBoundary>
  </StrictMode>,
);
