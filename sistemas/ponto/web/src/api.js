// Endereço da API (vem do arquivo .env na hora do build/dev)
export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5001'

// fetch com tempo limite de 5 s e mensagens de erro em português.
// - API desligada (docker stop)  -> "conexão recusada" quase na hora
// - API pausada  (docker pause)  -> fica esperando... e estoura o tempo limite
export async function api(caminho, opcoes = {}) {
  const ctrl = new AbortController()
  const timer = setTimeout(() => ctrl.abort(), 5000)
  try {
    const resp = await fetch(API_URL + caminho, {
      headers: { 'Content-Type': 'application/json' },
      ...opcoes,
      signal: ctrl.signal
    })
    if (resp.status === 204) return null
    const corpo = await resp.json().catch(() => null)
    if (!resp.ok) throw new Error(corpo?.erro || `Erro HTTP ${resp.status}`)
    return corpo
  } catch (e) {
    if (e.name === 'AbortError') throw new Error(`A API (${API_URL}) não respondeu em 5 segundos. Ela está pausada ou travada?`)
    if (e instanceof TypeError) throw new Error(`Não foi possível conectar na API (${API_URL}). Ela está fora do ar?`)
    throw e
  } finally {
    clearTimeout(timer)
  }
}
