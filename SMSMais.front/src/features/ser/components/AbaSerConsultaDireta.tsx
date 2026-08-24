import { useState } from 'react';
import { AlertTriangle, History, Loader2, Radio, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Modal } from '@/shared/ui/Modal';
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
/**
 * Mesma régua de cor do detalhe da solicitação: FollowUP em roxo, porque é a tentativa de
 * contato com o paciente — o evento que a regulação procura e o único que não muda a situação.
 */
function corDoEvento(evento: string): string {
  const e = evento.toLowerCase();
  if (e.includes('followup') || e.includes('follow-up')) return 'bg-purple-500';
  if (e.includes('cancel')) return 'bg-red-500';
  if (e.includes('pendenc')) return 'bg-orange-500';
  if (e.includes('solicit')) return 'bg-blue-500';
  return 'bg-slate-400';
}

export function AbaSerConsultaDireta() {
  const [situacao, setSituacao] = useState<SituacaoSer>('EmFila');
  const [tipo, setTipo] = useState<TipoRecursoSer | ''>('');
  const [idSolicitacao, setIdSolicitacao] = useState('');
  const [nome, setNome] = useState('');
  const [cpf, setCpf] = useState('');
  const [inicio, setInicio] = useState('');
  const [fim, setFim] = useState('');
  const [pagina, setPagina] = useState(1);
  const [porExport, setPorExport] = useState(true);
  // Ligado por padrão (07/08/2026): sem a amarração do autocomplete a consulta devolve a fila do
  // ESTADO INTEIRO (PII de outros municípios) em ordem instável. Desligar é diagnóstico, não uso.
  const [filtrarPorSolicitante, setFiltrarPorSolicitante] = useState(true);

  const [carregando, setCarregando] = useState(false);
  const [resultado, setResultado] = useState<ConsultaDiretaResultado | null>(null);
  const [historico, setHistorico] = useState<HistoricoDiretoResultado | null>(null);
  const [carregandoHistorico, setCarregandoHistorico] = useState<string | null>(null);
  const [linhaDoHistorico, setLinhaDoHistorico] = useState<LinhaDiretaSer | null>(null);
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
        porExport,
        filtrarPorSolicitante,
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
      setLinhaDoHistorico(linha);
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
      {/* Só o selo de "ao vivo". O texto longo explicava o que a tela já demonstra, e quem usa
          esta aba é quem opera o SER — não precisa ser lembrado da sessão única toda vez. */}
      <span
        className="inline-flex items-center gap-1.5 rounded-full bg-blue-50 px-2.5 py-1 text-xs font-medium text-blue-800"
        title="Consulta ao vivo no SER — nada é gravado na nossa base."
      >
        <Radio className="size-3.5" />
        ao vivo no SER
      </span>

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

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={porExport}
            onChange={(e) => setPorExport(e.target.checked)}
            className="size-4"
          />
          {/* É o caminho que a varredura usa. A tela de Solicitação trava em 100 por construção,
              então conferir cobertura por ela é impossível — só o export permite CONTAR. */}
          Usar o export (tela de Histórico, até 500 e avisa quando corta)
        </label>

        {porExport && (
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input
              type="checkbox"
              checked={filtrarPorSolicitante}
              onChange={(e) => setFiltrarPorSolicitante(e.target.checked)}
              className="size-4"
            />
            {/* Resolvido em 07/08/2026: o motor amarra o solicitante pela ida-e-volta do
                autocomplete (docs/ser.md §4.3). Desmarcado = fila do ESTADO INTEIRO. */}
            Filtrar por “GESTOR SMS MARICA” (desmarcado = Estado inteiro)
          </label>
        )}

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
              {resultado.linhas.length} linha(s)
              {resultado.fonte === 'ExportHistorico'
                ? ' · export (tela de Histórico)'
                : ` · ${resultado.paginas} página(s) · tela de Solicitação`}{' '}
              · <strong>{resultado.duracaoMs} ms</strong>
            </span>
            {resultado.bateuNoTeto ? (
              <span className="inline-flex items-center gap-1 text-amber-700">
                <AlertTriangle className="size-4" />
                {/* No export o aviso é do PRÓPRIO SER, por escrito. Na tela de Solicitação o corte
                    é mudo e o teto de 100 é a única pista. */}
                {resultado.avisoDoSer ??
                  'Bateu no teto de 100 — o SER não pagina além disso. A varredura resolve fatiando por data.'}
              </span>
            ) : (
              resultado.fonte === 'ExportHistorico' &&
              resultado.linhas.length > 0 && (
                // SEM aviso no export significa cobertura completa daquele recorte. É a única
                // tela do SER em que "não avisou" é informação, e não silêncio.
                <span className="text-green-700">
                  O SER não avisou corte — este recorte veio completo.
                </span>
              )
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

      <Modal
        aberto={historico !== null}
        aoFechar={() => {
          setHistorico(null);
          setLinhaDoHistorico(null);
        }}
        titulo={`Histórico da solicitação ${historico?.idSer ?? ''}`}
        descricao={
          historico
            ? `${linhaDoHistorico?.paciente ?? ''} — ${historico.eventos.length} evento(s) lidos ao vivo no SER em ${historico.duracaoMs} ms`
            : undefined
        }
        largura="lg"
      >
        {historico && (
          <div className="space-y-4">
            <section>
              <h4 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
                Paciente
              </h4>
              <div className="grid gap-2 sm:grid-cols-2">
                {Object.entries(historico.paciente).map(([chave, valor]) => (
                  <div key={chave}>
                    <div className="text-xs text-slate-500">{chave}</div>
                    <div className="text-sm">{valor}</div>
                  </div>
                ))}
              </div>
            </section>

            <section>
              <h4 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
                Trilha de eventos
              </h4>
              {historico.eventos.length === 0 ? (
                <p className="text-sm text-slate-500">O SER não devolveu nenhum evento.</p>
              ) : (
                <ol className="space-y-2">
                  {historico.eventos.map((e, i) => (
                    <li
                      key={`${e.data}-${e.evento}-${i}`}
                      className="rounded-lg border border-slate-200 p-3"
                    >
                      <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                        <span
                          className={`inline-block size-2.5 rounded-full ${corDoEvento(e.evento ?? '')}`}
                        />
                        <span className="text-sm font-semibold">{e.evento}</span>
                        <span className="text-xs text-slate-500">{e.data}</span>
                        {e.estadoAnterior && e.estadoAtual && (
                          <span className="text-xs text-slate-500">
                            {e.estadoAnterior} → {e.estadoAtual}
                          </span>
                        )}
                      </div>
                      <div className="mt-1 flex flex-wrap gap-x-4 text-xs text-slate-500">
                        {e.usuario && <span>por {e.usuario}</span>}
                        {e.centralRegulacao && <span>{e.centralRegulacao}</span>}
                        {e.lotacaoEvento && <span>{e.lotacaoEvento}</span>}
                        {e.ip && <span className="font-mono">IP {e.ip}</span>}
                      </div>
                      {e.observacao && (
                        <p className="mt-2 whitespace-pre-wrap rounded bg-slate-50 p-2 text-sm text-slate-700">
                          {e.observacao}
                        </p>
                      )}
                    </li>
                  ))}
                </ol>
              )}
            </section>
          </div>
        )}
      </Modal>

    </div>
  );
}
