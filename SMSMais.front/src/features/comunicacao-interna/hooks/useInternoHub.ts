import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { apiBaseAbsoluto } from '@/shared/api/httpClient';
import { obterToken, useAuth } from '@/shared/auth/authStore';
import { useNotificacoesNavegador } from '@/features/conversas/hooks/useNotificacoesNavegador';
import { anexarMensagemNoCache, chavesInterno, transcricaoNoCache } from '@/features/comunicacao-interna/api/queries';
import {
  CANAL_INTERNO,
  ehJanelaInterna,
  focarJanelaInterna,
  janelaInternaAberta,
} from '@/features/comunicacao-interna/lib/janelaInterna';
import { textoLegivel } from '@/features/comunicacao-interna/lib/selos';
import { tocarPlim } from '@/features/comunicacao-interna/lib/som';
import { useInterno } from '@/features/comunicacao-interna/store/internoStore';
import type {
  ConversaInternaDetalhe,
  ConversaInternaItem,
  DigitandoInternoEvento,
  LeituraInternaEvento,
  MensagemInternaEvento,
  PresencaInternaEvento,
  TranscricaoInternaEvento,
} from '@/features/comunicacao-interna/types';

/** Mesmas esperas da Central (useChatHub): a última se repete para sempre — deploy derruba ~45 s. */
const ESPERAS_RECONEXAO_MS = [0, 2_000, 5_000, 10_000, 15_000];

/** Teto de recarga da lista/resumo/unidades disparada por eventos (rajada vira uma consulta só). */
const INTERVALO_LISTA_MS = 2_000;

/** "Digitando…" não é mandado mais que uma vez a cada 3 s por conversa. */
const INTERVALO_DIGITANDO_MS = 3_000;

function esperaReconexao(tentativasAnteriores: number): number {
  return ESPERAS_RECONEXAO_MS[Math.min(tentativasAnteriores, ESPERAS_RECONEXAO_MS.length - 1)];
}

// Conexão module-level: o composer avisa "digitando" sem prop-drilling (há uma conexão por janela).
let connAtual: HubConnection | null = null;
const ultimoDigitando = new Map<string, number>();

/** Avisa a outra ponta que estou digitando nesta conversa (com teto de frequência). */
export function avisarDigitando(conversaId: string) {
  const agora = Date.now();
  if (agora - (ultimoDigitando.get(conversaId) ?? 0) < INTERVALO_DIGITANDO_MS) return;
  ultimoDigitando.set(conversaId, agora);
  if (connAtual?.state === HubConnectionState.Connected) {
    connAtual.invoke('Digitando', conversaId).catch(() => {
      /* cortesia — perder um "digitando" não importa */
    });
  }
}

/** Título que a notificação mostra: na direta é o autor; na de unidade, "A ↔ B". */
function tituloDoEvento(evt: MensagemInternaEvento): string {
  if (evt.tipo === 'Direta') return evt.mensagem.autorNome;
  const a = evt.unidadeANome ?? '';
  const b = evt.unidadeBNome ?? '';
  return a && b && a !== b ? `${a} ↔ ${b}` : a || b || 'Comunicação interna';
}

function previaDoEvento(evt: MensagemInternaEvento): string {
  const m = evt.mensagem;
  const corpo = m.texto
    ? textoLegivel(m.texto)
    : m.anexo
      ? m.anexo.ehAudio
        ? '🎤 Áudio'
        : `📎 ${m.anexo.nome}`
      : 'Nova mensagem';
  return evt.tipo === 'Unidade' ? `${m.autorNome}: ${corpo}` : corpo;
}

/**
 * Conecta ao hub da Comunicação interna e traduz os eventos em cache do react-query + atenção
 * (pulo do painel, som, notificação do Windows). Montar UMA vez por janela: no BalaoInterno
 * (principal) e na página da janela solta. `habilitado` = tem o módulo ComunicacaoInterna.
 */
