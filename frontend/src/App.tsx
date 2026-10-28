import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { api, type Account, type Note, type Settings, type UserRow } from './api/client'
import './App.css'

const roles = ['Administrator', 'Kierownik', 'Redaktor', 'Pracownik', 'Czytelnik']
type SecretResult = { temporaryPassword?: string; expiresAtUtc?: string; created?: unknown[]; recoveryCodes?: string[]; secret?: string; otpauthUri?: string }

function UserEditor({ row, refresh, output }: { row: UserRow; refresh: () => Promise<void>; output: (v: unknown) => void }) {
  const [login, setLogin] = useState(row.userName)
  const [email, setEmail] = useState(row.email ?? '')
  const [role, setRole] = useState(row.roles[0] ?? 'Czytelnik')
  const [busy, setBusy] = useState(false)
  async function action(path: string, method: string, body?: unknown) {
    setBusy(true)
    try { output(await api('/admin/users/' + row.id + path, method, body)); await refresh() }
    catch (e) { output(String(e)) }
    finally { setBusy(false) }
  }
  return <tr>
    <td><input aria-label="Login użytkownika" value={login} onChange={e => setLogin(e.target.value)} disabled={row.userName.toUpperCase() === 'ADMIN'} />
      <small>{row.isBlocked ? 'Zablokowany' : row.mustChangePassword ? 'Zmiana hasła wymagana' : 'Aktywny'}</small></td>
    <td><input aria-label="E-mail użytkownika" value={email} onChange={e => setEmail(e.target.value)} /></td>
    <td><select aria-label="Rola" value={role} disabled={row.userName.toUpperCase() === 'ADMIN'} onChange={e => setRole(e.target.value)}>{roles.map(r => <option key={r}>{r}</option>)}</select></td>
    <td className="actions">
      <button disabled={busy} onClick={() => action('', 'PUT', { login, email: email || null, role })}>Zapisz</button>
      <button disabled={busy} onClick={() => action('/reset-password', 'POST')}>Reset hasła</button>
      {row.isBlocked ? <button disabled={busy} onClick={() => action('/unblock', 'POST')}>Odblokuj</button> :
        <button disabled={busy || row.userName.toUpperCase() === 'ADMIN'} onClick={() => action('/block', 'POST')}>Blokuj</button>}
      <button className="danger" disabled={busy || row.userName.toUpperCase() === 'ADMIN'} onClick={() => {
        if (window.confirm('Usunąć konto ' + row.userName + '?')) void action('', 'DELETE')
      }}>Usuń</button>
    </td>
  </tr>
}

