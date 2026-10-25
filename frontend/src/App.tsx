import { useState } from "react";
import { getHealth } from "./api/health";

export default function App() {
  const [mesage, setMessage] = useState('Nie sprawdzono połaczenia z backendem')
  const [loading, setLoading] = useState(false)

  async function handleCheckConnection() {
    setLoading(true)
    setMessage('Łączenie ...')

    try {
      const health = await getHealth()
      setMessage(`Połączono z backendem: ${health.message} (wersja: ${health.version})`)
    }
    catch
    {
      setMessage('Nie udało się połączyć z API')
    }
    finally{
      setLoading(false)
    }
  }

  return (
    <main>
      <h1>AccessLab</h1>
      <p>Panel użytkowników i notatek</p>

      <button onClick={handleCheckConnection} disabled={loading}>
        {loading ? 'Łączenie ...' : 'Sprawdź połączenie z backendem'}
      </button>

      <p role="status">{mesage}</p>

    </main>
  )
}