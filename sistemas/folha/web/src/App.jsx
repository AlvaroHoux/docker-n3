import { useEffect, useState } from 'react'
import { api, API_URL } from './api'
import StatusApi from './StatusApi'

const brl = v => Number(v).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
const br = d => d ? d.split('-').reverse().join('/') : ''

function inicioDoMes() {
  const d = new Date(); d.setDate(1); d.setMonth(d.getMonth() - 1)
  return d.toLocaleDateString('sv-SE')
}

export default function App() {
  const [funcionarios, setFuncionarios] = useState([])
  const [id, setId] = useState('')
  const [inicio, setInicio] = useState(inicioDoMes())
  const [fim, setFim] = useState(new Date().toLocaleDateString('sv-SE'))
  const [folha, setFolha] = useState(null)
  const [resumo, setResumo] = useState(null)
  const [erro, setErro] = useState('')

  async function carregarFuncionarios() {
    try { setErro(''); setFuncionarios(await api('/api/funcionarios')) }
    catch (e) { setErro(e.message) }
  }
  useEffect(() => { carregarFuncionarios() }, [])

  async function gerar() {
    setErro(''); setFolha(null); setResumo(null)
    try {
      if (id) setFolha(await api(`/api/folha/${id}?inicio=${inicio}&fim=${fim}`))
      else setResumo(await api(`/api/folha?inicio=${inicio}&fim=${fim}`))
    } catch (e) { setErro(e.message) }
  }

  return (
    <>
      <header>
        <h1>Sistema 3 — Folha de Ponto</h1>
        <small>API: {API_URL} · <StatusApi /></small>
      </header>
      <main>
        {erro && <div className="erro">⚠️ {erro}</div>}

        <div className="cartao linha naoimprime">
          <label>Funcionário
            <select value={id} onChange={e => setId(e.target.value)}>
              <option value="">Todos (resumo)</option>
              {funcionarios.map(f => <option key={f.id} value={f.id}>{f.nome}</option>)}
            </select>
          </label>
          <label>De <input type="date" value={inicio} onChange={e => setInicio(e.target.value)} /></label>
          <label>Até <input type="date" value={fim} onChange={e => setFim(e.target.value)} /></label>
          <button onClick={gerar}>Gerar folha</button>
          <button className="sec" onClick={carregarFuncionarios}>Recarregar funcionários</button>
          {(folha || resumo) && <button className="sec" onClick={() => window.print()}>Imprimir</button>}
        </div>

        {folha && (
          <div className="cartao">
            <h2 style={{ marginTop: 0 }}>Folha de ponto — {folha.funcionario.nome}</h2>
            <p>Cargo: <b>{folha.funcionario.cargo}</b> · Valor da hora: <b>{brl(folha.funcionario.valorHora)}</b> ·
              Período: <b>{br(folha.inicio)} a {br(folha.fim)}</b></p>
            <table>
              <thead><tr><th>Data</th><th>Entrada</th><th>Saída</th><th>Horas</th><th>Valor do dia</th></tr></thead>
              <tbody>
                {folha.registros.map(r => (
                  <tr key={r.id}><td>{br(r.data)}</td><td>{r.entrada}</td><td>{r.saida}</td>
                    <td>{r.horasTrabalhadas}</td><td>{brl(r.horasTrabalhadas * folha.funcionario.valorHora)}</td></tr>
                ))}
                {folha.registros.length === 0 && <tr><td colSpan="5">Nenhum dia trabalhado no período.</td></tr>}
              </tbody>
            </table>
            <p className="total">Dias trabalhados: {folha.diasTrabalhados} · Total de horas: {folha.totalHoras} h · Valor bruto: {brl(folha.valorBruto)}</p>
            <small>Gerada em {new Date(folha.geradaEm).toLocaleString('pt-BR')}</small>
          </div>
        )}

        {resumo && (
          <div className="cartao">
            <h2 style={{ marginTop: 0 }}>Resumo da folha — {br(resumo.inicio)} a {br(resumo.fim)}</h2>
            <table>
              <thead><tr><th>Funcionário</th><th>Cargo</th><th>Dias</th><th>Horas</th><th>Valor/hora</th><th>Valor bruto</th></tr></thead>
              <tbody>
                {resumo.folhas.map(x => (
                  <tr key={x.funcionario.id}><td>{x.funcionario.nome}</td><td>{x.funcionario.cargo}</td>
                    <td>{x.diasTrabalhados}</td><td>{x.totalHoras}</td><td>{brl(x.funcionario.valorHora)}</td><td>{brl(x.valorBruto)}</td></tr>
                ))}
              </tbody>
            </table>
            <p className="total">Total geral: {brl(resumo.totalGeral)}</p>
          </div>
        )}
      </main>
    </>
  )
}
