'use client';
import { authRequest } from './auth-api';
const API_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:8080';
export type AdminOverview={users:number;organizations:number;tenders:number;sources:number;healthySources:number;failingSources:number;unreadNotifications:number;activeRefreshTokens:number;generatedAt:string};
export type AdminSource={id:string;key:string;name:string;countryCode:string;health:number;isEnabled:boolean;consecutiveFailures:number;crawlIntervalMinutes:number;nextCrawlAt?:string;lastSuccessAt?:string;lastError?:string};
export const getAdminOverview=()=>authRequest<AdminOverview>('/api/admin/v2/overview');
export const getAdminSources=()=>authRequest<AdminSource[]>('/api/admin/v2/sources');
export const updateAdminSource=(id:string,enabled:boolean,intervalMinutes:number)=>authRequest<AdminSource>(`/api/admin/v2/sources/${id}`,{method:'PATCH',body:JSON.stringify({enabled,intervalMinutes})});
export const runAdminSource=(id:string)=>authRequest<{imported:number}>(`/api/admin/v2/sources/${id}/run`,{method:'POST'});
export const getAdminOrganizations=()=>authRequest<any[]>('/api/admin/v2/organizations');
export const getAdminUsers=()=>authRequest<any[]>('/api/admin/v2/users');
export const getAdminAudit=()=>authRequest<any[]>('/api/admin/v2/audit?take=200');
export function downloadAdminReport(format:'csv'|'excel'|'pdf'){
 const token=localStorage.getItem('tenderscope:access-token');
 fetch(`${API_URL}/api/admin/v2/reports/${format}`,{headers:{Authorization:`Bearer ${token}`}}).then(async r=>{if(!r.ok)throw new Error('Report failed');const blob=await r.blob();const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=`tenderscope-report.${format==='excel'?'xls':format}`;a.click();URL.revokeObjectURL(url);});
}
