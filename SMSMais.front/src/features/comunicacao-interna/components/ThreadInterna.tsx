import { Fragment, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeft, Bell, BellOff, Loader2, Users } from 'lucide-react';
import { useAuth, usePermissao } from '@/shared/auth/authStore';
import { cn } from '@/shared/lib/cn';
import {
  useConversaInterna,
  useConversasInternas,
  useMarcarLidaInterna,
  useMensagensInternas,
  useSilenciarConversaInterna,
} from '@/features/comunicacao-interna/api/queries';
import { chaveDia, diaFmt } from '@/features/comunicacao-interna/lib/formatar';
import { useInterno, useOnline } from '@/features/comunicacao-interna/store/internoStore';
import { AvatarPessoa, AvatarUnidade, PontoOnline } from '@/features/comunicacao-interna/components/Avatar';
import { BolhaMensagemInterna } from '@/features/comunicacao-interna/components/BolhaMensagemInterna';
import { ComposerInterno } from '@/features/comunicacao-interna/components/ComposerInterno';
import type { MensagemInterna, ParticipanteInterno } from '@/features/comunicacao-interna/types';

/** Quantas páginas a busca pode puxar para trás atrás da mensagem pedida antes de desistir. */
const MAX_PAGINAS_ALVO = 20;

type Props = {
  conversaId: string;
  /** Voltar para a lista (tela estreita, onde lista e conversa não cabem lado a lado). */
  aoVoltar?: () => void;
};

