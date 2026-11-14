import { useEffect, useState } from 'react'
import { api, API_URL } from './api'
import StatusApi from './StatusApi'

const vazio = { nome: '', cargo: '', valorHora: '' }

export default function App() {
  const [funcionarios, setFuncionarios] = useState([])
  const [form, setForm] = useState(vazio)
  const [editando, setEditando] = useState(null)
  const [erro, setErro] = useState('')
  const [msg, setMsg] = useState('')

  async function carregar() {
    try {
      setErro('')
      setFuncionarios(await api('/api/funcionarios'))
    } catch (e) { setErro(e.message) }
  }

  useEffect(() => { carregar() }, [])

  async function salvar(ev) {
    ev.preventDefault()
    setMsg(''); setErro('')
    try {
      const corpo = JSON.stringify({ ...form, valorHora: Number(form.valorHora) })
      if (editando) await api(`/api/funcionarios/${editando}`, { method: 'PUT', body: corpo })
      else await api('/api/funcionarios', { method: 'POST', body: corpo })
      setMsg(editando ? 'Funcionário atualizado!' : 'Funcionário cadastrado!')
      setForm(vazio); setEditando(null)
      carregar()
    } catch (e) { setErro(e.message) }
  }

  async function excluir(f) {
    if (!confirm(`Excluir ${f.nome}?`)) return
    try { await api(`/api/funcionarios/${f.id}`, { method: 'DELETE' }); carregar() }
    catch (e) { setErro(e.message) }
  }

  async function exemplo() {
    try { const r = await api('/api/seed', { method: 'POST' }); setMsg(r.mensagem); carregar() }
    catch (e) { setErro(e.message) }
  }

  return (
    <>
      <header>
        <h1>Sistema 1 — Cadastro de Funcionários</h1>
        <small>API: {API_URL} · <StatusApi /></small>
      </header>
      <main>
        {erro && <div className="erro">⚠️ {erro}</div>}
        {msg && <div className="ok">✅ {msg}</div>}

        <form className="cartao linha" onSubmit={salvar}>
          <label>Nome
            <input value={form.nome} onChange={e => setForm({ ...form, nome: e.target.value })} required />
          </label>
          <label>Cargo
            <input value={form.cargo} onChange={e => setForm({ ...form, cargo: e.target.value })} required />
          </label>
          <label>Valor da hora (R$)
            <input type="number" step="0.01" min="0.01" value={form.valorHora}
              onChange={e => setForm({ ...form, valorHora: e.target.value })} required />
          </label>
          <button type="submit">{editando ? 'Salvar alteração' : 'Cadastrar'}</button>
          {editando && <button type="button" className="sec" onClick={() => { setEditando(null); setForm(vazio) }}>Cancelar</button>}
          <button type="button" className="sec" onClick={exemplo}>Criar 5 de exemplo</button>
          <button type="button" className="sec" onClick={carregar}>Recarregar</button>
        </form>

        <div className="cartao">
          <table>
            <thead><tr><th>ID</th><th>Nome</th><th>Cargo</th><th>Valor/hora</th><th></th></tr></thead>
            <tbody>
              {funcionarios.map(f => (
                <tr key={f.id}>
                  <td>{f.id}</td><td>{f.nome}</td><td>{f.cargo}</td>
                  <td>{f.valorHora.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}</td>
                  <td>
                    <button className="sec" onClick={() => { setEditando(f.id); setForm({ nome: f.nome, cargo: f.cargo, valorHora: f.valorHora }) }}>Editar</button>{' '}
                    <button className="perigo" onClick={() => excluir(f)}>Excluir</button>
                  </td>
                </tr>
              ))}
              {funcionarios.length === 0 && <tr><td colSpan="5">Nenhum funcionário cadastrado.</td></tr>}
            </tbody>
          </table>
        </div>
      </main>
    </>
  )
}
