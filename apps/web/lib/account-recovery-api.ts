'use client';

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:8080';

async function request<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
  });
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error((payload as { error?: string }).error ?? 'Request failed.');
  return payload as T;
}

export function requestPasswordReset(email: string) { return request<{ accepted: boolean; actionUrl?: string }>('/api/account/password-reset/request', { email }); }
export function confirmPasswordReset(token: string, newPassword: string) { return request<{ reset: boolean }>('/api/account/password-reset/confirm', { token, newPassword }); }
export function confirmEmail(token: string) { return request<{ verified: boolean }>('/api/account/email-verification/confirm', { token }); }
