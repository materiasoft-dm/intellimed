import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { AuthApi } from '@/api/auth';
import { FieldMessage, hasErrors, rules, validate, ValidationSummary } from '@/lib/validation';
import './auth.css';

export default function ForgotPassword() {
  const [email, setEmail] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [tried, setTried] = useState(false);

  const spec = { email: [rules.required('Email'), rules.email('Email')] };
  const errors = tried ? validate({ email }, spec) : {};

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setTried(true);
    if (hasErrors(validate({ email }, spec))) return;
    setSubmitting(true);
    try {
      await AuthApi.forgotPassword(email);
      // the same message whether or not the account exists, so the form can't be used to probe for users
      setSubmitted(true);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="public-auth-container">
      <div className="public-auth-card">
        <div className="text-center mb-4">
          <span className="brand-icon-sm"><i className="bi bi-key" /></span>
          <h4 className="mt-3 mb-1">Forgot Password</h4>
          <p className="text-muted small">Enter your email and we'll send you a link to reset your password.</p>
        </div>

        {submitted ? (
          <>
            <div className="alert alert-success d-flex align-items-center">
              <i className="bi bi-check-circle-fill me-2" />
              <div>If that account exists, a password reset email has been sent.</div>
            </div>
            <Link to="/login" className="btn btn-primary w-100">Back to Sign In</Link>
          </>
        ) : (
          <form onSubmit={submit} noValidate>
            <ValidationSummary errors={errors} className="text-danger small mb-3" />
            <div className="mb-3">
              <label className="form-label" htmlFor="forgotEmail">Email Address</label>
              <input id="forgotEmail" type="email" autoComplete="email" className="form-control" placeholder="name@clinic.com"
                     value={email} onChange={(e) => setEmail(e.target.value)} />
              <FieldMessage messages={errors.email} />
            </div>
            <button type="submit" className="btn btn-primary w-100" disabled={submitting}>
              {submitting ? <><span className="spinner-border spinner-border-sm me-2" /><span>Sending...</span></> : <span>Send Reset Link</span>}
            </button>
            <div className="text-center mt-3">
              <Link to="/login" className="small">Back to Sign In</Link>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
