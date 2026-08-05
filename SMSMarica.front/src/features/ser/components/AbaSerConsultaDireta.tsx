import { useState } from 'react';
import { AlertTriangle, History, Loader2, Radio, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { consultaDiretaSer, historicoDiretoSer } from '@/features/ser/api/serApi';
import {
  ROTULO_SITUACAO,
  SITUACOES_SER,
  type ConsultaDiretaResultado,
  type HistoricoDiretoResultado,
  type LinhaDiretaSer,
  type SituacaoSer,
  type TipoRecursoSer,
} from '@/features/ser/types';

/**
 * Tela de testes: consulta o SER AO VIVO, com os mesmos filtros da tela de lá, e mostra o
 * resultado cru. Nada é gravado.
 *
 * É o ensaio que valida o motor inteiro — login, AJAXREQUEST, ativação de módulo, ViewState,
 * busca, paginação e parsers — em segundos, antes de confiar numa varredura de uma hora.
 */
export function AbaSerConsultaDireta() {
  const [situacao, setSituacao] = useState<SituacaoSer>('EmFila');
  const [tipo, setTipo] = useState<TipoRecursoSer | ''>('');
  const [idSolicitacao, setIdSolicitacao] = useState('');
  const [nome, setNome] = useState('');
  const [cpf, setCpf] = useState('');
  const [inicio, setInicio] = useState('');
  const [fim, setFim] = useState('');
  const [pagina, setPagina] = useState(1);

  const [carregando, setCarregando] = useState(false);
  const [resultado, setResultado] = useState<ConsultaDiretaResultado | null>(null);
  const [historico, setHistorico] = useState<HistoricoDiretoResultado | null>(null);
  const [carregandoHistorico, setCarregandoHistorico] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  async function consultar(paginaAlvo = 1) {
    setCarregando(true);
    setErro(null);
    setHistorico(null);
    try {
      const r = await consultaDiretaSer({
        situacao,
        tipo: tipo || undefined,
        idSolicitacao: idSolicitacao.trim() || undefined,
        nome: nome.trim() || undefined,
        cpf: cpf.trim() || undefined,
        dataSolicitacaoInicio: inicio || undefined,
        dataSolicitacaoFim: fim || undefined,
        pagina: paginaAlvo,
      });
      setResultado(r);
      setPagina(paginaAlvo);
    } catch (e) {
      setResultado(null);
      setErro(extrairMensagemDeErro(e));
    } finally {
      setCarregando(false);
    }
  }

  async function verHistorico(linha: LinhaDiretaSer) {
    setCarregandoHistorico(linha.idSer);
    setErro(null);
    try {
      setHistorico(await historicoDiretoSer(linha.idSer, situacao));
    } catch (e) {
      setHistorico(null);
      setErro(extrairMensagemDeErro(e));
    } finally {
      setCarregandoHistorico(null);
    }
  }

  const colunas: Coluna<LinhaDiretaSer>[] = [
    {
      chave: 'idSer',
      cabecalho: 'ID',
      className: 'w-24',
      render: (l) => <span className="font-mono text-xs">{l.idSer}</span>,
    },
    { chave: 'tipo', cabecalho: 'Tipo', className: 'w-24', render: (l) => <span className="text-xs">{l.tipo ?? '—'}</span> },
    { chave: 'recurso', cabecalho: 'Recurso', render: (l) => <span className="text-sm">{l.recurso ?? '—'}</span> },
    { chave: 'data', cabecalho: 'Solicitado', className: 'w-28', render: (l) => <span className="text-xs">{l.dataSolicitacao ?? '—'}</span> },
    { chave: 'paciente', cabecalho: 'Paciente', render: (l) => <span className="text-sm">{l.paciente ?? '—'}</span> },
    { chave: 'cpf', cabecalho: 'CPF', className: 'w-36', render: (l) => <span className="font-mono text-xs">{l.cpf ?? '—'}</span> },
    { chave: 'situacao', cabecalho: 'Situação', className: 'w-32', render: (l) => <span className="text-xs">{l.situacao ?? '—'}</span> },
    {
      chave: 'acao',
      cabecalho: '',
      className: 'w-28',
      render: (l) => (
        <Button
          variante="secundaria"
          tamanho="sm"
          onClick={() => verHistorico(l)}
          disabled={carregandoHistorico !== null}
        >
          {carregandoHistorico === l.idSer ? (
            <Loader2 className="size-3.5 animate-spin" />
          ) : (
            <History className="size-3.5" />
          )}
          Histórico
        </Button>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <p className="flex items-start gap-2 rounded bg-blue-50 p-3 text-sm text-blue-900">
        <Radio className="mt-0.5 size-4 shrink-0" />
        <span>
          Esta consulta vai <strong>ao vivo no SER</strong>, com os mesmos filtros da tela de lá, e
          mostra o resultado cru — <strong>nada é gravado</strong> na nossa base. Serve para
          conferir campo a campo contra o SER antes de confiar na varredura.
          <br />
          <span className="text-blue-700">
            A sessão do SER é única por operador: consultar aqui derruba a sessão de quem estiver
            logado lá com a credencial cadastrada.
          </span>
        </span>
      </p>

      <div className="flex flex-wrap items-end gap-3 rounded-lg border border-slate-200 bg-white p-4">
        <Campo label="Situação" htmlFor="cd-situacao" className="w-52">
          <Select id="cd-situacao" value={situacao} onChange={(e) => setSituacao(e.target.value as SituacaoSer)}>
            {SITUACOES_SER.map((s) => (
              <option key={s} value={s}>{ROTULO_SITUACAO[s]}</option>
            ))}
          </Select>
        </Campo>

        <Campo label="Tipo" htmlFor="cd-tipo" className="w-36">
          <Select id="cd-tipo" value={tipo} onChange={(e) => setTipo(e.target.value as TipoRecursoSer | '')}>
            <option value="">Todos</option>
            <option value="Consulta">Consulta</option>
            <option value="Exame">Exame</option>
          </Select>
        </Campo>

        <Campo label="Id Solicitação" htmlFor="cd-id" className="w-36">
          <Input id="cd-id" value={idSolicitacao} onChange={(e) => setIdSolicitacao(e.target.value)} />
        </Campo>

        <Campo label="Nome do paciente" htmlFor="cd-nome" className="min-w-56 flex-1">
          <Input id="cd-nome" value={nome} onChange={(e) => setNome(e.target.value)} />
        </Campo>

        <Campo label="CPF" htmlFor="cd-cpf" className="w-40">
          <Input id="cd-cpf" value={cpf} onChange={(e) => setCpf(e.target.value)} />
        </Campo>

        <Campo label="Solicitação de" htmlFor="cd-inicio" className="w-40">
          <Input id="cd-inicio" type="date" value={inicio} onChange={(e) => setInicio(e.target.value)} />
        </Campo>

        <Campo label="até" htmlFor="cd-fim" className="w-40">
          <Input id="cd-fim" type="date" value={fim} onChange={(e) => setFim(e.target.value)} />
        </Campo>

        <Button onClick={() => consultar(1)} disabled={carregando}>
          {carregando ? <Loader2 className="size-4 animate-spin" /> : <Search className="size-4" />}
          Consultar o SER
        </Button>
      </div>

      {erro && <p className="rounded bg-red-50 p-3 text-sm text-red-700">{erro}</p>}

      {resultado && (
        <>
          <div className="flex flex-wrap items-center gap-4 text-sm">
            <span className="text-slate-600">
              {resultado.linhas.length} linha(s) · {resultado.paginas} página(s) ·{' '}
              <strong>{resultado.duracaoMs} ms</strong>
            </span>
            {resultado.bateuNoTeto && (
              // 5 páginas é o corte da tela do SER: existem mais registros que ela não mostra.
              <span className="inline-flex items-center gap-1 text-amber-700">
                <AlertTriangle className="size-4" />
                Bateu no teto de 100 — o SER não pagina além disso. A varredura resolve fatiando por data.
              </span>
            )}
          </div>

          <Tabela
            colunas={colunas}
            dados={resultado.linhas}
            chaveLinha={(l) => l.idSer}
            scrollXFlutuante
            vazio={<div className="py-6 text-center text-sm text-slate-500">O SER não devolveu nenhuma linha para esses filtros.</div>}
          />

          {resultado.paginas > 1 && (
            <div className="flex items-center gap-2">
              <span className="text-sm text-slate-600">Página {pagina} de {resultado.paginas}</span>
              <Button variante="secundaria" disabled={pagina <= 1 || carregando} onClick={() => consultar(pagina - 1)}>
                Anterior
              </Button>
              <Button
                variante="secundaria"
                disabled={pagina >= resultado.paginas || carregando}
                onClick={() => consultar(pagina + 1)}
              >
                Próxima
              </Button>
            </div>
          )}
        </>
      )}

      {historico && (
        <section className="rounded-lg border border-slate-200 bg-white p-4">
          <h3 className="mb-3 text-sm font-semibold">
            Histórico ao vivo da solicitação <span className="font-mono">{historico.idSer}</span>
            <span className="ml-2 text-xs font-normal text-slate-500">
              {historico.eventos.length} eventos · {historico.duracaoMs} ms
            </span>
          </h3>

          <div className="mb-4 grid gap-2 sm:grid-cols-3">
            {Object.entries(historico.paciente).map(([chave, valor]) => (
              <div key={chave}>
                <div className="text-xs text-slate-500">{chave}</div>
                <div className="text-sm">{valor}</div>
              </div>
            ))}
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-xs">
              <thead className="border-b text-left text-slate-500">
                <tr>
                  <th className="py-1 pr-3">Data</th>
                  <th className="py-1 pr-3">Evento</th>
                  <th className="py-1 pr-3">De → Para</th>
                  <th className="py-1 pr-3">Usuário</th>
                  <th className="py-1 pr-3">IP</th>
                  <th className="py-1">Observação</th>
                </tr>
              </thead>
              <tbody>
                {historico.eventos.map((e, i) => (
                  <tr key={`${e.data}-${e.evento}-${i}`} className="border-b last:border-0 align-top">
                    <td className="py-1 pr-3 whitespace-nowrap">{e.data}</td>
                    <td className="py-1 pr-3 font-medium">{e.evento}</td>
                    <td className="py-1 pr-3 whitespace-nowrap">
                      {e.estadoAnterior} → {e.estadoAtual}
                    </td>
                    <td className="py-1 pr-3">{e.usuario}</td>
                    <td className="py-1 pr-3 font-mono">{e.ip}</td>
                    <td className="py-1">{e.observacao}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  );
}
