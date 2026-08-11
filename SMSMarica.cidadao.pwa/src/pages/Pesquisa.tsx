import { useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import {
  ArrowLeft,
  Building2,
  CalendarDays,
  Check,
  CheckCircle2,
  Lock,
  MessageSquareHeart,
  Send,
} from 'lucide-react';
import { api, type PesquisaPublica } from '@/lib/api';
import { cn } from '@/lib/cn';
import { extrairMensagemDeErro } from '@/lib/httpClient';
import { ErroCard, PrimaryButton, Spinner } from '@/components/ui';

/**
 * TELA DESLIGADA em 11/08/2026 — sem rota, não alcançável.
 *
 * <p>A pesquisa deixou de ser nossa: o questionário passou a ser da AvanteSocial, com um link
 * por unidade, e a resposta precisa ser ANÔNIMA — não pode chegar aqui vinculada a paciente e
 * atendimento. O WhatsApp agora leva a <c>api.smsmarica.online/pesquisa/{token}</c>, que só
 * conta o clique e encaminha para fora.</p>
 *
 * <p>Guardada, e não apagada, a pedido: se um dia a pesquisa voltar a ser nossa, o desenho está
 * aqui. Mas os endpoints que ela chama <b>não existem mais no servidor</b> — religar a rota sem
 * recriá-los dá 404.</p>
 *
 * Pesquisa de satisfação do atendimento.
 *
 * <p>Duas portas para a mesma tela: <c>/pesquisa/:token</c> (link do WhatsApp, sem login — o
 * token abre só a pesquisa, nunca o prontuário) e <c>/atendimentos/:id/pesquisa</c> (botão
 * dentro do histórico, com sessão). Por isso a tela desenha a própria moldura quando é
 * pública: fora do AppShell não existe frame de 460px.</p>
 *
 * <p><b>As perguntas são configuração, não código.</b> Ficam aqui até a secretaria aprovar a
 * redação; depois passam a vir do backend. Mudar texto de pergunta depois de coletar resposta
 * invalida a série histórica, então o texto precisa nascer aprovado.</p>
 */

/**
 * <c>fonte</c> é procedência, não texto de tela: registra de qual pergunta do instrumento
 * oficial cada item veio. Viaja junto quando as perguntas migrarem para o backend — é o que
 * sustenta o resultado diante da secretaria e o que impede alguém de reescrever a redação sem
 * perceber que quebrou a comparabilidade.
 */
type Pergunta =
  | { id: string; dimensao: string; texto: string; tipo: 'opcoes'; opcoes: string[]; fonte: string }
  | { id: string; dimensao: string; texto: string; tipo: 'texto'; dica: string; fonte: string };

/**
 * Escala oficial do PNASS (Caderno PNASS 2015, Ministério da Saúde). Manter esta régua — e não
 * "ótimo/péssimo" — é o que permite comparar o resultado de Maricá com o que a rede pública
 * publica. "Não sei responder" é o equivalente self-service do "Não sabe/Não respondeu" do
 * instrumento entrevistado.
 */
const ESCALA = ['Muito bom', 'Bom', 'Regular', 'Ruim', 'Muito ruim', 'Não sei responder'];

/**
 * Seis perguntas fechadas, cada uma correspondente a uma pergunta literal do questionário de
 * satisfação do PNASS aplicável a Emergência (marcador E/A/I/C no instrumento). A redação foi
 * ajustada de "o(a) senhor(a)" para "você" porque aqui é leitura na tela, não entrevista.
 */
const PERGUNTAS: Pergunta[] = [
  {
    id: 'geral',
    dimensao: 'Avaliação geral',
    texto: 'De uma maneira geral, como você avalia este estabelecimento de saúde?',
    tipo: 'opcoes',
    opcoes: ESCALA,
    fonte: 'PNASS · pergunta 10',
  },
  {
    id: 'espera',
    dimensao: 'Agilidade no atendimento',
    texto: 'Você considera que o tempo de espera para ser atendido foi:',
    tipo: 'opcoes',
    opcoes: ESCALA,
    fonte: 'PNASS · pergunta 3B',
  },
  {
    id: 'equipe',
    dimensao: 'Acolhimento',
    texto: 'Como você avalia o atendimento da equipe de saúde?',
    tipo: 'opcoes',
    opcoes: ESCALA,
    fonte: 'PNASS · pergunta 7',
  },
  {
    id: 'informacoes',
    dimensao: 'Informação',
    texto:
      'Como você avalia as informações e esclarecimentos que teve sobre o seu estado de saúde?',
    tipo: 'opcoes',
    opcoes: ESCALA,
    fonte: 'PNASS · pergunta 9',
  },
  {
    id: 'confianca',
    dimensao: 'Confiança',
    texto: 'Você sentiu segurança e confiança na equipe de saúde durante o atendimento?',
    tipo: 'opcoes',
    opcoes: ['Sim', 'Não', 'Não sei responder'],
    fonte: 'PNASS · pergunta 8',
  },
  {
    id: 'limpeza',
    dimensao: 'Ambiência',
    texto: 'No geral, como você julga a limpeza dos ambientes?',
    tipo: 'opcoes',
    opcoes: ESCALA,
    fonte: 'PNASS · pergunta 4',
  },
  {
    id: 'comentario',
    dimensao: 'Se quiser contar mais',
    texto: 'Quer contar mais alguma coisa sobre o seu atendimento?',
    tipo: 'texto',
    dica: 'Opcional — escreva à vontade.',
    fonte: 'campo aberto (não faz parte do PNASS)',
  },
];

const FECHADAS = PERGUNTAS.filter((p) => p.tipo !== 'texto').length;

/**
 * Prazo para responder, contado da data do atendimento.
 *
 * <p>Não é limitação técnica: memória de detalhe — quanto esperou, quem atendeu, o que foi
 * orientado — não sobrevive a duas semanas. Resposta tardia mede lembrança, não experiência, e
 * contamina a série com ruído que ninguém consegue interpretar depois.</p>
 *
 * <p>O backend precisa repetir esta regra ao validar o token: o que a tela esconde, um link
 * antigo ainda alcançaria.</p>
 */
export const JANELA_DIAS = 15;

/** Dias inteiros entre a data informada e hoje. <c>null</c> quando não dá para saber. */
export function diasDesde(iso: string | null | undefined): number | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return Math.floor((Date.now() - d.getTime()) / 86_400_000);
}

