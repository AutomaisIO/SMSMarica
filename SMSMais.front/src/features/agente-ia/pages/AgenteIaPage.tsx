import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { useLocation, useSearchParams } from 'react-router-dom';
import {
  Bot,
  ChevronDown,
  ChevronRight,
  ExternalLink,
  ImageIcon,
  Loader2,
  MessageSquare,
  Send,
  Square,
  Terminal,
  Ticket,
  User,
  WifiOff,
} from 'lucide-react';
import { useAuth, usePermissao, useTemConsulta } from '@/shared/auth/authStore';
import { useContextoTicketPorNumero } from '@/features/tickets/api/queries';
import { obterContextoTicketPorNumero } from '@/features/tickets/api/ticketsApi';
import type { TicketStatus } from '@/features/tickets/types';
import { VisualizadorImagem } from '@/shared/ui/VisualizadorImagem';
import { ehUrlDominioConfiavel } from '@/shared/lib/dominio';
import { useQueryClient } from '@tanstack/react-query';
import {
  cancelarTurno,
  criarSessao,
  criarTurno,
  listarSessoes,
  obterSessao,
  obterTurno,
} from '../api/agenteIaApi';
import { agenteIaKeys } from '../api/queries';
import { ListaSessoes } from '../components/ListaSessoes';
import type { EventoAgente, StatusTurno, TurnoResumo } from '../types';

const INTERVALO_POLL_MS = 700;
// O turno roda no servidor. Falha de rede aqui não significa que ele morreu — significa que
// perdemos a visão dele. Continuamos tentando; o histórico persistido recupera o resto.
const MAX_FALHAS_POLL = 100;

/**
 * Um item = um turno (a pergunta e a resposta dela). Endereçar por `turnId`, e não por
 * índice do array, é o que garante que os eventos caiam sempre na bolha certa mesmo com
 * troca de sessão, reatamento ou remontagem da tela.
 */
type Mensagem = {
  chave: string;
  turnId: string | null;
  prompt: string;
  autor: string | null;
  eventos: EventoAgente[];
  status: StatusTurno;
  erro?: string | null;
  /** Texto chegando ao vivo, antes do bloco fechar. Não é evento persistido. */
  parcial?: string | null;
};

function turnosParaMensagens(turns: TurnoResumo[] = []): Mensagem[] {
  return turns.map((t) => ({
    chave: t.id,
    turnId: t.id,
    prompt: t.prompt,
    autor: t.usuario_nome,
    eventos: t.events ?? [],
    status: t.status,
    erro: t.error,
  }));
}

// Aplica negrito (vermelho em negrito) dentro de um trecho de linha; o resto fica literal.
// Reconhece tanto **texto** quanto *texto* (a alternância tenta ** antes, para não quebrar
// o duplo em dois simples).
function inlineNegrito(texto: string, chave: string): ReactNode[] {
  return texto.split(/(\*\*[^*]+?\*\*|\*[^*]+?\*)/g).map((parte, i) => {
    const negrito = /^\*\*([^*]+?)\*\*$/.exec(parte) ?? /^\*([^*]+?)\*$/.exec(parte);
    return negrito ? (
      <strong key={`${chave}-${i}`} className="font-bold text-red-600">
        {negrito[1]}
      </strong>
    ) : (
      <span key={`${chave}-${i}`}>{parte}</span>
    );
  });
}

// Marcador de imagem inline emitido pelo agente: `<abreimagem url="..." legenda="...">`.
// Ocupa a linha inteira (sozinho). `url` é obrigatório; `legenda` é opcional. As aspas
// podem ser " ou ' ou ausentes (valor sem espaço). Devolve null se a linha não for um
// marcador válido — aí ela segue como texto comum.
function parseMarcadorImagem(linha: string): { url: string; legenda?: string } | null {
  const m = /^<abreimagem\b([^>]*)>$/i.exec(linha.trim());
  if (!m) return null;
  const attrs: Record<string, string> = {};
  const re = /(\w+)\s*=\s*(?:"([^"]*)"|'([^']*)'|(\S+))/g;
  let a: RegExpExecArray | null;
  while ((a = re.exec(m[1])) !== null) {
    attrs[a[1].toLowerCase()] = a[2] ?? a[3] ?? a[4] ?? '';
  }
  if (!attrs.url) return null;
  return { url: attrs.url, legenda: attrs.legenda || undefined };
}

