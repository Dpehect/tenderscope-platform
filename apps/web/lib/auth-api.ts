'use client';

export type AuthUser = {
  id: string;
  email: string;
  displayName: string;
  organizationId: string;
  organizationName: string;
  role: string;
};

export type AuthSession = { accessToken: string; expiresAt: string; user: AuthUser };
export type UserOrganization = { id: string; name: string; slug: string; role: string; isCurrent: boolean };
export type RegisterInput = { email: string; password: string; displayName: string; organizationName?: string };
export type LoginInput = { email: string; password: string };

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:8080';
const ACCESS_TOKEN_KEY = 'tenderscope:access-token';
const SESSION_KEY = 'tenderscope:session';
let refreshPromise: Promise<AuthSession | null> | null = null;

export function getAccessToken() { return typeof window === 'undefined' ? null : localStorage.getItem(ACCESS_TOKEN_KEY); }
export function getStoredSession(): AuthSession | null {
  if (typeof window === 'undefined') return null;
  try { const raw = localStorage.getItem(SESSION_KEY); return raw ? JSON.parse(raw) as AuthSession : null; } catch { return null; }
}
export function storeSession(session: AuthSession | null) {
  if (typeof window === 'undefined') return;
  if (!session) { localStorage.removeItem(ACCESS_TOKEN_KEY); localStorage.removeItem(SESSION_KEY); }
  else { localStorage.setItem(ACCESS_TOKEN_KEY, session.accessToken); localStorage.setItem(SESSION_KEY, JSON.stringify(session)); }
  window.dispatchEvent(new Event('tenderscope:auth-changed'));
}

async function publicRequest<T>(path: string, init: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, { ...init, credentials: 'include', headers: { 'Content-Type': 'application/json', ...init.headers } });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { error?: string; title?: string } | null;
    throw new Error(payload?.error ?? payload?.title ?? `Request failed (${response.status})`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export async function login(input: LoginInput) { const session = await publicRequest<AuthSession>('/api/auth/login', { method: 'POST', body: JSON.stringify(input) }); storeSession(session); return session; }
export async function register(input: RegisterInput) { const session = await publicRequest<AuthSession>('/api/auth/register', { method: 'POST', body: JSON.stringify(input) }); storeSession(session); return session; }
export async function refreshSession(): Promise<AuthSession | null> {
  if (refreshPromise) return refreshPromise;
  refreshPromise = (async () => {
    try { const session = await publicRequest<AuthSession>('/api/auth/refresh', { method: 'POST' }); storeSession(session); return session; }
    catch { storeSession(null); return null; }
    finally { refreshPromise = null; }
  })();
  return refreshPromise;
}
export async function logout() { try { await publicRequest<void>('/api/auth/logout', { method: 'POST' }); } finally { storeSession(null); } }

export async function authRequest<T>(path: string, init: RequestInit = {}, retry = true): Promise<T> {
  let token = getAccessToken();
  if (!token) token = (await refreshSession())?.accessToken ?? null;
  if (!token) throw new Error('AUTH_REQUIRED');
  const response = await fetch(`${API_URL}${path}`, { ...init, credentials: 'include', headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, ...init.headers } });
  if (response.status === 401 && retry) {
    const refreshed = await refreshSession();
    if (!refreshed) throw new Error('AUTH_REQUIRED');
    return authRequest<T>(path, init, false);
  }
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { error?: string } | null;
    throw new Error(payload?.error ?? `Request failed (${response.status})`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export function listUserOrganizations() { return authRequest<UserOrganization[]>('/api/auth/organizations'); }
export async function switchOrganization(organizationId: string) {
  const session = await authRequest<AuthSession>('/api/auth/switch-organization', { method: 'POST', body: JSON.stringify({ organizationId }) });
  storeSession(session);
  return session;
}