export function useInternoHub(habilitado: boolean) {
  const queryClient = useQueryClient();
  const token = useAuth((s) => s.token);
  const meuId = useAuth((s) => s.usuario?.id ?? null);
  const total = useInterno((s) => s.totalNaoLidas);
  const painelAberto = useInterno((s) => s.painelAberto);
  const { notificar } = useNotificacoesNavegador();

  const notificarRef = useRef(notificar);
  useEffect(() => {
    notificarRef.current = notificar;
  }, [notificar]);
  const meuIdRef = useRef(meuId);
  useEffect(() => {
    meuIdRef.current = meuId;
  }, [meuId]);

  useEffect(() => {
    if (!token || !habilitado) return;
    const base = apiBaseAbsoluto.replace(/\/api$/, '');
    const conn = new HubConnectionBuilder()
      // Autenticação é o JWT (nunca cookie) — ver o comentário em useChatHub.
      .withUrl(`${base}/hubs/comunicacao-interna`, {
        accessTokenFactory: () => obterToken() ?? '',
        withCredentials: false,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) => esperaReconexao(ctx.previousRetryCount),
      })
      .build();
    connAtual = conn;

    // Lista, resumo e contagem de online das unidades: no máximo uma recarga por janela de tempo.
    let ultimaEm = 0;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const recarregar = () => {
      ultimaEm = Date.now();
      void queryClient.invalidateQueries({ queryKey: chavesInterno.conversas });
      void queryClient.invalidateQueries({ queryKey: chavesInterno.resumo });
    };
    const invalidarLista = () => {
      const espera = INTERVALO_LISTA_MS - (Date.now() - ultimaEm);
      if (espera <= 0) {
        recarregar();
        return;
      }
      if (timer) return;
      timer = setTimeout(() => {
        timer = undefined;
        recarregar();
      }, espera);
    };

    // Conversa de unidade: o evento chega uma vez por grupo (as duas unidades) — dedup pelo id.
    const vistas: string[] = [];
    const jaVista = (id: string) => {
      if (vistas.includes(id)) return true;
      vistas.push(id);
      if (vistas.length > 200) vistas.shift();
      return false;
    };

    conn.on('mensagemInterna', (evt: MensagemInternaEvento) => {
      const m = evt.mensagem;
      if (jaVista(m.id)) return;
      anexarMensagemNoCache(queryClient, m);
      invalidarLista();

      // Mensagem MINHA (de outra aba/janela): só atualiza — nunca chama atenção.
      if (m.autorId === meuIdRef.current) return;

      const st = useInterno.getState();
      const janela = ehJanelaInterna();
      const olhando = (janela || st.painelAberto) && st.conversaAtivaId === m.conversaId && !document.hidden;
      if (olhando) return;

      const item = queryClient
        .getQueryData<ConversaInternaItem[]>(chavesInterno.conversas)
        ?.find((c) => c.id === m.conversaId);
      if (item?.silenciada) return;

      // Com a janela solta aberta, quem chama atenção é ELA (cada janela tem seu hub) — aqui só
      // tentamos levantá-la.
      if (!janela && janelaInternaAberta()) {
        if (st.abrirSozinho) focarJanelaInterna();
        return;
      }

      if (st.somAtivo) tocarPlim();

      if (st.abrirSozinho) {
        if (janela) {
          // Na janela solta: sem conversa aberta, abre esta; com outra aberta, não troca no meio
          // da leitura (a lista mostra o contador). Foco é cortesia do navegador.
          if (!st.conversaAtivaId) st.setConversaAtiva(m.conversaId);
          st.pular();
          try {
            window.focus();
          } catch {
            /* ignorado */
          }
        } else if (!st.painelAberto) {
          st.abrirPainelSozinho(m.conversaId);
        } else {
          st.pular();
        }
      } else {
        st.pular();
      }

      if (document.hidden || !document.hasFocus()) {
        notificarRef.current(tituloDoEvento(evt), previaDoEvento(evt), `interno:${m.conversaId}`, () => {
          const s = useInterno.getState();
          if (ehJanelaInterna()) s.setConversaAtiva(m.conversaId);
          else s.abrirPainel(m.conversaId);
        });
      }
    });

    conn.on('leituraInterna', (evt: LeituraInternaEvento) => {
      if (evt.usuarioId === meuIdRef.current) {
        // Li em outra aba/janela: a lista e o total desta precisam acompanhar.
        invalidarLista();
        return;
      }
      queryClient.setQueryData<ConversaInternaDetalhe>(chavesInterno.detalhe(evt.conversaId), (d) =>
        d && (!d.outroLidaAte || d.outroLidaAte < evt.lidaAte) ? { ...d, outroLidaAte: evt.lidaAte } : d,
      );
    });

    let timerUnidades: ReturnType<typeof setTimeout> | undefined;
    conn.on('presencaInterna', (evt: PresencaInternaEvento) => {
      useInterno.getState().setPresenca(evt.usuarioId, evt.online);
      // Os contadores "N online" das unidades vêm do servidor: refaz (só se a tela estiver aberta).
      if (!timerUnidades) {
        timerUnidades = setTimeout(() => {
          timerUnidades = undefined;
          void queryClient.invalidateQueries({ queryKey: chavesInterno.unidades });
        }, 3_000);
      }
    });

    // Alguém da conversa transcreveu um áudio: o texto aparece para todos sem recarregar.
    conn.on('transcricaoInterna', (evt: TranscricaoInternaEvento) => {
      transcricaoNoCache(queryClient, evt.conversaId, evt.mensagemId, evt.transcricao);
    });

    conn.on('digitandoInterno', (evt: DigitandoInternoEvento) => {
      if (evt.usuarioId === meuIdRef.current) return;
      useInterno.getState().setDigitando(evt.conversaId, evt.usuarioId, evt.nome);
    });

    conn.onreconnected(() => {
      // Presença e mensagens podem ter mudado enquanto o socket estava caído.
      useInterno.getState().limparPresenca();
      void queryClient.invalidateQueries({ queryKey: chavesInterno.tudo });
    });

    // Mesmo laço da Central: o reconnect automático só cobre QUEDA de conexão que chegou a abrir.
    let encerrado = false;
    let tentativasInicio = 0;
    let timerInicio: ReturnType<typeof setTimeout> | undefined;
    const iniciar = () => {
      conn
        .start()
        .then(() => {
          tentativasInicio = 0;
        })
        .catch(() => {
          if (encerrado) return;
          timerInicio = setTimeout(iniciar, Math.max(2_000, esperaReconexao(tentativasInicio++)));
        });
    };
    conn.onclose(() => {
      if (encerrado) return;
      useInterno.getState().limparPresenca();
      void queryClient.invalidateQueries({ queryKey: chavesInterno.tudo });
      timerInicio = setTimeout(iniciar, 2_000);
    });
    iniciar();

    return () => {
      encerrado = true;
      clearTimeout(timerInicio);
      clearTimeout(timer);
      clearTimeout(timerUnidades);
      if (connAtual === conn) connAtual = null;
      conn.stop().catch(() => {});
    };
  }, [token, habilitado, queryClient]);

  // Eco entre a janela principal e a solta: som, abrir sozinho e troca de conversa.
  useEffect(() => {
    if (!('BroadcastChannel' in window)) return;
    const canal = new BroadcastChannel(CANAL_INTERNO);
    canal.onmessage = (e) => {
      const d = e.data as { tipo?: string; ativo?: unknown; id?: unknown } | null;
      if (d?.tipo === 'som' && typeof d.ativo === 'boolean') useInterno.getState().setSom(d.ativo);
      if (d?.tipo === 'abrir-sozinho' && typeof d.ativo === 'boolean') useInterno.getState().setAbrirSozinho(d.ativo);
      if (d?.tipo === 'abrir-conversa' && typeof d.id === 'string' && ehJanelaInterna()) {
        useInterno.getState().setConversaAtiva(d.id);
      }
    };
    return () => canal.close();
  }, []);

  // Título da aba com o contador. Convive com o "(N)" da Central: o nosso prefixo é "✉N" e vai
  // DEPOIS do dela; o reaplique periódico conserta quando a Central reescreve o título.
  useEffect(() => {
    if (!habilitado) return;
    const RE = /✉\d+\s/g;
    function aplicar() {
      const atual = document.title.replace(RE, '');
      const foraDeFoco = document.hidden || (!ehJanelaInterna() && !useInterno.getState().painelAberto);
      if (total > 0 && foraDeFoco) {
        const m = /^\(\d+\)\s*/.exec(atual);
        const prefixoCentral = m ? m[0] : '';
        const novo = `${prefixoCentral}✉${total} ${atual.slice(prefixoCentral.length)}`;
        if (document.title !== novo) document.title = novo;
      } else if (document.title !== atual) {
        document.title = atual;
      }
    }
    aplicar();
    const intervalo = setInterval(aplicar, 2_000);
    document.addEventListener('visibilitychange', aplicar);
    window.addEventListener('focus', aplicar);
    return () => {
      clearInterval(intervalo);
      document.removeEventListener('visibilitychange', aplicar);
      window.removeEventListener('focus', aplicar);
      document.title = document.title.replace(RE, '');
    };
  }, [total, habilitado, painelAberto]);
}
