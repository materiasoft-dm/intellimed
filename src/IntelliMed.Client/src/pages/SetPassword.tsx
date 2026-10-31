import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { AuthApi } from '@/api/auth';
import { FieldMessage, hasErrors, rules, validate, ValidationSummary } from '@/lib/validation';
import './auth.css';

/** Landing page for the invite / reset-password email link: ?userId=…&token=…(&pwd=… prefilled). */
export default function SetPassword() {
  const [params] = useSearchParams();
  const userId = params.get('userId');
  const token = params.get('token');
  const prefilled = params.get('pwd') ?? '';

  const [model, setModel] = useState({ newPassword: prefilled, confirmPassword: prefilled });
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [completed, setCompleted] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [tried, setTried] = useState(false);

  const check = () => {
    const errors = validate(model, {
      newPassword: [rules.required('NewPassword'), rules.minLength(8, 'Password must be at least 8 characters.')],
      confirmPassword: [rules.required('ConfirmPassword')],
    });
    if (model.newPassword !== model.confirmPassword) {
      errors.confirmPassword = [...(errors.confirmPassword ?? []), 'Passwords do not match.'];
    }
    return errors;
  };
  const errors = tried ? check() : {};

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setTried(true);
    if (hasErrors(check())) return;
    setSubmitting(true);
    setError(null);
    try {
      const result = await AuthApi.setPassword(userId!, token!, model.newPassword);
      if (result.success) setCompleted(true);
      else setError(result.message ?? 'Failed to set password. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="public-auth-container">
      <div className="public-auth-card">
        <div className="text-center mb-4">
          <span className="brand-icon-sm"><i className="bi bi-shield-lock" /></span>
          <h4 className="mt-3 mb-1">Set Your Password</h4>
          <p className="text-muted small">Choose a password to finish setting up your account.</p>
        </div>

        {!userId || !token ? (
          <>
            <div className="alert alert-danger">
              <i className="bi bi-exclamation-triangle me-1" /> This link is invalid. Please use the link from your invite/reset email, or request a new one.
            </div>
            <Link to="/forgot-password" className="btn btn-outline-primary w-100">Request a New Link</Link>
          </>
        ) : completed ? (
          <>
            <div className="alert alert-success d-flex align-items-center">
              <i className="bi bi-check-circle-fill me-2" />
              <div>Your password has been set. You can now log in.</div>
            </div>
            <Link to="/login" className="btn btn-primary w-100">Go to Sign In</Link>
          </>
        ) : (
          <form onSubmit={submit} noValidate>
            <ValidationSummary errors={errors} className="text-danger small mb-3" />
            {error && <div className="alert alert-danger">{error}</div>}
            <div className="mb-3">
              <label className="form-label" htmlFor="newPassword">New Password</label>
              <div className="input-group">
                <input id="newPassword" type={showPassword ? 'text' : 'password'} autoComplete="new-password" className="form-control"
                       value={model.newPassword} onChange={(e) => setModel((m) => ({ ...m, newPassword: e.target.value }))} />
                <button type="button" className="btn btn-outline-secondary" tabIndex={-1} aria-label={showPassword ? 'Hide password' : 'Show password'}
                        onClick={() => setShowPassword((s) => !s)}>
                  <i className={`bi ${showPassword ? 'bi-eye-slash' : 'bi-eye'}`} />
                </button>
              </div>
              <div className="form-text">Min 8 chars: upper, lower, digit, special</div>
              <FieldMessage messages={errors.newPassword} />
            </div>
            <div className="mb-3">
              <label className="form-label" htmlFor="confirmPassword">Confirm Password</label>
              <input id="confirmPassword" type={showPassword ? 'text' : 'password'} autoComplete="new-password" className="form-control"
                     value={model.confirmPassword} onChange={(e) => setModel((m) => ({ ...m, confirmPassword: e.target.value }))} />
              <FieldMessage messages={errors.confirmPassword} />
            </div>
            <button type="submit" className="btn btn-primary w-100" disabled={submitting}>
              {submitting ? <><span className="spinner-border spinner-border-sm me-2" /><span>Saving...</span></> : <span>Set Password</span>}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}
