import { useEffect, useRef } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { apiBaseAbsoluto } from '@/shared/api/httpClient';
import { obterToken, useAuth } from '@/shared/auth/authStore';
import { useNotificacoesNavegador } from '@/features/conversas/hooks/useNotificacoesNavegador';
import { ocorrenciasKeys } from '@/features/ocorrencias/api/queries';
import { rotuloTipo } from '@/features/ocorrencias/lib/rotulos';
import { useAlertasOcorrencia } from '@/features/ocorrencias/store/alertasOcorrencia';
import type { OcorrenciaRegistradaEvento } from '@/features/ocorrencias/types';

/** Mesmas esperas do hub de conversas: a última se repete para sempre (deploy deixa a API fora ~40 s). */
const ESPERAS_RECONEXAO_MS = [0, 2_000, 5_000, 10_000, 15_000];

function esperaReconexao(tentativasAnteriores: number): number {
  return ESPERAS_RECONEXAO_MS[Math.min(tentativasAnteriores, ESPERAS_RECONEXAO_MS.length - 1)];
}

let audioCtx: AudioContext | null = null;

/** Três bipes agudos — mais insistente que o bipe de mensagem do chat. */
function tocarAlarme() {
  try {
    const Ctx = window.AudioContext
      ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return;
    audioCtx ??= new Ctx();
    for (let i = 0; i < 3; i++) {
      const inicio = audioCtx.currentTime + i * 0.25;
      const osc = audioCtx.createOscillator();
      const gain = audioCtx.createGain();
      osc.connect(gain);
      gain.connect(audioCtx.destination);
      osc.frequency.value = 1_200;
      gain.gain.value = 0.08;
      osc.start(inicio);
      osc.stop(inicio + 0.15);
    }
  } catch {
    /* som é best-effort */
  }
}

/**
 * Conecta ao RastreamentoHub (SignalR) para a central saber NA HORA do REPORT de um tablet:
 * `ocorrenciaRegistrada` vira aviso vermelho em qualquer tela + alarme + notificação do navegador;
 * `ocorrenciaAtualizada` (atendida/encerrada por outro operador) só refaz as listas. O socket é o
 * "invalidador"; a fonte de verdade é o react-query (com poll de fallback de 30 s).
 * `habilitado` = usuário tem o módulo Rastreamento (sem ele, nem conecta).
 */
export function useRastreamentoHub(habilitado: boolean) {
  const queryClient = useQueryClient();
  const token = useAuth((s) => s.token);
  const { notificar } = useNotificacoesNavegador();

  const notificarRef = useRef(notificar);
  useEffect(() => { notificarRef.current = notificar; }, [notificar]);

  useEffect(() => {
    if (!token || !habilitado) return;
    const base = apiBaseAbsoluto.replace(/\/api$/, '');
    const conn = new HubConnectionBuilder()
      // withCredentials:false — autenticação é o JWT, nunca cookie (ver useChatHub).
      .withUrl(`${base}/hubs/rastreamento`, {
        accessTokenFactory: () => obterToken() ?? '',
        withCredentials: false,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) => esperaReconexao(ctx.previousRetryCount),
      })
      .build();

    const invalidar = () => {
      queryClient.invalidateQueries({ queryKey: ocorrenciasKeys.todas() });
      queryClient.invalidateQueries({ queryKey: ['rastreamento', 'frota'] });
    };

    conn.on('ocorrenciaRegistrada', (evt: OcorrenciaRegistradaEvento) => {
      invalidar();
      useAlertasOcorrencia.getState().adicionar(evt);
      tocarAlarme();
      notificarRef.current(
        `Ocorrência: ${rotuloTipo(evt.tipo)}`,
        `Veículo ${evt.veiculoPlaca} — toque para ver.`,
        `ocorrencia:${evt.ocorrenciaId}`,
        () => window.location.assign('/app/rastreamento/ocorrencias'),
      );
    });
    conn.on('ocorrenciaAtualizada', invalidar);
    conn.onreconnected(invalidar);

    // Reconexão automática só cobre QUEDA de conexão aberta; o primeiro start que falha (painel
    // aberto durante um deploy) ou o fechamento definitivo voltam por este laço.
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
      invalidar();
      timerInicio = setTimeout(iniciar, 2_000);
    });
    iniciar();

    return () => {
      encerrado = true;
      clearTimeout(timerInicio);
      conn.stop().catch(() => {});
    };
  }, [token, habilitado, queryClient]);
}
