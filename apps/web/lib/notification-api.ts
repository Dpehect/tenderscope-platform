'use client';

import { authRequest } from './auth-api';

export type NotificationItem = {
  id: string;
  type: string;
  title: string;
  message: string;
  resourceUrl?: string | null;
  createdAt: string;
  readAt?: string | null;
  isRead: boolean;
};

export type NotificationFeed = { unread: number; items: NotificationItem[] };
export type NotificationPreferences = {
  inAppEnabled: boolean;
  watchlistMatchesEnabled: boolean;
  deadlineRemindersEnabled: boolean;
};

export function listNotifications(unreadOnly = false) {
  return authRequest<NotificationFeed>(`/api/notifications/?unreadOnly=${unreadOnly}`);
}
export function markNotificationRead(id: string) {
  return authRequest<NotificationItem>(`/api/notifications/${id}/read`, { method: 'PATCH' });
}
export function markAllNotificationsRead() {
  return authRequest<{ updated: number }>('/api/notifications/read-all', { method: 'POST' });
}
export function getNotificationPreferences() {
  return authRequest<NotificationPreferences>('/api/notifications/preferences');
}
export function updateNotificationPreferences(value: NotificationPreferences) {
  return authRequest<NotificationPreferences>('/api/notifications/preferences', { method: 'PUT', body: JSON.stringify(value) });
}
export function runWatchlistMatching() {
  return authRequest<{ created: number; searches: number }>('/api/notifications/run-watchlist-matches', { method: 'POST' });
}
