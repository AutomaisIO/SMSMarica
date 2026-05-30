import { useState } from 'react'
import { PatientApi, FhirError, type FhirBundle } from './api/fhirClient'

export default function App() {
  const [termo, setTermo] = useState('')
  const [bundle, setBundle] = useState<FhirBundle | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [carregando, setCarregando] = useState(false)

  async function buscar(e: React.FormEvent) {
    e.preventDefault()
    setErro(null)
    setBundle(null)
    setCarregando(true)
    try {
      const r = await PatientApi.search({ name: termo || undefined })
      setBundle(r)
    } catch (ex) {
      setErro(ex instanceof FhirError ? `Erro ${ex.status}` : String(ex))
    } finally {
      setCarregando(false)
    }
  }

  return (
    <main style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 720, margin: '2rem auto', padding: '0 1rem' }}>
      <h1>Automais FHIR</h1>
      <p style={{ color: '#666' }}>Hub FHIR R4 — busca de pacientes (primeira fatia).</p>

      <form onSubmit={buscar} style={{ display: 'flex', gap: 8, marginTop: 16 }}>
        <input
          value={termo}
          onChange={(e) => setTermo(e.target.value)}
          placeholder="Nome do paciente"
          style={{ flex: 1, padding: 8 }}
        />
        <button type="submit" disabled={carregando}>
          {carregando ? 'Buscando…' : 'Buscar'}
        </button>
      </form>

      {erro && <p style={{ color: 'crimson' }}>{erro}</p>}

      {bundle && (
        <ul style={{ marginTop: 16 }}>
          {(bundle.entry ?? []).map((e, i) => (
            <li key={e.resource.id ?? i}>
              {(e.resource as { name?: Array<{ text?: string }> }).name?.[0]?.text ?? '(sem nome)'} —{' '}
              <code>{e.resource.id}</code>
            </li>
          ))}
          {(bundle.entry ?? []).length === 0 && <li style={{ color: '#666' }}>Nenhum paciente encontrado.</li>}
        </ul>
      )}
    </main>
  )
}
