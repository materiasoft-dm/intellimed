import { useState, type FormEvent } from 'react';
import { Link, Navigate, useNavigate } from 'react-router';
import { AuthApi } from '@/api/auth';
import { AppVersion } from '@/components/AppVersion';
import { FieldMessage, hasErrors, rules, validate, ValidationSummary } from '@/lib/validation';
import './auth.css';

interface LoginModel extends Record<string, unknown> {
  email: string;
  password: string;
  rememberMe: boolean;
}

export default function Login() {
  const navigate = useNavigate();
  const [model, setModel] = useState<LoginModel>(() => ({
    // pre-filled for convenience in a development build only, as the Blazor DEBUG build did
    email: import.meta.env.DEV ? 'admin@clinic.com' : '',
    password: import.meta.env.DEV ? 'IntelliMed2024!' : '',
    rememberMe: false,
  }));
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  if (AuthApi.isAuthenticated()) return <Navigate to="/" replace />;

  const errors = submitted
    ? validate(model, {
      email: [rules.required('Email'), rules.email('Email')],
      password: [rules.required('Password')],
    })
    : {};

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitted(true);
    const problems = validate(model, { email: [rules.required('Email'), rules.email('Email')], password: [rules.required('Password')] });
    if (hasErrors(problems)) return;

    setLoading(true);
    setError(null);
    try {
      const response = await AuthApi.login(model.email, model.password);
      if (response.success) navigate('/');
      else setError(response.errorMessage ?? 'Invalid email or password. Please try again.');
    } catch {
      setError('An error occurred. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const set = <K extends keyof LoginModel>(key: K, value: LoginModel[K]) => setModel((m) => ({ ...m, [key]: value }));

  return (
    <div className="login-container">
      <div className="login-split">
        <div className="login-image-section" aria-hidden="true">
          <div className="login-image-overlay" />
          <img src="https://images.unsplash.com/photo-1519494026892-80bbd2d6fd0d?w=1200&q=80" alt="" className="login-image" />
          <div className="login-brand-overlay">
            <span className="login-brand-mark">IM</span>
            <h1 className="brand-title">IntelliMed</h1>
            <p className="brand-subtitle">Practice Management System</p>
          </div>
          <div className="login-quote">
            <p>"Streamline your practice with intelligent, integrated healthcare management."</p>
          </div>
        </div>

        <div className="login-form-section">
          <div className="login-form-wrapper">
            <div className="login-form-header">
              <span className="login-mobile-mark" aria-hidden="true">IM</span>
              <h4 className="welcome-text">Welcome Back</h4>
              <p className="subtitle-text">Sign in to access your practice dashboard</p>
            </div>

            <form className="login-form" onSubmit={submit} noValidate>
              <ValidationSummary errors={errors} />

              <div className="mb-3">
                <label htmlFor="email" className="form-label">Email Address</label>
                <input id="email" type="email" autoComplete="username" className={`form-control login-field ${errors.email ? 'invalid' : ''}`}
                       placeholder="name@clinic.com" value={model.email} onChange={(e) => set('email', e.target.value)} />
                <FieldMessage messages={errors.email} />
              </div>

              <div className="mb-3">
                <label htmlFor="password" className="form-label">Password</label>
                <div className="input-group">
                  <input id="password" type={showPassword ? 'text' : 'password'} autoComplete="current-password"
                         className={`form-control login-field ${errors.password ? 'invalid' : ''}`} placeholder="Enter your password"
                         value={model.password} onChange={(e) => set('password', e.target.value)} />
                  <button type="button" className="btn btn-outline-secondary" tabIndex={-1}
                          aria-label={showPassword ? 'Hide password' : 'Show password'} onClick={() => setShowPassword((s) => !s)}>
                    <i className={`bi ${showPassword ? 'bi-eye-slash' : 'bi-eye'}`} />
                  </button>
                </div>
                <FieldMessage messages={errors.password} />
              </div>

              <div className="form-options">
                <div className="form-check">
                  <input id="rememberMe" type="checkbox" className="form-check-input" checked={model.rememberMe}
                         onChange={(e) => set('rememberMe', e.target.checked)} />
                  <label className="form-check-label" htmlFor="rememberMe">Remember me</label>
                </div>
                <Link to="/forgot-password" className="forgot-link">Forgot password?</Link>
              </div>

              <button type="submit" className="btn btn-primary btn-lg w-100 login-button" disabled={loading}>
                {loading
                  ? <><span className="spinner-border spinner-border-sm me-2" role="status" /><span>Signing in...</span></>
                  : <span>Sign In</span>}
              </button>
            </form>

            {error && (
              <div className="alert alert-danger login-error-alert animate-shake mt-3 d-flex align-items-center" role="alert">
                <i className="bi bi-exclamation-triangle me-2" />
                <div>{error}</div>
              </div>
            )}

            <hr className="my-4" />

            <div className="login-footer">
              {/* A plain access notice rather than compliance badges — make a privacy claim here only once a
                  real assessment (e.g. against the APPs) backs it. */}
              <p className="footer-text">
                <i className="bi bi-shield-lock me-1" />
                Authorised users only. Patient information is confidential.
              </p>
              <div className="mt-2"><AppVersion /></div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