/** A conversa aberta: cabeçalho, mensagens (rolagem infinita para cima) e caixa de escrever. */
export function ThreadInterna({ conversaId, aoVoltar }: Props) {
  const meuId = useAuth((s) => s.usuario?.id ?? '');
  const podeEnviar = usePermissao('ComunicacaoInterna', 'Inclusao');
  const { data: detalhe } = useConversaInterna(conversaId);
  const { data: lista } = useConversasInternas();
  const msgs = useMensagensInternas(conversaId);
  const marcarLida = useMarcarLidaInterna();
  const silenciar = useSilenciarConversaInterna();
  const mensagemAlvoId = useInterno((s) => s.mensagemAlvoId);
  const digitando = useInterno((s) => s.digitando[conversaId]);
  const presenca = useInterno((s) => s.presenca);
  const [participantesAbertos, setParticipantesAbertos] = useState(false);
  const [destacada, setDestacada] = useState<string | null>(null);

  const item = lista?.find((c) => c.id === conversaId) ?? detalhe?.conversa;
  const ehDireta = item?.tipo === 'Direta';
  const outroOnline = useOnline(item?.outroUsuarioId, item?.outroOnline ?? false);

  // Páginas: a 0 é a mais recente; para a tela, da mais antiga para a mais nova, sem repetir id.
  const mensagens = useMemo<MensagemInterna[]>(() => {
    const vistos = new Set<string>();
    const out: MensagemInterna[] = [];
    for (const p of [...(msgs.data?.pages ?? [])].reverse()) {
      for (const m of p.mensagens) {
        if (vistos.has(m.id)) continue;
        vistos.add(m.id);
        out.push(m);
      }
    }
    return out;
  }, [msgs.data]);

  const ultima = mensagens[mensagens.length - 1];
  const primeiraId = mensagens[0]?.id;

  // ---- Rolagem ----------------------------------------------------------------------------
  const rolagemRef = useRef<HTMLDivElement>(null);
  const alturaAntesRef = useRef<number | null>(null);
  const ultimaVistaRef = useRef<string | null>(null);

  // Página antiga chegou em cima: mantém a mesma mensagem na tela (compensa a altura nova).
  useLayoutEffect(() => {
    const el = rolagemRef.current;
    if (!el || alturaAntesRef.current == null) return;
    el.scrollTop += el.scrollHeight - alturaAntesRef.current;
    alturaAntesRef.current = null;
  }, [primeiraId]);

  // Mensagem nova embaixo: desce se a pessoa já estava no fim (ou se a mensagem é minha).
  useLayoutEffect(() => {
    const el = rolagemRef.current;
    if (!el || !ultima) return;
    const primeiraVez = ultimaVistaRef.current === null;
    if (ultimaVistaRef.current === ultima.id) return;
    ultimaVistaRef.current = ultima.id;
    const perto = el.scrollHeight - el.scrollTop - el.clientHeight < 160;
    if (primeiraVez || perto || ultima.autorId === meuId) el.scrollTop = el.scrollHeight;
  }, [ultima, meuId]);

  useEffect(() => {
    ultimaVistaRef.current = null;
  }, [conversaId]);

  function aoRolar() {
    const el = rolagemRef.current;
    if (!el || el.scrollTop > 80 || !msgs.hasNextPage || msgs.isFetchingNextPage) return;
    alturaAntesRef.current = el.scrollHeight;
    void msgs.fetchNextPage();
  }

  // Busca mandou mostrar uma mensagem: puxa páginas para trás até achar, rola e destaca.
  useEffect(() => {
    if (!mensagemAlvoId || !msgs.data) return;
    const el = document.getElementById(`msg-interna-${mensagemAlvoId}`);
    if (el) {
      el.scrollIntoView({ block: 'center' });
      setDestacada(mensagemAlvoId);
      useInterno.getState().limparMensagemAlvo();
      const t = setTimeout(() => setDestacada(null), 2_500);
      return () => clearTimeout(t);
    }
    if (msgs.hasNextPage && !msgs.isFetchingNextPage && msgs.data.pages.length < MAX_PAGINAS_ALVO) {
      void msgs.fetchNextPage();
    } else if (!msgs.hasNextPage) {
      useInterno.getState().limparMensagemAlvo();
    }
  }, [mensagemAlvoId, msgs.data, msgs.hasNextPage, msgs.isFetchingNextPage, msgs.fetchNextPage]);

  // ---- Leitura ----------------------------------------------------------------------------
  // Marca como lida quando a conversa está NA TELA: ao abrir com não-lidas e a cada mensagem
  // nova dos outros. Aba escondida não conta — marca quando a pessoa voltar.
  const marcadaRef = useRef<string | null>(null);
  const naoLidas = item?.naoLidas ?? 0;
  useEffect(() => {
    function talvezMarcar() {
      if (document.hidden || !ultima) return;
      const chave = `${conversaId}:${ultima.id}`;
      if (marcadaRef.current === chave) return;
      if (ultima.autorId === meuId && naoLidas === 0) return;
      marcadaRef.current = chave;
      marcarLida.mutate(conversaId);
    }
    talvezMarcar();
    document.addEventListener('visibilitychange', talvezMarcar);
    return () => document.removeEventListener('visibilitychange', talvezMarcar);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversaId, ultima?.id, naoLidas, meuId]);

  // ---- "Digitando…" expira sozinho --------------------------------------------------------
  const [, forcar] = useState(0);
  const digitandoAtivo = digitando && digitando.ate > Date.now();
  useEffect(() => {
    if (!digitando) return;
    const resta = digitando.ate - Date.now();
    if (resta <= 0) return;
    const t = setTimeout(() => forcar((n) => n + 1), resta + 50);
    return () => clearTimeout(t);
  }, [digitando]);

  const outroLidaAte = detalhe?.outroLidaAte ?? null;
  const participantes = detalhe?.participantes ?? [];

  return (
    <div className="flex h-full min-h-0 flex-col bg-[#faf7f4]">
      {/* Cabeçalho */}
      <div className="relative flex items-center gap-2.5 border-b border-gray-200 bg-white px-3 py-2">
        {aoVoltar && (
          <button
            type="button"
            onClick={aoVoltar}
            className="rounded-md p-1 text-gray-500 hover:bg-gray-100 sm:hidden"
            aria-label="Voltar para a lista"
          >
            <ArrowLeft className="h-4 w-4" />
          </button>
        )}
        {item ? (
          ehDireta && item.outroUsuarioId ? (
            <AvatarPessoa id={item.outroUsuarioId} nome={item.titulo} online={outroOnline} />
          ) : (
            <AvatarUnidade equipe={item.unidadeAId === item.unidadeBId} />
          )
        ) : null}
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold text-gray-900">{item?.titulo ?? 'Carregando…'}</p>
          {digitandoAtivo ? (
            <p className="truncate text-xs italic text-orange-600">{digitando.nome} está digitando…</p>
          ) : ehDireta ? (
            <p className="truncate text-xs text-gray-500">
              {outroOnline ? 'online agora' : 'offline'}
              {item?.subtitulo ? ` · ${item.subtitulo}` : ''}
            </p>
          ) : (
            <button
              type="button"
              onClick={() => setParticipantesAbertos((v) => !v)}
              className="flex max-w-full items-center gap-1 truncate text-xs text-gray-500 hover:text-orange-700"
            >
              <Users className="h-3 w-3 shrink-0" />
              {participantes.length > 0
                ? `${participantes.length} participantes · ${contarOnline(participantes, presenca)} online`
                : (item?.subtitulo ?? 'Todos das unidades')}
            </button>
          )}
        </div>
        {item && (
          <button
            type="button"
            onClick={() => silenciar.mutate({ id: conversaId, silenciada: !item.silenciada })}
            disabled={silenciar.isPending}
            className={cn(
              'rounded-md p-1.5 hover:bg-gray-100',
              item.silenciada ? 'text-gray-400' : 'text-gray-500',
            )}
            title={
              item.silenciada
                ? 'Conversa silenciada: não toca som nem abre sozinha (nem conta no balão). Clique para reativar.'
                : 'Silenciar esta conversa'
            }
            aria-label={item.silenciada ? 'Reativar avisos desta conversa' : 'Silenciar esta conversa'}
          >
            {item.silenciada ? <BellOff className="h-4 w-4" /> : <Bell className="h-4 w-4" />}
          </button>
        )}

        {participantesAbertos && !ehDireta && (
          <ListaParticipantes participantes={participantes} aoFechar={() => setParticipantesAbertos(false)} />
        )}
      </div>

      {/* Mensagens */}
      <div ref={rolagemRef} onScroll={aoRolar} className="min-h-0 flex-1 overflow-y-auto py-2">
        {msgs.isFetchingNextPage && (
          <p className="flex justify-center py-2 text-gray-400">
            <Loader2 className="h-4 w-4 animate-spin" />
          </p>
        )}
        {msgs.isLoading ? (
          <p className="flex justify-center py-10 text-gray-400">
            <Loader2 className="h-5 w-5 animate-spin" />
          </p>
        ) : msgs.isError ? (
          <p className="px-4 py-10 text-center text-sm text-red-600">Não foi possível carregar as mensagens.</p>
        ) : mensagens.length === 0 ? (
          <p className="px-6 py-10 text-center text-sm text-gray-400">
            Nenhuma mensagem ainda. Escreva a primeira — fica tudo registrado aqui.
          </p>
        ) : (
          mensagens.map((m, i) => {
            const novoDia = i === 0 || chaveDia(mensagens[i - 1].enviadaEm) !== chaveDia(m.enviadaEm);
            const minha = m.autorId === meuId;
            return (
              <Fragment key={m.id}>
                {novoDia && (
                  <div className="my-2 flex justify-center">
                    <span className="rounded-full bg-white px-3 py-0.5 text-[11px] font-medium text-gray-500 shadow-sm ring-1 ring-gray-200">
                      {diaFmt(m.enviadaEm)}
                    </span>
                  </div>
                )}
                <BolhaMensagemInterna
                  m={m}
                  minha={minha}
                  mostrarAutor={!ehDireta}
                  lida={ehDireta && minha ? Boolean(outroLidaAte && outroLidaAte >= m.enviadaEm) : undefined}
                  destacada={destacada === m.id}
                />
              </Fragment>
            );
          })
        )}
      </div>

      {podeEnviar ? (
        <ComposerInterno conversaId={conversaId} />
      ) : (
        <p className="border-t border-gray-200 bg-white px-3 py-2.5 text-center text-xs text-gray-500">
          Seu perfil só permite ler a Comunicação interna.
        </p>
      )}
    </div>
  );
}

