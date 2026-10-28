export type Account = {
  id: string; login: string; level: 'password' | 'totp' | 'full';
  requiresPasswordChange: boolean; requiresTwoFactor: boolean; roles: string[]
}
export type UserRow = {
  id: string; userName: string; email: string | null; isBlocked: boolean;
  mustChangePassword: boolean; twoFactorEnabled: boolean; roles: string[];
  temporaryPasswordExpiresAtUtc: string | null
}
export type Note = {
  id: string; title: string; content: string; authorId: string;
  createdAtUtc: string; updatedAtUtc: string
}
export type Settings = {
  minimumPasswordLength: number; requirePasswordDistance: boolean;
  passwordValidityDays: number; sessionIdleMinutes: number
}
export async function api<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (method !== 'GET' && method !== 'HEAD') {
    const response = await fetch('/api/auth/csrf', { credentials: 'include', cache: 'no-store' })
    if (!response.ok) throw new Error('Nie udało się pobrać tokenu CSRF')
    const csrf: {token: string} = await response.json()
    headers['X-CSRF-TOKEN'] = csrf.token
  }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const response = await fetch('/api' + path, {
    method, headers, credentials: 'include', cache: 'no-store',
    body: body === undefined ? undefined : JSON.stringify(body)
  })
  if (response.status === 204) return undefined as T
  const text = await response.text()
  let data: unknown = null
  try { data = text ? JSON.parse(text) : null } catch { data = { message: text } }
  if (!response.ok) {
    const message = typeof data === 'object' && data !== null && 'message' in data
      ? String(data.message) : `HTTP ${response.status}`
    throw new Error(message)
  }
  return data as T
}
