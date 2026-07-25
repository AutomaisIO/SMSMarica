import { useCallback, useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import {
  ChevronDown,
  ChevronRight,
  Code2,
  Database,
  Loader2,
  MessageSquarePlus,
  Send,
  Sparkles,
  Square,
  Trash2,
  User,
  WifiOff,
} from 'lucide-react';
import { useAuth, usePermissao, useTemConsulta } from '@/shared/auth/authStore';
import { cn } from '@/shared/lib/cn';
import {
  arquivarSessao,
  cancelarTurno,
  criarSessao,
  criarTurno,
  listarFontes,
  listarSessoes,
  obterSessao,
  obterTurno,
} from '../api/consultaApi';
import { VizRenderer } from '../components/VizRenderer';
import type {
  EventoConsulta,
  FonteConsulta,
  SessaoResumo,
  StatusTurno,
  TurnoResumo,
  VizSpec,
} from '../types';

const INTERVALO_POLL_MS = 700;
const MAX_FALHAS_POLL = 100;
const TOOL_CONSULTAR = 'mcp__dados__consultar_base';
const TOOL_VISUALIZAR = 'mcp__dados__visualizar';

type Mensagem = {
  chave: string;
  turnId: string | null;
  prompt: string;
  eventos: EventoConsulta[];
  status: StatusTurno;
  erro?: string | null;
  parcial?: string | null;
};

function turnosParaMensagens(turns: TurnoResumo[] = []): Mensagem[] {
  return turns.map((t) => ({
    chave: t.id,
    turnId: t.id,
    prompt: t.prompt,
    eventos: t.events ?? [],
    status: t.status,
    erro: t.error,
  }));
}

/** A pergunta é enviada com o contexto RAG anexado pelo backend; na tela mostramos só a
 *  primeira linha (a pergunta de fato), sem o bloco de schema. */
function perguntaVisivel(prompt: string): string {
  const corte = prompt.indexOf('\n---\n');
  return (corte > 0 ? prompt.slice(0, corte) : prompt).trim();
}

function ToolConsulta({ evento }: { evento: Extract<EventoConsulta, { type: 'tool_use' }> }) {
  const [aberto, setAberto] = useState(false);
  const sql = typeof (evento.input as { sql?: unknown })?.sql === 'string'
    ? (evento.input as { sql: string }).sql
    : JSON.stringify(evento.input);
  const Chevron = aberto ? ChevronDown : ChevronRight;
  return (
    <div className="my-1 rounded-md border border-slate-200 bg-slate-50">
      <button
        type="button"
        onClick={() => setAberto((v) => !v)}
        className="flex w-full items-center gap-2 px-3 py-1.5 text-left text-xs text-slate-500 hover:text-slate-800"
      >
        <Chevron className="h-3.5 w-3.5 shrink-0" />
        <Code2 className="h-3.5 w-3.5 shrink-0" />
        <span className="truncate">Consulta ao banco</span>
      </button>
      {aberto && (
        <pre className="max-h-60 overflow-auto whitespace-pre-wrap break-words border-t border-slate-200 px-3 py-2 text-[11px] leading-relaxed text-slate-600">
          {sql}
        </pre>
      )}
    </div>
  );
}

function lerSpec(input: unknown): VizSpec | null {
  const spec = (input as { spec?: unknown })?.spec;
  if (spec && typeof spec === 'object' && 'tipo' in (spec as object)) {
    return spec as VizSpec;
  }
  return null;
}

const MD_COMPONENTS = {
  p: ({ children }: { children?: React.ReactNode }) => <p className="mb-2 last:mb-0">{children}</p>,
  ul: ({ children }: { children?: React.ReactNode }) => (
    <ul className="mb-2 list-disc space-y-0.5 pl-5">{children}</ul>
  ),
  ol: ({ children }: { children?: React.ReactNode }) => (
    <ol className="mb-2 list-decimal space-y-0.5 pl-5">{children}</ol>
  ),
  strong: ({ children }: { children?: React.ReactNode }) => (
    <strong className="font-semibold text-primary-700">{children}</strong>
  ),
  table: ({ children }: { children?: React.ReactNode }) => (
    <div className="overflow-x-auto">
      <table className="my-2 min-w-full border-collapse text-xs">{children}</table>
    </div>
  ),
  th: ({ children }: { children?: React.ReactNode }) => (
    <th className="border border-gray-200 bg-gray-50 px-2 py-1 text-left">{children}</th>
  ),
  td: ({ children }: { children?: React.ReactNode }) => (
    <td className="border border-gray-200 px-2 py-1">{children}</td>
  ),
};

function Markdown({ children }: { children: string }) {
  return (
    <div className="prose prose-sm max-w-none leading-relaxed text-slate-800">
      <ReactMarkdown components={MD_COMPONENTS}>{children}</ReactMarkdown>
    </div>
  );
}

/** Rótulo de atividade (modo padrão) — reflete o que o motor está fazendo, sem revelar conteúdo. */
function atividadeLabel(eventos: EventoConsulta[]): string {
  const ultimoTool = [...eventos].reverse().find((e) => e.type === 'tool_use') as
    | Extract<EventoConsulta, { type: 'tool_use' }>
    | undefined;
  if (ultimoTool?.name === TOOL_CONSULTAR) return 'Consultando a base…';
  if (ultimoTool?.name === TOOL_VISUALIZAR) return 'Preparando a visualização…';
  return 'Pensando…';
}

/** Texto final concreto: o resultado do turno (ou o último bloco de texto). */
function textoFinal(eventos: EventoConsulta[]): string {
  const result = [...eventos].reverse().find((e) => e.type === 'result') as
    | Extract<EventoConsulta, { type: 'result' }>
    | undefined;
  if (result?.text && result.text.trim()) return result.text;
  const ultimoTexto = [...eventos].reverse().find((e) => e.type === 'text' && e.text.trim()) as
    | Extract<EventoConsulta, { type: 'text' }>
    | undefined;
  return ultimoTexto?.text ?? '';
}

/** Modo padrão: esconde o raciocínio. Enquanto roda, só atividade; ao terminar, gráficos/mapas
 *  + a resposta concreta. */
function RespostaConcreta({ m }: { m: Mensagem }) {
  if (m.status === 'running') {
    const passos = m.eventos.filter(
      (e) => e.type === 'tool_use' && (e.name === TOOL_CONSULTAR || e.name === TOOL_VISUALIZAR),
    ).length;
    return (
      <div className="flex items-center gap-2 text-sm text-slate-500">
        <Loader2 className="h-4 w-4 animate-spin" />
        <span>
          {atividadeLabel(m.eventos)}
          {passos > 0 ? ` (${passos} passo${passos > 1 ? 's' : ''})` : ''}
        </span>
      </div>
    );
  }
  const vizs = m.eventos.filter(
    (e): e is Extract<EventoConsulta, { type: 'tool_use' }> =>
      e.type === 'tool_use' && e.name === TOOL_VISUALIZAR,
  );
  const final = textoFinal(m.eventos);
  return (
    <>
      {vizs.map((e, j) => {
        const spec = lerSpec(e.input);
        return spec ? <VizRenderer key={j} spec={spec} /> : null;
      })}
      {final ? <Markdown>{final}</Markdown> : null}
      {!final && vizs.length === 0 && m.status === 'done' ? (
        <p className="text-sm text-slate-500">Sem resposta.</p>
      ) : null}
    </>
  );
}

function Evento({ evento }: { evento: EventoConsulta }) {
  if (evento.type === 'text' && evento.text.trim()) {
    return (
      <div className="prose prose-sm max-w-none leading-relaxed text-slate-800">
        <ReactMarkdown
          components={{
            p: ({ children }) => <p className="mb-2 last:mb-0">{children}</p>,
            ul: ({ children }) => <ul className="mb-2 list-disc space-y-0.5 pl-5">{children}</ul>,
            ol: ({ children }) => <ol className="mb-2 list-decimal space-y-0.5 pl-5">{children}</ol>,
            strong: ({ children }) => <strong className="font-semibold text-primary-700">{children}</strong>,
            table: ({ children }) => (
              <div className="overflow-x-auto">
                <table className="my-2 min-w-full border-collapse text-xs">{children}</table>
              </div>
            ),
            th: ({ children }) => <th className="border border-gray-200 bg-gray-50 px-2 py-1 text-left">{children}</th>,
            td: ({ children }) => <td className="border border-gray-200 px-2 py-1">{children}</td>,
          }}
        >
          {evento.text}
        </ReactMarkdown>
      </div>
    );
  }
  if (evento.type === 'tool_use') {
    if (evento.name === TOOL_CONSULTAR) return <ToolConsulta evento={evento} />;
    if (evento.name === TOOL_VISUALIZAR) {
      const spec = lerSpec(evento.input);
      return spec ? <VizRenderer spec={spec} /> : null;
    }
    return null; // ToolSearch e afins: ruído, não mostra
  }
  if (evento.type === 'tool_result' && evento.isError) {
    return (
      <p className="my-1 rounded-md bg-red-50 px-3 py-2 text-xs text-red-700">
        {typeof evento.content === 'string' ? evento.content : JSON.stringify(evento.content)}
      </p>
    );
  }
  return null;
}

export function ConsultaInteligentePage() {
  const podeVer = useTemConsulta('Inteligencia');
  const podeDevMode = usePermissao('InteligenciaConsultaDev', 'Consulta');
  const usuario = useAuth((s) => s.usuario);

  const [fontes, setFontes] = useState<FonteConsulta[]>([]);
  const [sessoes, setSessoes] = useState<SessaoResumo[]>([]);
  const [sessaoId, setSessaoId] = useState<string | null>(null);
  const [fonteIdAtiva, setFonteIdAtiva] = useState<string | null>(null);
  const [mensagens, setMensagens] = useState<Mensagem[]>([]);
  const [entrada, setEntrada] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const [turnoAtivo, setTurnoAtivo] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [semRede, setSemRede] = useState(false);
  const [iniciando, setIniciando] = useState(false);
  const [novaBase, setNovaBase] = useState<string>('');
  const [modoDev, setModoDev] = useState(false);

  const fimRef = useRef<HTMLDivElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const grudadoNoFimRef = useRef(true);
  const execucaoRef = useRef(0);

  const fonteIdPorSlug = useCallback(
    (slug: string | null | undefined) => fontes.find((f) => f.slug === slug)?.id ?? null,
    [fontes],
  );
  const nomeBase = useCallback(
    (slug: string | null | undefined) => fontes.find((f) => f.slug === slug)?.nome ?? slug ?? '—',
    [fontes],
  );

  const recarregarSessoes = useCallback(async () => {
    try {
      setSessoes(await listarSessoes());
    } catch {
      /* silencioso: a lista não é crítica para conversar */
    }
  }, []);

  // Carga inicial: fontes + sessões.
  useEffect(() => {
    if (!podeVer) return;
    (async () => {
      try {
        const fs = await listarFontes();
        setFontes(fs);
        setNovaBase((b) => b || fs[0]?.id || '');
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar as bases.');
      }
      void recarregarSessoes();
    })();
  }, [podeVer, recarregarSessoes]);

  const acompanharTurno = useCallback(
    async (turnId: string, cursorInicial: number, token: number) => {
      let cursor = cursorInicial;
      let falhas = 0;
      setTurnoAtivo(turnId);
      setOcupado(true);
      try {
        for (;;) {
          if (execucaoRef.current !== token) return;
          let turno;
          try {
            turno = await obterTurno(turnId, cursor);
            falhas = 0;
            setSemRede(false);
          } catch (e) {
            falhas += 1;
            setSemRede(true);
            if (falhas >= MAX_FALHAS_POLL) throw e;
            await new Promise((r) => setTimeout(r, INTERVALO_POLL_MS));
            continue;
          }
          cursor = turno.cursor;
          setMensagens((prev) =>
            prev.map((m) =>
              m.turnId === turnId
                ? {
                    ...m,
                    eventos: turno.events?.length ? [...m.eventos, ...turno.events] : m.eventos,
                    status: turno.status,
                    erro: turno.error,
                    parcial: turno.partial,
                  }
                : m,
            ),
          );
          if (turno.status !== 'running') {
            void recarregarSessoes();
            return;
          }
          await new Promise((r) => setTimeout(r, INTERVALO_POLL_MS));
        }
      } finally {
        if (execucaoRef.current === token) {
          setOcupado(false);
          setTurnoAtivo(null);
        }
      }
    },
    [recarregarSessoes],
  );

  const enviarPrompt = useCallback(
    async (prompt: string, sessaoAlvo: string, fonteId: string, token: number) => {
      setErro(null);
      const chave = `local-${Date.now()}`;
      setOcupado(true);
      setMensagens((prev) => [
        ...prev,
        { chave, turnId: null, prompt, eventos: [], status: 'running' },
      ]);
      try {
        const { turnId } = await criarTurno(sessaoAlvo, fonteId, prompt, modoDev);
        setMensagens((prev) => prev.map((m) => (m.chave === chave ? { ...m, turnId } : m)));
        await acompanharTurno(turnId, 0, token);
      } catch (e) {
        const msg = e instanceof Error ? e.message : 'Falha ao enviar a pergunta.';
        setErro(msg);
        setMensagens((prev) =>
          prev.map((m) => (m.chave === chave ? { ...m, status: 'error', erro: msg } : m)),
        );
        setOcupado(false);
      }
    },
    [acompanharTurno, modoDev],
  );

  // Carrega o histórico ao trocar de sessão e reata turno rodando.
  useEffect(() => {
    if (!podeVer || !sessaoId) return;
    const token = ++execucaoRef.current;
    setIniciando(true);
    setErro(null);
    (async () => {
      try {
        const detalhe = await obterSessao(sessaoId);
        if (execucaoRef.current !== token) return;
        setFonteIdAtiva(fonteIdPorSlug(detalhe.base_slug));
        setMensagens(turnosParaMensagens(detalhe.turns));
        setIniciando(false);
        const ultimo = detalhe.turns?.[detalhe.turns.length - 1];
        if (ultimo?.status === 'running') {
          void acompanharTurno(ultimo.id, (ultimo.events ?? []).length, token);
        }
      } catch (e) {
        if (execucaoRef.current !== token) return;
        setErro(e instanceof Error ? e.message : 'Falha ao abrir a conversa.');
        setIniciando(false);
      }
    })();
  }, [podeVer, sessaoId, acompanharTurno, fonteIdPorSlug]);

  const aoRolar = useCallback(() => {
    const el = containerRef.current;
    if (!el) return;
    grudadoNoFimRef.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
  }, []);

  useEffect(() => {
    if (grudadoNoFimRef.current) fimRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [mensagens]);

  if (!podeVer) {
    return <div className="p-6 text-sm text-slate-600">Você não tem acesso à Consulta Inteligente.</div>;
  }

  function selecionar(id: string) {
    if (id === sessaoId) return;
    execucaoRef.current += 1;
    grudadoNoFimRef.current = true;
    setOcupado(false);
    setTurnoAtivo(null);
    setMensagens([]);
    setSessaoId(id);
  }

  async function novaConversa() {
    setErro(null);
    if (!novaBase) {
      setErro('Escolha uma base para começar.');
      return;
    }
    try {
      const criada = await criarSessao(novaBase);
      await recarregarSessoes();
      execucaoRef.current += 1;
      grudadoNoFimRef.current = true;
      setMensagens([]);
      setFonteIdAtiva(novaBase);
      setSessaoId(criada.sessionId);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao abrir a conversa.');
    }
  }

  async function aoArquivar(id: string) {
    try {
      await arquivarSessao(id);
      if (id === sessaoId) {
        setSessaoId(null);
        setMensagens([]);
      }
      await recarregarSessoes();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao arquivar.');
    }
  }

  async function cancelar() {
    if (!turnoAtivo) return;
    try {
      await cancelarTurno(turnoAtivo);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao cancelar.');
    }
  }

  async function enviar(ev: React.FormEvent) {
    ev.preventDefault();
    const prompt = entrada.trim();
    if (!prompt || ocupado || !sessaoId || !fonteIdAtiva) return;
    setEntrada('');
    await enviarPrompt(prompt, sessaoId, fonteIdAtiva, execucaoRef.current);
  }

  return (
    <div className="flex h-[calc(100vh-8rem)] flex-col p-4">
      <div className="mb-3 flex items-start justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-bold text-slate-900">
            <Sparkles className="h-6 w-6 text-primary-600" />
            Consulta Inteligente
          </h1>
          <p className="mt-1 text-sm text-slate-600">
            Pergunte sobre os dados em linguagem natural. Cada conversa consulta uma base — com
            segurança: somente leitura, sem acesso ao sistema.
          </p>
        </div>
        {podeDevMode && (
          <label className="mt-1 flex shrink-0 cursor-pointer items-center gap-2 rounded-md border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs font-medium text-slate-600">
            <input
              type="checkbox"
              checked={modoDev}
              onChange={(e) => setModoDev(e.target.checked)}
              className="h-3.5 w-3.5 accent-primary-600"
            />
            Modo desenvolvedor
          </label>
        )}
      </div>

      <div className="flex min-h-0 flex-1 overflow-hidden rounded-lg border border-slate-200 bg-white">
        {/* Lista de conversas */}
        <div className="flex w-72 shrink-0 flex-col border-r border-slate-200">
          <div className="space-y-2 border-b border-slate-200 p-3">
            <select
              value={novaBase}
              onChange={(e) => setNovaBase(e.target.value)}
              className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm text-slate-700 focus:border-primary-500 focus:outline-none"
            >
              {fontes.length === 0 && <option value="">Nenhuma base ativa</option>}
              {fontes.map((f) => (
                <option key={f.id} value={f.id}>{f.nome}</option>
              ))}
            </select>
            <button
              type="button"
              onClick={() => void novaConversa()}
              disabled={!novaBase}
              className="flex w-full items-center justify-center gap-2 rounded-md bg-primary-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50"
            >
              <MessageSquarePlus className="h-4 w-4" />
              Nova consulta
            </button>
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto">
            {sessoes.length === 0 ? (
              <p className="p-3 text-xs text-slate-400">Nenhuma conversa ainda.</p>
            ) : (
              sessoes.map((s) => (
                <button
                  key={s.id}
                  type="button"
                  onClick={() => selecionar(s.id)}
                  className={cn(
                    'group flex w-full items-start gap-2 border-b border-slate-100 px-3 py-2 text-left hover:bg-slate-50',
                    s.id === sessaoId && 'bg-primary-50',
                  )}
                >
                  <Database className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm text-slate-800">
                      {s.title || 'Nova conversa'}
                    </span>
                    <span className="block truncate text-[11px] text-slate-400">
                      {nomeBase(s.base_slug)}
                      {s.running && <span className="ml-1 text-primary-600">• rodando</span>}
                    </span>
                  </span>
                  <span
                    role="button"
                    tabIndex={0}
                    onClick={(e) => {
                      e.stopPropagation();
                      void aoArquivar(s.id);
                    }}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        e.stopPropagation();
                        void aoArquivar(s.id);
                      }
                    }}
                    className="shrink-0 rounded p-1 text-slate-300 opacity-0 hover:bg-red-50 hover:text-red-600 group-hover:opacity-100"
                    title="Arquivar"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </span>
                </button>
              ))
            )}
          </div>
        </div>

        {/* Conversa */}
        <div className="flex min-w-0 flex-1 flex-col">
          {semRede && (
            <div className="flex items-center gap-2 border-b border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-600">
              <WifiOff className="h-4 w-4 shrink-0" />
              Sem contato com o servidor — a consulta continua rodando lá. Reconectando…
            </div>
          )}

          <div ref={containerRef} onScroll={aoRolar} className="flex-1 overflow-y-auto p-4">
            {iniciando && (
              <div className="flex items-center gap-2 text-sm text-slate-500">
                <Loader2 className="h-4 w-4 animate-spin" /> Carregando…
              </div>
            )}
            {!iniciando && !sessaoId && (
              <p className="text-sm text-slate-500">
                Escolha uma base e clique em <strong>Nova consulta</strong> para começar.
              </p>
            )}
            {!iniciando && sessaoId && mensagens.length === 0 && (
              <p className="text-sm text-slate-500">
                Pergunte algo sobre os dados. Ex.: "quantos atendimentos hoje?", "top 10 bairros
                por nº de pacientes num gráfico de barras".
              </p>
            )}

            {mensagens.map((m) => (
              <div key={m.chave} className="mb-5">
                <div className="flex gap-3">
                  <User className="mt-1 h-4 w-4 shrink-0 text-primary-500" />
                  <div className="min-w-0 flex-1 rounded-md border border-primary-100 bg-primary-50 px-3 py-2">
                    <p className="whitespace-pre-wrap font-medium text-primary-900">
                      {perguntaVisivel(m.prompt)}
                    </p>
                  </div>
                </div>
                <div className="mt-3 flex gap-3">
                  <Sparkles className="mt-1 h-4 w-4 shrink-0 text-primary-600" />
                  <div className="min-w-0 flex-1">
                    {modoDev ? (
                      <>
                        {m.eventos.map((e, j) => (
                          <Evento key={j} evento={e} />
                        ))}
                        {m.parcial && (
                          <div className="leading-relaxed text-slate-800">
                            <ReactMarkdown>{m.parcial}</ReactMarkdown>
                            <span className="ml-0.5 inline-block h-4 w-1.5 animate-pulse bg-slate-400 align-text-bottom" />
                          </div>
                        )}
                        {m.status === 'running' && !m.parcial && (
                          <div className="flex items-center gap-2 text-sm text-slate-500">
                            <Loader2 className="h-4 w-4 animate-spin" /> Trabalhando…
                          </div>
                        )}
                      </>
                    ) : (
                      <RespostaConcreta m={m} />
                    )}
                    {m.status === 'cancelled' && <p className="text-sm text-slate-500">Cancelado.</p>}
                    {m.status === 'interrupted' && (
                      <p className="text-sm text-amber-700">{m.erro ?? 'O serviço reiniciou.'}</p>
                    )}
                    {m.status === 'error' && (
                      <p className="text-sm text-red-600">{m.erro ?? 'Falha na consulta.'}</p>
                    )}
                  </div>
                </div>
              </div>
            ))}
            <div ref={fimRef} />
          </div>

          {erro && <p className="px-4 pb-1 text-sm text-red-600">{erro}</p>}

          <form onSubmit={enviar} className="flex items-end gap-2 border-t border-slate-200 p-3">
            <textarea
              value={entrada}
              onChange={(e) => setEntrada(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                  e.preventDefault();
                  void enviar(e);
                }
              }}
              rows={2}
              disabled={ocupado || iniciando || !sessaoId}
              placeholder={sessaoId ? 'Pergunte sobre os dados…' : 'Abra uma nova consulta para começar.'}
              className="flex-1 resize-none rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-primary-500 focus:outline-none disabled:opacity-50"
            />
            {ocupado ? (
              <button
                type="button"
                onClick={cancelar}
                className="flex h-10 items-center gap-2 rounded-md border border-slate-300 px-4 text-sm font-medium text-slate-700 hover:bg-slate-50"
              >
                <Square className="h-4 w-4" /> Parar
              </button>
            ) : (
              <button
                type="submit"
                disabled={iniciando || !entrada.trim() || !sessaoId}
                className="flex h-10 items-center gap-2 rounded-md bg-primary-600 px-4 text-sm font-medium text-white disabled:opacity-50"
              >
                <Send className="h-4 w-4" /> Enviar
              </button>
            )}
          </form>
        </div>
      </div>
      <p className="mt-1 text-[11px] text-slate-400">
        {usuario?.nome ? `Suas conversas, ${usuario.nome}. ` : ''}
        Somente leitura, restrito à base escolhida — sem acesso a arquivos, sistema ou código.
      </p>
    </div>
  );
}