function contarOnline(ps: ParticipanteInterno[], presenca: Record<string, boolean>): number {
  return ps.filter((p) => presenca[p.usuarioId] ?? p.online).length;
}

function ListaParticipantes({ participantes, aoFechar }: { participantes: ParticipanteInterno[]; aoFechar: () => void }) {
  const presenca = useInterno((s) => s.presenca);
  const ordenados = [...participantes].sort((a, b) => {
    const oa = presenca[a.usuarioId] ?? a.online;
    const ob = presenca[b.usuarioId] ?? b.online;
    return oa === ob ? a.nome.localeCompare(b.nome, 'pt-BR') : oa ? -1 : 1;
  });
  return (
    <div
      className="absolute left-3 right-3 top-full z-10 mt-1 max-h-72 overflow-y-auto rounded-lg border border-gray-200 bg-white py-1 shadow-lg"
      onMouseLeave={aoFechar}
    >
      <p className="px-3 py-1 text-[11px] font-semibold uppercase tracking-wide text-gray-400">
        Participantes — todos das unidades
      </p>
      {ordenados.map((p) => (
        <div key={p.usuarioId} className="flex items-center gap-2 px-3 py-1.5 text-sm">
          <PontoOnline online={presenca[p.usuarioId] ?? p.online} />
          <span className="min-w-0 flex-1 truncate text-gray-800">{p.nome}</span>
          {p.unidadeNome && <span className="shrink-0 truncate text-xs text-gray-400">{p.unidadeNome}</span>}
        </div>
      ))}
    </div>
  );
}
