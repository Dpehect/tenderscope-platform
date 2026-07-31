'use client';

import { authRequest } from './auth-api';

export type Watchlist = {
  id: string;
  organizationId: string;
  createdByUserId: string;
  name: string;
  query?: string | null;
  country?: string | null;
  category?: string | null;
  notificationsEnabled: boolean;
  createdAt: string;
};

export type WatchlistInput = {
  name: string;
  query?: string | null;
  country?: string | null;
  category?: string | null;
  notificationsEnabled: boolean;
};

const base = '/api/workspace/v2/watchlists';

export function listWatchlists() { return authRequest<Watchlist[]>(`${base}/`); }
export function createWatchlist(input: WatchlistInput) { return authRequest<Watchlist>(`${base}/`, { method: 'POST', body: JSON.stringify(input) }); }
export function updateWatchlist(id: string, input: WatchlistInput) { return authRequest<Watchlist>(`${base}/${id}`, { method: 'PUT', body: JSON.stringify(input) }); }
export function setWatchlistNotifications(id: string, enabled: boolean) { return authRequest<Watchlist>(`${base}/${id}/notifications`, { method: 'PATCH', body: JSON.stringify({ enabled }) }); }
export function deleteWatchlist(id: string) { return authRequest<void>(`${base}/${id}`, { method: 'DELETE' }); }
