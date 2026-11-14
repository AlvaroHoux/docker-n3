import { useEffect, useState } from 'react'
import { api } from './api'

// Bolinha que consulta GET /health a cada 3 segundos.
// Use no vídeo: pare a API e veja a bolinha ficar vermelha (o front continua de pé!).
export default function StatusApi() {
  const [estado, setEstado] = useState({ ok: null, texto: 'verificando...' })

  useEffect(() => {
    let ativo = true
    async function checar() {
      try {
        const h = await api('/health')
        const deps = h.dependencias ? Object.entries(h.dependencias).filter(([, v]) => v !== 'ok').map(([k]) => k) : []
        if (ativo) setEstado({ ok: deps.length === 0, texto: deps.length ? `API no ar, mas sem: ${deps.join(', ')}` : 'API online' })
      } catch (e) {
        if (ativo) setEstado({ ok: false, texto: 'API OFFLINE' })
      }
    }
    checar()
    const t = setInterval(checar, 3000)
    return () => { ativo = false; clearInterval(t) }
  }, [])

  const cor = estado.ok === null ? '#ccc' : estado.ok ? '#3ddc84' : '#ff5252'
  return (
    <span className="status">
      <span style={{ display: 'inline-block', width: 10, height: 10, borderRadius: 5, background: cor, marginRight: 6 }} />
      {estado.texto}
    </span>
  )
}