// Cartão de imagem dentro do chat. Só carrega a <img> de verdade se a URL for do domínio
// SMSMarica (ver ehUrlDominioConfiavel) — origem externa NÃO é renderizada como imagem
// (evita carregamento automático de recurso de terceiro); vira um link discreto. Clicar no
// cartão abre o VisualizadorImagem (o mesmo lightbox com zoom / Salvar como / etc.).
function ImagemChat({ url, legenda }: { url: string; legenda?: string }) {
  const [aberto, setAberto] = useState(false);
  const [falhou, setFalhou] = useState(false);

  if (!ehUrlDominioConfiavel(url)) {
    return (
      <a
        href={url}
        target="_blank"
        rel="noopener noreferrer"
        className="my-1 inline-flex max-w-full items-center gap-1.5 rounded-md border border-amber-200 bg-amber-50 px-2.5 py-1.5 text-xs text-amber-800 hover:bg-amber-100"
        title="Imagem fora do domínio SMSMarica — abre em nova aba"
      >
        <ExternalLink className="h-3.5 w-3.5 shrink-0" />
        <span className="truncate">{legenda ?? 'Imagem externa'}</span>
      </a>
    );
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setAberto(true)}
        className="group my-1 flex w-full max-w-xs items-center gap-2 overflow-hidden rounded-lg border border-slate-200 bg-white p-1.5 text-left transition hover:border-red-300 hover:ring-1 hover:ring-red-200"
        title={`${legenda ?? 'Imagem'} — clique para ampliar`}
      >
        {falhou ? (
          <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-md bg-slate-100 text-slate-400">
            <ImageIcon className="h-6 w-6" />
          </span>
        ) : (
          <img
            src={url}
            alt={legenda ?? 'Imagem'}
            loading="lazy"
            onError={() => setFalhou(true)}
            className="h-16 w-16 shrink-0 rounded-md object-cover ring-1 ring-slate-200"
          />
        )}
        <span className="min-w-0 flex-1">
          <span className="block truncate text-sm font-medium text-slate-700">
            {legenda ?? 'Imagem'}
          </span>
          <span className="block text-xs text-slate-400">Clique para ampliar</span>
        </span>
      </button>
      {aberto ? (
        <VisualizadorImagem
          imagens={[{ url, legenda, nomeArquivo: legenda }]}
          aoFechar={() => setAberto(false)}
        />
      ) : null}
    </>
  );
}

// Título por nível (nº de #). O agente usa # / ## como seção principal, ### como subseção
// e ####+ como detalhe — a escala de azul acompanha essa hierarquia: quanto mais alto o
// nível, mais forte o destaque (fundo/borda/cor/tamanho).
function classeTitulo(nivel: number): string {
  if (nivel <= 2) {
    // Seção (# / ##): destaque mais forte.
    return 'mt-3 mb-1.5 rounded-md border border-l-4 border-blue-300 border-l-blue-700 bg-blue-100 px-3 py-2 text-base font-bold text-blue-900';
  }
  if (nivel === 3) {
    // Subseção (###): bloco médio.
    return 'my-1 rounded-md border border-l-4 border-blue-200 border-l-blue-500 bg-blue-50 px-3 py-1.5 text-sm font-semibold text-blue-800';
  }
  // Detalhe (####+): destaque leve, sem preenchimento.
  return 'mt-2 mb-0.5 border-l-2 border-l-blue-400 pl-2 text-sm font-semibold text-blue-700';
}