export default function App() {
  const [account, setAccount] = useState<Account | null>(null)
  const [login, setLogin] = useState('ADMIN')
  const [password, setPassword] = useState('')
  const [oldPassword, setOldPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [totpCode, setTotpCode] = useState('')
  const [totpInfo, setTotpInfo] = useState<SecretResult | null>(null)
  const [notes, setNotes] = useState<Note[]>([])
  const [noteTitle, setNoteTitle] = useState('')
  const [noteContent, setNoteContent] = useState('')
  const [editNoteId, setEditNoteId] = useState<string | null>(null)
  const [users, setUsers] = useState<UserRow[]>([])
  const [newUserLogin, setNewUserLogin] = useState('')
  const [newUserRole, setNewUserRole] = useState('Czytelnik')
  const [settings, setSettings] = useState<Settings | null>(null)
  const [result, setResult] = useState('')
  const [secret, setSecret] = useState('')
  const [busy, setBusy] = useState(false)

  const isAdmin = account?.roles.includes('Administrator') ?? false
  const canWrite = account?.roles.some(r => ['Administrator', 'Kierownik', 'Redaktor', 'Pracownik'].includes(r)) ?? false
  const canDelete = account?.roles.some(r => ['Administrator', 'Kierownik'].includes(r)) ?? false
  const canEditAll = account?.roles.some(r => ['Administrator', 'Kierownik', 'Redaktor'].includes(r)) ?? false

  const refresh = useCallback(async () => {
    try {
      const me = await api<Account>('/auth/me')
      setAccount(me)
      if (me.level === 'full') {
        setNotes(await api<Note[]>('/notes'))
        if (me.roles.includes('Administrator')) {
          setUsers(await api<UserRow[]>('/admin/users'))
          setSettings(await api<Settings>('/admin/settings'))
        }
      }
    } catch { setAccount(null); setNotes([]); setUsers([]) }
  }, [])
  useEffect(() => { void refresh() }, [refresh])

  async function run(task: () => Promise<unknown>, options?: { reload?: boolean; showSecret?: boolean }) {
    setBusy(true); setResult(''); setSecret('')
    try {
      const value = await task()
      setResult('Operacja zakończona pomyślnie.')
      if (options?.showSecret) setSecret(JSON.stringify(value, null, 2))
      if (options?.reload !== false) await refresh()
    } catch (e) { setResult(e instanceof Error ? e.message : String(e)) }
    finally { setBusy(false) }
  }
  async function submitLogin(e: FormEvent) {
    e.preventDefault()
    await run(async () => { await api('/auth/login', 'POST', { login, password }); setOldPassword(password); setPassword('') })
  }
  async function submitPassword(e: FormEvent) {
    e.preventDefault()
    await run(async () => {
      await api('/auth/change-password', 'POST', { currentPassword: oldPassword, newPassword, confirmNewPassword: confirmPassword })
      setOldPassword(''); setNewPassword(''); setConfirmPassword('')
    })
  }
  async function saveNote(e: FormEvent) {
    e.preventDefault()
    await run(async () => {
      await api(editNoteId ? '/notes/' + editNoteId : '/notes', editNoteId ? 'PUT' : 'POST',
        { title: noteTitle, content: noteContent })
      setEditNoteId(null); setNoteTitle(''); setNoteContent('')
    })
  }
  async function createUser(e: FormEvent) {
    e.preventDefault()
    await run(() => api('/admin/users', 'POST', { login: newUserLogin, role: newUserRole }), { showSecret: true })
    setNewUserLogin('')
  }

  return <main className="container">
    <header><div><h1>AccessLab</h1><p>Prosty panel testowy kont, sesji i notatek</p></div>
      <div className="header-actions"><span>{account ? `${account.login} · ${account.level}` : 'Niezalogowany'}</span>
        <button onClick={() => void refresh()}>Odśwież</button>
        {account && <button onClick={() => void run(() => api('/auth/logout', 'POST'))}>Wyloguj</button>}
      </div></header>
    {result && <p role="status" className="status">{result}</p>}
    {secret && <section className="secret"><strong>Dane jednorazowe — zapisz bezpiecznie, nie udostępniaj:</strong><pre>{secret}</pre></section>}

    {!account && <section className="panel"><h2>Logowanie</h2><form onSubmit={e => void submitLogin(e)}>
      <label>Login<input autoComplete="username" value={login} onChange={e => setLogin(e.target.value)} required /></label>
      <label>Hasło<input type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required /></label>
      <button disabled={busy}>Zaloguj</button>
    </form></section>}

    {account?.level === 'password' && <section className="panel"><h2>Wymagana zmiana hasła</h2><p>Pierwsza sesja ma ograniczone uprawnienia.</p>
      <form onSubmit={e => void submitPassword(e)}>
        <label>Obecne hasło<input type="password" autoComplete="current-password" value={oldPassword} onChange={e => setOldPassword(e.target.value)} required /></label>
        <label>Nowe hasło (co najmniej 16 znaków)<input type="password" autoComplete="new-password" value={newPassword} onChange={e => setNewPassword(e.target.value)} required minLength={16}/></label>
        <label>Powtórz nowe hasło<input type="password" autoComplete="new-password" value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} required /></label>
        <button disabled={busy}>Zmień hasło</button>
      </form></section>}

    {account?.level === 'totp' && <section className="panel"><h2>Drugi składnik logowania</h2>
      <p>Wpisz 6-cyfrowy kod z aplikacji lub kod odzyskiwania.</p>
      <form onSubmit={e => { e.preventDefault(); void run(() => api('/auth/totp/verify', 'POST', { code: totpCode })) }}>
        <label>Kod TOTP<input value={totpCode} onChange={e => setTotpCode(e.target.value)} required /></label>
        <button disabled={busy}>Potwierdź</button>
      </form></section>}

    {account?.level === 'full' && <>
      <section className="panel"><div className="panel-title"><h2>Notatki</h2><button onClick={() => void refresh()}>Odśwież listę</button></div>
        {canWrite && <form onSubmit={e => void saveNote(e)}>
          <label>Tytuł<input maxLength={150} value={noteTitle} onChange={e => setNoteTitle(e.target.value)} required /></label>
          <label>Treść<textarea maxLength={10000} rows={3} value={noteContent} onChange={e => setNoteContent(e.target.value)} required /></label>
          <div className="actions"><button disabled={busy}>{editNoteId ? 'Zapisz edycję' : 'Dodaj notatkę'}</button>
            {editNoteId && <button type="button" onClick={() => {setEditNoteId(null);setNoteTitle('');setNoteContent('')}}>Anuluj</button>}</div>
        </form>}
        <table><thead><tr><th>Tytuł</th><th>Treść</th><th>Autor</th><th>Akcje</th></tr></thead><tbody>
          {notes.map(n => <tr key={n.id}><td>{n.title}</td><td className="note-body">{n.content}</td><td>{n.authorId === account.id ? 'Ty' : n.authorId.slice(0,8)}</td>
          <td className="actions">{(canEditAll || (account.roles.includes('Pracownik') && n.authorId === account.id)) &&
            <button onClick={() => {setEditNoteId(n.id);setNoteTitle(n.title);setNoteContent(n.content)}}>Edytuj</button>}
            {canDelete && <button className="danger" onClick={() => {if (window.confirm('Usunąć notatkę?')) void run(() => api('/notes/' + n.id, 'DELETE'))}}>Usuń</button>}
          </td></tr>)}
          {notes.length === 0 && <tr><td colSpan={4}>Brak notatek.</td></tr>}
        </tbody></table></section>

      <section className="panel"><h2>Zmiana własnego hasła</h2><form onSubmit={e => void submitPassword(e)}>
        <label>Obecne<input type="password" value={oldPassword} onChange={e => setOldPassword(e.target.value)} required /></label>
        <label>Nowe<input type="password" value={newPassword} onChange={e => setNewPassword(e.target.value)} minLength={16} required /></label>
        <label>Potwierdź<input type="password" value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} required /></label>
        <button disabled={busy}>Zmień hasło</button>
      </form></section>

      <section className="panel"><h2>TOTP (opcjonalnie)</h2><p>Skonfiguruj aplikację uwierzytelniającą, a potem potwierdź kod.</p>
        <div className="actions"><button onClick={() => void run(async () => { setTotpInfo(await api<SecretResult>('/auth/totp/setup', 'POST')) }, { reload: false })}>Pokaż sekret TOTP</button></div>
        {totpInfo && <div className="secret"><p>Wprowadź poniższy sekret w aplikacji (nie udostępniaj):</p><code>{totpInfo.secret}</code><p>URI: <code>{totpInfo.otpauthUri}</code></p></div>}
        <form onSubmit={e => {e.preventDefault(); void run(() => api('/auth/totp/enable', 'POST', { code: totpCode }), { showSecret: true })}}>
          <label>Kod z aplikacji<input value={totpCode} onChange={e => setTotpCode(e.target.value)} required /></label>
          <button disabled={busy}>Włącz TOTP / pokaż kody odzyskiwania</button>
        </form>
      </section>

      {isAdmin && <>
        <section className="panel"><h2>Zarządzanie użytkownikami</h2><form className="inline" onSubmit={e => void createUser(e)}>
          <label>Nowy login<input value={newUserLogin} onChange={e => setNewUserLogin(e.target.value)} required /></label>
          <label>Rola<select value={newUserRole} onChange={e => setNewUserRole(e.target.value)}>{roles.map(r => <option key={r}>{r}</option>)}</select></label>
          <button disabled={busy}>Utwórz konto + hasło tymczasowe</button>
        </form>
        
        <table><thead><tr><th>Konto</th><th>E-mail</th><th>Rola</th><th>Operacje</th></tr></thead>
          <tbody>{users.map(u => <UserEditor key={u.id} row={u} refresh={refresh} output={v => {setResult('Operacja wykonana'); setSecret(v ? JSON.stringify(v,null,2):'')}} />)}</tbody></table>
        </section>
        <section className="panel"><h2>Ustawienia bezpieczeństwa</h2>{settings && <form onSubmit={e => {e.preventDefault(); void run(() => api('/admin/settings', 'PUT', settings))}}>
          <label>Minimalna długość hasła<input type="number" min="16" max="128" value={settings.minimumPasswordLength} onChange={e => setSettings({...settings, minimumPasswordLength: Number(e.target.value)})} /></label>
          <label>Ważność hasła (dni, 0 = bez limitu)<input type="number" min="0" max="3650" value={settings.passwordValidityDays} onChange={e => setSettings({...settings, passwordValidityDays: Number(e.target.value)})} /></label>
          <label>Bezczynność sesji (minuty)<input type="number" min="5" max="240" value={settings.sessionIdleMinutes} onChange={e => setSettings({...settings, sessionIdleMinutes: Number(e.target.value)})} /></label>
          <label className="checkbox"><input type="checkbox" checked={settings.requirePasswordDistance} onChange={e => setSettings({...settings, requirePasswordDistance: e.target.checked})} /> Odległość Levenshteina ≥ 4</label>
          <button disabled={busy}>Zapisz ustawienia</button>
        </form>}</section>
      </>}
    </>}
    <footer>AccessLab · interfejs demonstracyjny · API: <a href="https://localhost:7068/swagger" target="_blank" rel="noreferrer">Swagger</a></footer>
  </main>
}