export type ContextoPesquisa = {
  unidade: string | null;
  data: string | null;
  /** Data do atendimento em ISO — é dela que sai a contagem da janela. */
  dataIso?: string | null;
};

/**
 * Token reservado para apresentar a tela sem backend. Só ele lê unidade/data da query string —
 * um token real ignora a query por completo, senão qualquer um forjaria o cabeçalho da pesquisa
 * de outra pessoa. E a tela se identifica como demonstração: sem isso, quem responde acredita
 * ter opinado, e não opinou.
 */
const TOKEN_DEMO = 'demo';

export function Pesquisa() {
  const { token, id: encounterId } = useParams();
  const [query] = useSearchParams();
  const navigate = useNavigate();
  const publica = Boolean(token);
  const demo = token === TOKEN_DEMO;
  /** Token de verdade (não o de demonstração): é o único caso que consulta o servidor. */
  const publicaReal = publica && !demo;

  const [respostas, setRespostas] = useState<Record<string, string>>({});
  const [enviando, setEnviando] = useState(false);
  const [enviada, setEnviada] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  // Só o link público precisa perguntar ao servidor quem é: entrando pelo app, a lista já
  // passou unidade e data pelo state.
  const [doServidor, setDoServidor] = useState<PesquisaPublica | null>(null);
  const [carregando, setCarregando] = useState(publicaReal);

  // Contexto do atendimento. Entrando pelo histórico, a lista já tem unidade e data e passa
  // pelo state — não há por que buscar de novo. Pelo link do WhatsApp virá do token. A tela
  // funciona sem ele: contexto ausente não pode impedir ninguém de responder.
  const contexto: ContextoPesquisa = {
    unidade: doServidor?.unidade ?? (demo ? query.get('u') : null),
    data: doServidor ? formatarDia(doServidor.atendimentoEm) : demo ? query.get('d') : null,
    dataIso: doServidor?.atendimentoEm ?? null,
    ...((useLocation().state as Partial<ContextoPesquisa> | null) ?? {}),
  };

  useEffect(() => {
    if (!publicaReal || !token) return;
    let vivo = true;
    api
      .pesquisa(token)
      .then((d) => {
        if (!vivo) return;
        setDoServidor(d);
        if (d.jaRespondida) setEnviada(true);
      })
      .catch((e) => vivo && setErro(extrairMensagemDeErro(e)))
      .finally(() => vivo && setCarregando(false));
    return () => {
      vivo = false;
    };
  }, [publicaReal, token]);

  const respondidas = useMemo(
    () => PERGUNTAS.filter((p) => p.tipo !== 'texto' && respostas[p.id] !== undefined).length,
    [respostas],
  );
  // A avaliação geral (PNASS 10) é a única obrigatória: é dela que sai o índice da unidade.
  const podeEnviar = respostas.geral !== undefined;

  function responder(id: string, valor: string) {
    setRespostas((r) => ({ ...r, [id]: valor }));
  }

  async function enviar() {
    setEnviando(true);
    setErro(null);
    try {
      // Demonstração não grava: é a tela real, mas sem destino. Ver TOKEN_DEMO.
      if (demo) await new Promise((r) => setTimeout(r, 500));
      else if (token) await api.responderPesquisaPorToken(token, respostas);
      else await api.responderPesquisaDoAtendimento(encounterId!, respostas);
      setEnviada(true);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  // Janela vencida: o botão do histórico já some antes disso, mas quem chegou por link antigo
  // (ou deixou a tela aberta) precisa ver o motivo, não um formulário que não vai ser aceito.
  const dias = diasDesde(contexto.dataIso);
  const expirada = doServidor?.expirada ?? (dias !== null && dias > JANELA_DIAS);

  const conteudo = carregando ? (
    <div className="grid min-h-dvh place-items-center">
      <Spinner />
    </div>
  ) : erro && !doServidor ? (
    <div className="grid min-h-dvh place-items-center px-6">
      <ErroCard mensagem={erro} />
    </div>
  ) : expirada ? (
    <Expirada aoVoltar={() => navigate('/')} />
  ) : enviada ? (
    <Agradecimento publica={publica} aoVoltar={() => navigate('/')} />
  ) : (
    <>
      <Cabecalho contexto={contexto} publica={publica} aoVoltar={() => navigate(-1)} />

      <Progresso feitas={respondidas} total={FECHADAS} />

      <div className="space-y-3 px-4 pb-32 pt-4">
        {PERGUNTAS.map((p, i) => (
          <CartaoPergunta
            key={p.id}
            numero={i + 1}
            pergunta={p}
            valor={respostas[p.id]}
            aoResponder={(v) => responder(p.id, v)}
          />
        ))}

        <p className="flex items-start gap-2 px-1 pt-2 text-[12px] leading-relaxed text-tinta-mute">
          <Lock className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          <span>
            Sua resposta fica ligada a este atendimento para que a gente possa melhorar o serviço
            da unidade. Ela <strong className="font-semibold text-tinta">não interfere</strong> em
            nenhum atendimento seu daqui para frente.
          </span>
        </p>
      </div>

      {/* Barra de envio fixa: em formulário longo no celular, o botão no fim do scroll é o
          principal motivo de abandono. */}
      <div className="fixed inset-x-0 bottom-0 z-20 mx-auto max-w-[460px] border-t border-areia bg-papel/95 p-4 pb-[calc(1rem+env(safe-area-inset-bottom))] backdrop-blur">
        <PrimaryButton onClick={enviar} carregando={enviando} disabled={!podeEnviar}>
          {!enviando && <Send className="h-4 w-4" />}
          Enviar avaliação
        </PrimaryButton>
        {erro && <p className="mt-2 text-center text-[12px] text-marica">{erro}</p>}
        {!podeEnviar && !erro && (
          <p className="mt-2 text-center text-[12px] text-tinta-mute">
            Responda ao menos a primeira pergunta.
          </p>
        )}
      </div>
    </>
  );

  if (!publica) return <div className="animate-rise -mx-4 -mt-4">{conteudo}</div>;

  return (
    <div className="mx-auto flex min-h-dvh max-w-[460px] flex-col bg-papel shadow-2xl">
      <div className="animate-rise">{conteudo}</div>
    </div>
  );
}

/* ---------------------------------- cabeçalho --------------------------------- */

function Cabecalho({
  contexto,
  publica,
  aoVoltar,
}: {
  contexto: ContextoPesquisa;
  publica: boolean;
  aoVoltar: () => void;
}) {
  return (
    <div className="overflow-hidden bg-gradient-to-br from-vinho to-marica text-white">
      <div className="px-5 pb-6 pt-[calc(1.25rem+env(safe-area-inset-top))]">
        {!publica && (
          <button
            type="button"
            onClick={aoVoltar}
            className="mb-3 flex items-center gap-1.5 text-sm font-medium text-white/80 transition active:scale-95"
          >
            <ArrowLeft className="h-4 w-4" /> Voltar
          </button>
        )}

        <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.2em] text-white/85">
          <MessageSquareHeart className="h-3.5 w-3.5" />
          Pesquisa de satisfação
        </div>

        <h1 className="mt-2 font-display text-[26px] font-bold leading-tight">
          Como foi o seu atendimento?
        </h1>
        <p className="mt-1.5 text-sm leading-relaxed text-white/85">
          São 6 perguntas rápidas. Leva menos de um minuto e ajuda a melhorar a saúde de Maricá.
        </p>

        {(contexto.unidade || contexto.data) && (
          <div className="mt-4 flex flex-wrap gap-2">
            {contexto.unidade && <Pastilha icone={Building2} texto={contexto.unidade} />}
            {contexto.data && <Pastilha icone={CalendarDays} texto={contexto.data} />}
          </div>
        )}
      </div>

      {/* Picote — a assinatura do ticket de exame. Sem os furos laterais: aqui o cabeçalho é
          full-bleed, e círculo em borda de tela vira bolinha solta em vez de recorte. */}
      <div className="relative h-6 bg-papel">
        <div className="absolute inset-x-5 top-1/2 -translate-y-1/2 border-t-2 border-dashed border-areia" />
      </div>
    </div>
  );
}

function Pastilha({ icone: Icone, texto }: { icone: typeof Building2; texto: string }) {
  return (
    <span className="inline-flex max-w-full items-center gap-1.5 rounded-full bg-white/15 px-3 py-1 text-xs font-medium text-white ring-1 ring-inset ring-white/20">
      <Icone className="h-3.5 w-3.5 shrink-0" />
      <span className="truncate">{texto}</span>
    </span>
  );
}

function Progresso({ feitas, total }: { feitas: number; total: number }) {
  const pct = Math.round((feitas / total) * 100);
  return (
    <div className="sticky top-0 z-10 border-b border-areia bg-papel/95 px-4 py-2.5 backdrop-blur">
      <div className="flex items-center justify-between text-[11px] font-medium text-tinta-mute">
        <span>
          {feitas} de {total} respondidas
        </span>
        {feitas === total && (
          <span className="flex items-center gap-1 font-semibold text-lagoa">
            <CheckCircle2 className="h-3.5 w-3.5" /> Tudo pronto
          </span>
        )}
      </div>
      <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-areia">
        <div
          className="h-full rounded-full bg-lagoa transition-all duration-500 ease-out"
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}

/* ---------------------------------- perguntas --------------------------------- */

function CartaoPergunta({
  numero,
  pergunta,
  valor,
  aoResponder,
}: {
  numero: number;
  pergunta: Pergunta;
  valor: string | undefined;
  aoResponder: (v: string) => void;
}) {
  const respondida = valor !== undefined && valor !== '';
  return (
    <section
      className={cn(
        'rounded-2xl border bg-white p-4 shadow-carta transition-colors',
        respondida ? 'border-lagoa/30' : 'border-areia',
      )}
    >
      <div className="flex items-start gap-3">
        <span
          className={cn(
            'mt-0.5 grid h-7 w-7 shrink-0 place-items-center rounded-full text-[12px] font-bold transition-colors',
            respondida ? 'bg-lagoa text-white' : 'bg-areia text-tinta-mute',
          )}
        >
          {respondida ? <Check className="h-3.5 w-3.5" /> : numero}
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-[10.5px] font-semibold uppercase tracking-[0.14em] text-tinta-mute">
            {pergunta.dimensao}
          </p>
          <p className="mt-0.5 font-display text-[17px] font-semibold leading-snug text-tinta">
            {pergunta.texto}
          </p>
        </div>
      </div>

      <div className="mt-4">
        {pergunta.tipo === 'opcoes' && (
          <Opcoes opcoes={pergunta.opcoes} valor={valor} aoResponder={aoResponder} />
        )}
        {pergunta.tipo === 'texto' && (
          <textarea
            rows={4}
            value={valor ?? ''}
            onChange={(e) => aoResponder(e.target.value)}
            placeholder={pergunta.dica}
            className="w-full resize-none rounded-2xl border border-areia bg-papel/60 p-3 text-[15px] text-tinta placeholder:text-tinta-mute/70 focus:border-lagoa focus:outline-none"
          />
        )}
      </div>
    </section>
  );
}

function Opcoes({
  opcoes,
  valor,
  aoResponder,
}: {
  opcoes: string[];
  valor: string | undefined;
  aoResponder: (v: string) => void;
}) {
  return (
    <div className="space-y-2">
      {opcoes.map((o) => {
        const sel = valor === o;
        return (
          <button
            key={o}
            type="button"
            onClick={() => aoResponder(o)}
            aria-pressed={sel}
            className={cn(
              'flex min-h-[48px] w-full items-center gap-3 rounded-2xl border px-4 text-left text-[15px] transition active:scale-[.99]',
              sel
                ? 'border-lagoa bg-lagoa-claro font-semibold text-lagoa-escuro'
                : 'border-areia bg-papel/50 text-tinta hover:bg-areia/40',
            )}
          >
            <span
              className={cn(
                'grid h-5 w-5 shrink-0 place-items-center rounded-full border-2 transition',
                sel ? 'border-lagoa bg-lagoa' : 'border-areia bg-white',
              )}
            >
              {sel && <Check className="h-3 w-3 text-white" />}
            </span>
            {o}
          </button>
        );
      })}
    </div>
  );
}

/* ----------------------------------- expirada --------------------------------- */

function Expirada({ aoVoltar }: { aoVoltar: () => void }) {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center px-6 text-center">
      <span className="grid h-20 w-20 place-items-center rounded-3xl bg-areia text-tinta-mute">
        <CalendarDays className="h-10 w-10" />
      </span>
      <h1 className="mt-6 font-display text-2xl font-bold text-tinta">
        O prazo para avaliar já passou
      </h1>
      <p className="mt-2 max-w-xs text-[15px] leading-relaxed text-tinta-mute">
        A avaliação fica disponível por {JANELA_DIAS} dias depois do atendimento. Nos próximos
        atendimentos você recebe o convite de novo — e a sua opinião continua importando.
      </p>
      <button
        type="button"
        onClick={aoVoltar}
        className="mt-8 text-sm font-semibold text-marica transition active:scale-95"
      >
        Ir para o início
      </button>
    </div>
  );
}

/* -------------------------------- agradecimento ------------------------------- */

function Agradecimento({ publica, aoVoltar }: { publica: boolean; aoVoltar: () => void }) {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center px-6 text-center">
      <span className="grid h-20 w-20 place-items-center rounded-3xl bg-lagoa-claro text-lagoa">
        <CheckCircle2 className="h-10 w-10" />
      </span>
      <h1 className="mt-6 font-display text-2xl font-bold text-tinta">Obrigado!</h1>
      <p className="mt-2 max-w-xs text-[15px] leading-relaxed text-tinta-mute">
        Sua avaliação foi registrada e vai direto para a equipe responsável pela unidade. É com
        ela que a gente melhora o atendimento.
      </p>
      {!publica && (
        <button
          type="button"
          onClick={aoVoltar}
          className="mt-8 text-sm font-semibold text-marica transition active:scale-95"
        >
          Voltar para o início
        </button>
      )}
    </div>
  );
}

/** dd/mm/aaaa a partir do ISO devolvido pelo servidor. */
function formatarDia(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString('pt-BR');
}