// Formata o texto do agente para exibição:
//  - linhas iniciadas por # ... ###### (título, exige espaço após os #) viram um bloco
//    destacado na escala de azul, com força proporcional ao nível (ver classeTitulo);
//  - **negrito** vira negrito vermelho;
//  - o restante é literal, com as quebras preservadas (whitespace-pre-wrap).
// Escopo intencionalmente restrito — não é um renderizador de markdown completo.
function TextoFormatado({ texto }: { texto: string }) {
  const linhas = texto.split('\n');
  const blocos: ReactNode[] = [];
  let buffer: string[] = [];

  const descarregar = (chave: string) => {
    if (buffer.length === 0) return;
    const conteudo = buffer.join('\n');
    blocos.push(
      <span key={chave} className="whitespace-pre-wrap">
        {inlineNegrito(conteudo, chave)}
      </span>,
    );
    buffer = [];
  };

  linhas.forEach((linha, i) => {
    // Marcador de imagem (linha inteira): fecha o parágrafo aberto e vira um cartão.
    const imagem = parseMarcadorImagem(linha);
    if (imagem) {
      descarregar(`p-${i}`);
      blocos.push(<ImagemChat key={`img-${i}`} url={imagem.url} legenda={imagem.legenda} />);
      return;
    }
    // Espaço obrigatório após os # evita tratar "#40" (referência de ticket) como título.
    const titulo = /^(#{1,6})\s+(.*)$/.exec(linha);
    if (titulo) {
      descarregar(`p-${i}`);
      blocos.push(
        <div key={`h-${i}`} className={classeTitulo(titulo[1].length)}>
          {inlineNegrito(titulo[2], `h-${i}`)}
        </div>,
      );
    } else {
      buffer.push(linha);
    }
  });
  descarregar('final');

  return <>{blocos}</>;
}

// Rótulo enxuto da ferramenta. Para `Skill`, mostra QUAL skill (`Skill: resolver-ticket`),
// lendo `input.skill` — assim a conversa indica o que está sendo feito sem despejar o corpo
// da skill (o corpo já não vira mais evento de texto no motor — ver ticket #90).
function rotuloFerramenta(evento: Extract<EventoAgente, { type: 'tool_use' }>): string {
  if (evento.name === 'Skill' && evento.input && typeof evento.input === 'object') {
    const skill = (evento.input as { skill?: unknown }).skill;
    if (typeof skill === 'string' && skill) return `Skill: ${skill}`;
  }
  return evento.name;
}

function BlocoFerramenta({ evento }: { evento: Extract<EventoAgente, { type: 'tool_use' }> }) {
  const [aberto, setAberto] = useState(false);
  const Chevron = aberto ? ChevronDown : ChevronRight;
  return (
    <div className="my-1 rounded-md border border-slate-200 bg-slate-50">
      <button
        type="button"
        onClick={() => setAberto((v) => !v)}
        className="flex w-full items-center gap-2 px-3 py-2 text-left font-mono text-xs text-slate-500 hover:text-slate-800"
      >
        <Chevron className="h-3.5 w-3.5 shrink-0" />
        <Terminal className="h-3.5 w-3.5 shrink-0" />
        <span className="truncate">{rotuloFerramenta(evento)}</span>
      </button>
      {aberto && (
        <pre className="max-h-72 overflow-auto whitespace-pre-wrap break-all border-t border-slate-200 px-3 py-2 text-[11px] leading-relaxed text-slate-600">
          {typeof evento.input === 'string' ? evento.input : JSON.stringify(evento.input, null, 2)}
        </pre>
      )}
    </div>
  );
}

function Evento({ evento }: { evento: EventoAgente }) {
  if (evento.type === 'text') {
    return (
      <div className="leading-relaxed text-slate-800">
        <TextoFormatado texto={evento.text} />
      </div>
    );
  }
  if (evento.type === 'tool_use') return <BlocoFerramenta evento={evento} />;
  if (evento.type === 'tool_result' && evento.isError) {
    return (
      <p className="my-1 rounded-md bg-red-50 px-3 py-2 font-mono text-xs text-red-700">
        {typeof evento.content === 'string' ? evento.content : JSON.stringify(evento.content)}
      </p>
    );
  }
  return null;
}

const STATUS_TICKET_LABEL: Record<TicketStatus, string> = {
  Aberto: 'Aberto',
  EmAnalise: 'Em análise',
  Concluido: 'Concluído',
  Negado: 'Negado',
};

const STATUS_TICKET_CLASSE: Record<TicketStatus, string> = {
  Aberto: 'bg-blue-100 text-blue-800',
  EmAnalise: 'bg-amber-100 text-amber-800',
  Concluido: 'bg-green-100 text-green-800',
  Negado: 'bg-red-100 text-red-800',
};

function StatusTicketBadge({ status }: { status: TicketStatus }) {
  return (
    <span
      className={`shrink-0 rounded px-1.5 py-0.5 text-[11px] font-medium ${STATUS_TICKET_CLASSE[status]}`}
    >
      {STATUS_TICKET_LABEL[status]}
    </span>
  );
}

export function AgenteIaPage() {
  // As rotas do painel não são protegidas por permissão (RotaProtegida só checa login),
  // então a checagem tem que estar aqui — senão a URL direta abre a tela e ela dispara
  // chamadas que voltam 403.
  const podeVer = useTemConsulta('AgenteIa');
  const podeAgir = usePermissao('AgenteIa', 'Edicao');
  const usuario = useAuth((s) => s.usuario);
  const qc = useQueryClient();

  const [params, setParams] = useSearchParams();
  const ticketNumero = params.get('ticket') ? Number(params.get('ticket')) : undefined;
  const sessaoDaUrl = params.get('sessao');
  // Vindo da triagem, viaja só a INTENÇÃO de iniciar o trabalho — nunca o conteúdo do
  // ticket. O agente lê o ticket direto do banco (skill resolver-ticket): sempre fresco, e o
  // texto de terceiros (não-confiável) não passa pelo prompt.
  const { state } = useLocation() as {
    state?: { iniciarTicket?: boolean; instrucaoOperador?: string };
  };
  const iniciarTicket = state?.iniciarTicket === true;
  // Instrução opcional que o operador digitou na triagem antes de mandar o agente começar.
  // É texto do OPERADOR (canal confiável, como o chat) — não o conteúdo do ticket.
  const instrucaoOperador = state?.instrucaoOperador?.trim() || undefined;

  const [sessaoId, setSessaoId] = useState<string | null>(sessaoDaUrl);
  const [ticketDaSessao, setTicketDaSessao] = useState<number | null>(null);
  const [mensagens, setMensagens] = useState<Mensagem[]>([]);
  const [entrada, setEntrada] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const [turnoAtivo, setTurnoAtivo] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [semRede, setSemRede] = useState(false);
  const [iniciando, setIniciando] = useState(true);
  const fimRef = useRef<HTMLDivElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  // Enquanto o usuário está "grudado" perto do fim, o autoscroll segue a resposta ao vivo.
  // Se ele arrasta para cima, soltamos; quando volta a encostar no fim, gruda de novo.
  const grudadoNoFimRef = useRef(true);

  // Cada carregamento ganha um número. O polling só escreve na tela se o seu número ainda
  // for o corrente — é assim que trocar de conversa não deixa dois loops brigando.
  const execucaoRef = useRef(0);
  // Marca que a sessão foi aberta a partir da triagem e ainda deve receber o pontapé inicial.
  const contextoPendenteRef = useRef(false);
  // Instrução do operador (se houve) a anexar ao pontapé inicial.
  const instrucaoPendenteRef = useRef<string | undefined>(undefined);

  // Contexto do ticket em trabalho (título/status), sempre fresco enquanto a conversa está
  // aberta — o status pode mudar durante o atendimento. Só dispara quando há ticket.
  const { data: contextoTicket } = useContextoTicketPorNumero(ticketDaSessao);

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
            // A lista mostra "rodando" e a contagem de mensagens — atualiza junto.
            void qc.invalidateQueries({ queryKey: agenteIaKeys.raiz });
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
    [qc],
  );

  const enviarPrompt = useCallback(
    async (prompt: string, sessaoAlvo: string, token: number) => {
      setErro(null);
      const chave = `local-${Date.now()}`;
      setOcupado(true);
      setMensagens((prev) => [
        ...prev,
        {
          chave,
          turnId: null,
          prompt,
          autor: usuario?.nome ?? null,
          eventos: [],
          status: 'running',
        },
      ]);

      try {
        const { turnId } = await criarTurno(sessaoAlvo, prompt);
        setMensagens((prev) =>
          prev.map((m) => (m.chave === chave ? { ...m, turnId } : m)),
        );
        void qc.invalidateQueries({ queryKey: agenteIaKeys.raiz });
        await acompanharTurno(turnId, 0, token);
      } catch (e) {
        const msg = e instanceof Error ? e.message : 'Falha ao executar o turno.';
        setErro(msg);
        setMensagens((prev) =>
          prev.map((m) => (m.chave === chave ? { ...m, status: 'error', erro: msg } : m)),
        );
        setOcupado(false);
      }
    },
    [acompanharTurno, qc, usuario?.nome],
  );

  // ---------------------------------------------------------------- resolução da sessão

  // Qual conversa abrir. Roda na entrada da tela e quando se chega por um ticket; a troca
  // manual de conversa passa por `selecionar`, sem repetir esta resolução.
  useEffect(() => {
    if (!podeVer) return;
    let vivo = true;
    (async () => {
      try {
        if (sessaoDaUrl) {
          setSessaoId(sessaoDaUrl);
          return;
        }
        if (ticketNumero) {
          // Busca só o título do ticket (metadado, não o corpo) para batizar a conversa na
          // lista; se falhar, cria mesmo assim e o nome cai no fallback do motor.
          const ctx = await obterContextoTicketPorNumero(ticketNumero).catch(() => null);
          if (!vivo) return;
          // O motor reusa a sessão aberta daquele ticket, se houver.
          const criada = await criarSessao({ ticketNumero, ticketTitulo: ctx?.titulo });
          if (!vivo) return;
          contextoPendenteRef.current = iniciarTicket;
          instrucaoPendenteRef.current = instrucaoOperador;
          setSessaoId(criada.sessionId);
          return;
        }
        const sessoes = await listarSessoes();
        if (!vivo) return;
        setSessaoId(sessoes[0]?.id ?? null);
        if (!sessoes.length) setIniciando(false);
      } catch (e) {
        if (vivo) {
          setErro(e instanceof Error ? e.message : 'Falha ao abrir o Agente IA.');
          setIniciando(false);
        }
      }
    })();
    return () => {
      vivo = false;
    };
  }, [podeVer, ticketNumero, sessaoDaUrl, iniciarTicket, instrucaoOperador]);

  // Carrega o histórico da conversa ativa e reata um turno que esteja rodando no servidor.
  useEffect(() => {
    if (!podeVer || !sessaoId) return;
    const token = ++execucaoRef.current;
    setIniciando(true);
    setErro(null);
    (async () => {
      try {
        const detalhe = await obterSessao(sessaoId);
        if (execucaoRef.current !== token) return;
        setTicketDaSessao(detalhe.ticket_numero);
        setMensagens(turnosParaMensagens(detalhe.turns));
        setIniciando(false);

        const ultimo = detalhe.turns?.[detalhe.turns.length - 1];
        if (ultimo?.status === 'running') {
          void acompanharTurno(ultimo.id, (ultimo.events ?? []).length, token);
          return;
        }

        // Veio da triagem: dá o pontapé para o operador não ter que digitar nada. Só a
        // referência #N — quem busca o conteúdo (sempre fresco, direto do banco) é o agente,
        // pela skill resolver-ticket.
        if (contextoPendenteRef.current && (detalhe.turns?.length ?? 0) === 0) {
          contextoPendenteRef.current = false;
          const instrucao = instrucaoPendenteRef.current;
          instrucaoPendenteRef.current = undefined;
          void enviarPrompt(
            `Trabalhe no ticket #${detalhe.ticket_numero}. Leia-o direto do banco com a ` +
              `skill resolver-ticket (descrição, comentários internos e anexos), investigue, ` +
              `diga o que encontrou e proponha a solução. Não conclua o ticket.` +
              (instrucao ? `\n\nInstrução do operador para este ticket: ${instrucao}` : ''),
            detalhe.id,
            token,
          );
        }
      } catch (e) {
        if (execucaoRef.current !== token) return;
        setErro(e instanceof Error ? e.message : 'Falha ao abrir a conversa.');
        setIniciando(false);
      }
    })();
  }, [podeVer, sessaoId, acompanharTurno, enviarPrompt]);

  // Detecta se o usuário está perto do fim (margem de 80px cobre o arredondamento do
  // scroll e a barra de digitação). Só então o autoscroll continua "grudado".
  const aoRolar = useCallback(() => {
    const el = containerRef.current;
    if (!el) return;
    const distanciaDoFim = el.scrollHeight - el.scrollTop - el.clientHeight;
    grudadoNoFimRef.current = distanciaDoFim < 80;
  }, []);

  useEffect(() => {
    if (grudadoNoFimRef.current) {
      fimRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [mensagens]);

  if (!podeVer) {
    return (
      <div className="p-6">
        <p className="text-sm text-slate-600">Você não tem acesso ao Agente IA.</p>
      </div>
    );
  }

  // ---------------------------------------------------------------- ações

  function selecionar(id: string) {
    if (id === sessaoId) return;
    // Não interrompe nada: o turno da conversa anterior segue rodando no servidor e a
    // lista continua mostrando "rodando".
    execucaoRef.current += 1;
    // Conversa recém-aberta começa grudada no fim (mostra a última mensagem).
    grudadoNoFimRef.current = true;
    setOcupado(false);
    setTurnoAtivo(null);
    setMensagens([]);
    setSessaoId(id);
    setParams({ sessao: id }, { replace: true });
  }

  async function novaConversa() {
    setErro(null);
    try {
      // Abrir outra conversa NÃO encerra a atual — ela continua na lista, com o histórico.
      const criada = await criarSessao({});
      void qc.invalidateQueries({ queryKey: agenteIaKeys.raiz });
      selecionar(criada.sessionId);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao abrir nova conversa.');
    }
  }

  function aoArquivar(id: string) {
    if (id !== sessaoId) return;
    setSessaoId(null);
    setMensagens([]);
    setTicketDaSessao(null);
    setParams({}, { replace: true });
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
    if (!prompt || ocupado || !sessaoId) return;
    setEntrada('');
    await enviarPrompt(prompt, sessaoId, execucaoRef.current);
  }

  return (
    <div className="flex h-[calc(100vh-8rem)] flex-col p-4">
      <div className="mb-3">
        <h1 className="flex items-center gap-2 text-2xl font-bold text-slate-900">
          <Bot className="h-6 w-6 text-red-600" />
          Agente IA
        </h1>
        <p className="mt-1 text-sm text-slate-600">
          O trabalho roda no servidor — pode fechar esta aba ou trocar de conversa.
        </p>
      </div>

      {/* Faixa de contexto: o que a conversa ativa está tratando — um ticket (nº, título e
          status, sempre frescos) ou uma pergunta avulsa. */}
      {sessaoId && !iniciando && (
        <div className="mb-3 flex items-center gap-2 rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-sm">
          {ticketDaSessao ? (
            <>
              <Ticket className="h-4 w-4 shrink-0 text-red-600" />
              <span className="shrink-0 font-semibold text-slate-800">
                Ticket #{ticketDaSessao}
              </span>
              <span
                className="min-w-0 flex-1 truncate text-slate-600"
                title={contextoTicket?.titulo}
              >
                {contextoTicket?.titulo ?? 'Carregando…'}
              </span>
              {contextoTicket && <StatusTicketBadge status={contextoTicket.status} />}
            </>
          ) : (
            <>
              <MessageSquare className="h-4 w-4 shrink-0 text-slate-500" />
              <span className="font-medium text-slate-700">Pergunta avulsa</span>
            </>
          )}
        </div>
      )}

      <div className="flex min-h-0 flex-1 overflow-hidden rounded-lg border border-slate-200 bg-white">
        <div className="w-72 shrink-0 border-r border-slate-200">
          <ListaSessoes
            sessaoAtivaId={sessaoId}
            onSelecionar={selecionar}
            onNova={() => void novaConversa()}
            onArquivada={aoArquivar}
            podeEditar={podeAgir}
            criando={iniciando}
          />
        </div>

        <div className="flex min-w-0 flex-1 flex-col">
          {semRede && (
            <div className="flex items-center gap-2 border-b border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-600">
              <WifiOff className="h-4 w-4 shrink-0" />
              Sem contato com o servidor — o turno continua rodando lá. Reconectando…
            </div>
          )}

          <div ref={containerRef} onScroll={aoRolar} className="flex-1 overflow-y-auto p-4">
            {iniciando && (
              <div className="flex items-center gap-2 text-sm text-slate-500">
                <Loader2 className="h-4 w-4 animate-spin" />
                Carregando a conversa…
              </div>
            )}

            {!iniciando && !sessaoId && (
              <p className="text-sm text-slate-500">
                Selecione uma conversa à esquerda ou abra uma nova.
              </p>
            )}

            {!iniciando && sessaoId && mensagens.length === 0 && (
              <p className="text-sm text-slate-500">
                Peça um diagnóstico, uma investigação de ticket ou uma correção de código.
              </p>
            )}

            {mensagens.map((m) => (
              <div key={m.chave} className="mb-5">
                <div className="flex gap-3">
                  <User className="mt-1 h-4 w-4 shrink-0 text-amber-500" />
                  {/* Mensagem do usuário destacada (mesmo estilo do aviso de segurança):
                      borda/fundo/cor âmbar deixam fácil rolar e localizar o que foi pedido. */}
                  <div className="min-w-0 flex-1 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-amber-800">
                    {m.autor && (
                      <p className="text-[11px] font-medium uppercase tracking-wide text-amber-700">
                        {m.autor}
                      </p>
                    )}
                    <p className="whitespace-pre-wrap font-medium text-amber-900">{m.prompt}</p>
                  </div>
                </div>

                <div className="mt-3 flex gap-3">
                  <Bot className="mt-1 h-4 w-4 shrink-0 text-red-600" />
                  <div className="min-w-0 flex-1">
                    {m.eventos.map((e, j) => (
                      <Evento key={j} evento={e} />
                    ))}
                    {m.parcial && (
                      <div className="leading-relaxed text-slate-800">
                        <TextoFormatado texto={m.parcial} />
                        <span className="ml-0.5 inline-block h-4 w-1.5 animate-pulse bg-slate-400 align-text-bottom" />
                      </div>
                    )}
                    {m.status === 'running' && !m.parcial && (
                      <div className="flex items-center gap-2 text-sm text-slate-500">
                        <Loader2 className="h-4 w-4 animate-spin" />
                        Trabalhando…
                      </div>
                    )}
                    {m.status === 'cancelled' && <p className="text-sm text-slate-500">Cancelado.</p>}
                    {m.status === 'interrupted' && (
                      <p className="text-sm text-amber-700">
                        {m.erro ?? 'O serviço reiniciou durante este turno.'}
                      </p>
                    )}
                    {m.status === 'error' && (
                      <p className="text-sm text-red-600">{m.erro ?? 'Falha no turno.'}</p>
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
              disabled={ocupado || iniciando || !sessaoId || !podeAgir}
              placeholder="Ex.: investigue por que o laudo do ticket não abre. Olhe o journal do smsmarica-server."
              className="flex-1 resize-none rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-red-500 focus:outline-none disabled:opacity-50"
            />
            {ocupado ? (
              <button
                type="button"
                onClick={cancelar}
                className="flex h-10 items-center gap-2 rounded-md border border-slate-300 px-4 text-sm font-medium text-slate-700 hover:bg-slate-50"
              >
                <Square className="h-4 w-4" />
                Cancelar
              </button>
            ) : (
              <button
                type="submit"
                disabled={iniciando || !entrada.trim() || !sessaoId || !podeAgir}
                className="flex h-10 items-center gap-2 rounded-md bg-red-600 px-4 text-sm font-medium text-white disabled:opacity-50"
              >
                <Send className="h-4 w-4" />
                Enviar
              </button>
            )}
          </form>
        </div>
      </div>
    </div>
  );
}
