/**
 * The Google Sign-In button used to re-render (clear + rebuild its iframe via renderButton) on
 * EVERY keystroke in the email/password fields: its init effect depended on `onError`/`onSuccess`,
 * inline callbacks LoginForm recreates on every render, and typing re-renders LoginForm via its own
 * email/password state. That is the "modal giật giật while typing" bug — nothing to do with the
 * landing page's globe/map. Pins that typing no longer touches the Google button at all.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { LoginForm } from '../components/LoginForm';

const mockLoginWithGoogle = vi.fn();
vi.mock('../../../shared/hooks/useAuth', () => ({
  useAuth: () => ({ login: vi.fn(), loginWithGoogle: mockLoginWithGoogle }),
}));

const renderButton = vi.fn();
const initialize = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  (window as any).google = { accounts: { id: { initialize, renderButton } } };
});

const renderForm = () =>
  render(
    <MemoryRouter>
      <LoginForm />
    </MemoryRouter>,
  );

describe('LoginForm — Google button stability while typing', () => {
  it('renders the Google button once on mount', async () => {
    renderForm();
    await waitFor(() => expect(renderButton).toHaveBeenCalledTimes(1));
  });

  it('does not re-render the Google button on every email/password keystroke', async () => {
    renderForm();
    await waitFor(() => expect(renderButton).toHaveBeenCalledTimes(1));

    const email = screen.getByPlaceholderText('you@fpt.edu.vn');
    fireEvent.change(email, { target: { value: 'a' } });
    fireEvent.change(email, { target: { value: 'ab' } });
    fireEvent.change(email, { target: { value: 'abc@fpt.edu.vn' } });

    const password = screen.getByPlaceholderText('••••••••');
    fireEvent.change(password, { target: { value: 'p' } });
    fireEvent.change(password, { target: { value: 'pw' } });

    // Give React a tick in case an effect were (wrongly) scheduled.
    await waitFor(() => expect(email).toHaveValue('abc@fpt.edu.vn'));
    expect(renderButton).toHaveBeenCalledTimes(1);
  });
});
