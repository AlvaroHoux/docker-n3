import { useEffect, useState } from 'react'
import { api, API_URL } from './api'
import StatusApi from './StatusApi'

const hoje = () => new Date().toLocaleDateString('sv-SE')

export default function App() {
  const [funcionarios, setFuncionarios] = useState([])
  const [registros, setRegistros] = useState([])
  const [filtro, setFiltro] = useState('')
  const [form, setForm] = useState({ funcionarioId: '', data: hoje(), entrada: '08:00', saida: '17:00' })
  const [erroFunc, setErroFunc] = useState('')
  const [erro, setErro] = useState('')
  const [msg, setMsg] = useState('')

  // Funcionários vêm do Sistema 1 (através da API do ponto)
  async function carregarFuncionarios() {
    try { setErroFunc(''); setFuncionarios(await api('/api/funcionarios')) }
    catch (e) { setErroFunc(e.message); setFuncionarios([]) }
  }

  // Marcações vêm do ponto.json (não depende do Sistema 1)
  async function carregarRegistros(id = filtro) {
    try { setErro(''); setRegistros(await api('/api/ponto' + (id ? `?funcionarioId=${id}` : ''))) }
    catch (e) { setErro(e.message) }
  }

  useEffect(() => { carregarFuncionarios(); carregarRegistros() }, [])

  async function lancar(ev) {
    ev.preventDefault()
    setMsg(''); setErro('')
    try {
      const r = await api('/api/ponto', { method: 'POST', body: JSON.stringify({ ...form, funcionarioId: Number(form.funcionarioId) }) })
      setMsg(`Ponto lançado para ${r.nomeFuncionario}: ${r.horasTrabalhadas} h`)
      carregarRegistros()
    } catch (e) { setErro(e.message) }
  }

  async function gerar() {
    setMsg(''); setErro('')
    try {
      const corpo = { dias: 30, funcionarioId: form.funcionarioId ? Number(form.funcionarioId) : null }
      const r = await api('/api/ponto/gerar', { method: 'POST', body: JSON.stringify(corpo) })
      setMsg(r.mensagem)
      carregarRegistros()
    } catch (e) { setErro(e.message) }
  }

  async function excluir(r) {
    try { await api(`/api/ponto/${r.id}`, { method: 'DELETE' }); carregarRegistros() }
    catch (e) { setErro(e.message) }
  }

  return (
    <>
      <header>
        <h1>Sistema 2 — Controle de Ponto</h1>
        <small>API: {API_URL} · <StatusApi /></small>
      </header>
      <main>
        {erroFunc && <div className="erro">⚠️ Não foi possível carregar os funcionários: {erroFunc}</div>}
        {erro && <div className="erro">⚠️ {erro}</div>}
        {msg && <div className="ok">✅ {msg}</div>}

        <form className="cartao linha" onSubmit={lancar}>
          <label>Funcionário
            <select value={form.funcionarioId} onChange={e => setForm({ ...form, funcionarioId: e.target.value })}>
              <option value="">(todos — só para "Gerar 30 dias")</option>
              {funcionarios.map(f => <option key={f.id} value={f.id}>{f.id} - {f.nome}</option>)}
            </select>
          </label>
          <label>Data <input type="date" value={form.data} onChange={e => setForm({ ...form, data: e.target.value })} required /></label>
          <label>Entrada <input type="time" value={form.entrada} onChange={e => setForm({ ...form, entrada: e.target.value })} required /></label>
          <label>Saída <input type="time" value={form.saida} onChange={e => setForm({ ...form, saida: e.target.value })} required /></label>
          <button type="submit" disabled={!form.funcionarioId}>Lançar ponto</button>
          <button type="button" className="sec" onClick={gerar}>Gerar 30 dias úteis</button>
          <button type="button" className="sec" onClick={carregarFuncionarios}>Recarregar funcionários</button>
        </form>

        <div className="cartao">
          <div className="linha" style={{ marginBottom: 10 }}>
            <label>Filtrar por funcionário
              <select value={filtro} onChange={e => { setFiltro(e.target.value); carregarRegistros(e.target.value) }}>
                <option value="">Todos</option>
                {funcionarios.map(f => <option key={f.id} value={f.id}>{f.nome}</option>)}
              </select>
            </label>
            <button className="sec" onClick={() => carregarRegistros()}>Recarregar marcações</button>
            <span>{registros.length} marcação(ões)</span>
          </div>
          <table>
            <thead><tr><th>Data</th><th>Funcionário</th><th>Entrada</th><th>Saída</th><th>Horas</th><th></th></tr></thead>
            <tbody>
              {registros.map(r => (
                <tr key={r.id}>
                  <td>{r.data.split('-').reverse().join('/')}</td><td>{r.nomeFuncionario}</td>
                  <td>{r.entrada}</td><td>{r.saida}</td><td>{r.horasTrabalhadas}</td>
                  <td><button className="perigo" onClick={() => excluir(r)}>Excluir</button></td>
                </tr>
              ))}
              {registros.length === 0 && <tr><td colSpan="6">Nenhuma marcação.</td></tr>}
            </tbody>
          </table>
        </div>
      </main>
    </>
  )
}
