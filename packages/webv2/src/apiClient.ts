import { fetchAuthSession } from 'aws-amplify/auth';

let baseUrl = '';

export function configureApiClient(endpoint: string) {
  baseUrl = endpoint;
}

async function authHeaders(): Promise<Record<string, string>> {
  const session = await fetchAuthSession();
  const token = session.tokens?.idToken?.toString();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export async function apiGet<T = any>(path: string, queryParams?: Record<string, string>): Promise<T> {
  let url = `${baseUrl}${path}`;
  if (queryParams) {
    url += `?${new URLSearchParams(queryParams)}`;
  }
  const res = await fetch(url, { headers: await authHeaders() });
  if (!res.ok) throw new Error(`API ${res.status}: ${res.statusText}`);
  return res.json();
}

export async function apiPost<T = any>(path: string, options?: {
  body?: unknown;
  queryParams?: Record<string, string>;
}): Promise<T> {
  let url = `${baseUrl}${path}`;
  if (options?.queryParams) {
    url += `?${new URLSearchParams(options.queryParams)}`;
  }
  const headers: Record<string, string> = await authHeaders();
  if (options?.body) {
    headers['Content-Type'] = 'application/json';
  }
  const res = await fetch(url, {
    method: 'POST',
    headers,
    body: options?.body ? JSON.stringify(options.body) : undefined,
  });
  if (!res.ok) throw new Error(`API ${res.status}: ${res.statusText}`);
  return res.json();
}
