import { UA, WebSocketInterface } from 'jssip';
import type { RTCSession } from 'jssip/lib/RTCSession';
import type { RTCSessionEvent } from 'jssip/lib/UA';
import { obterMinhaCredencial } from '@/features/telefonia/api/telefoniaApi';
import { useSoftphone } from '@/features/telefonia/store/softphoneStore';
import type { CredencialSoftphone } from '@/features/telefonia/types';

/**
 * Motor do softphone: um UA do JsSIP falando SIP sobre WSS direto com o Asterisk da VM de
 * telefonia. O áudio (DTLS-SRTP) vai do navegador ao Asterisk — o SMSMais não toca na mídia.
 *
 * Só UMA aba registra o ramal: com duas registradas, o Asterisk mandaria a chamada para as duas
 * e a que não atendesse ficaria tocando. A aba que segura o Web Lock é a dona; as outras esperam
 * a vez (fechou a aba dona, a próxima assume sozinha).
 *
 * A credencial é pedida ao servidor na hora e vive só na memória deste módulo.
 */

const NOME_LOCK = 'smsmais-softphone';

let ua: UA | null = null;
let sessao: RTCSession | null = null;
let parar: AbortController | null = null;
let credencial: CredencialSoftphone | null = null;

const audioRemoto = typeof document !== 'undefined' ? criarAudioRemoto() : null;
let campainha: { ctx: AudioContext; parar: () => void } | null = null;

function criarAudioRemoto() {
  const el = document.createElement('audio');
  el.autoplay = true;
  el.style.display = 'none';
  document.body.appendChild(el);
  return el;
}

const estado = () => useSoftphone.getState();

/** Liga o softphone deste usuário. Idempotente: chamar de novo com ele ligado não faz nada. */
export function ligarSoftphone() {
  if (parar) return;
  parar = new AbortController();
  const sinal = parar.signal;

  if (!('locks' in navigator)) {
    // Navegador sem Web Locks: registra sem coordenação entre abas.
    void rodar(sinal);
    return;
  }

  void navigator.locks.query().then((q) => {
    if (sinal.aborted) return;
    if (q.held?.some((l) => l.name === NOME_LOCK)) estado().definirRegistro('outraAba');
  });

  navigator.locks
    .request(NOME_LOCK, { signal: sinal }, async () => {
      await rodar(sinal);
      // Segura o lock até o softphone ser desligado nesta aba (ou a aba fechar).
      await new Promise<void>((resolve) => sinal.addEventListener('abort', () => resolve(), { once: true }));
    })
    .catch(() => {
      /* abortado antes de obter o lock: nada a fazer */
    });
}

/** Desliga (logout, softphone desabilitado, desmontagem). Encerra chamada em curso. */
export function desligarSoftphone() {
  parar?.abort();
  parar = null;
  pararCampainha();
  try {
    sessao?.terminate();
  } catch {
    /* sessão já encerrada */
  }
  sessao = null;
  ua?.stop();
  ua = null;
  credencial = null;
  estado().definirRegistro('desligado');
  estado().definirChamada(null);
}

async function rodar(sinal: AbortSignal) {
  estado().definirRegistro('conectando');
  try {
    credencial = await obterMinhaCredencial();
  } catch {
    if (!sinal.aborted) estado().definirRegistro('falhou', 'Não foi possível obter a credencial do ramal.');
    return;
  }
  if (sinal.aborted) return;

  const socket = new WebSocketInterface(credencial.wssUrl);
  ua = new UA({
    sockets: [socket],
    uri: credencial.uri,
    authorization_user: credencial.usuarioSip,
    password: credencial.senha,
    display_name: credencial.nomeExibicao ?? undefined,
    register: true,
    session_timers: false,
    user_agent: 'SMSMais Softphone',
  });

  ua.on('registered', () => estado().definirRegistro('registrado'));
  ua.on('unregistered', () => {
    if (!sinal.aborted) estado().definirRegistro('conectando');
  });
  ua.on('disconnected', () => {
    if (!sinal.aborted) estado().definirRegistro('conectando', 'Sem conexão com a telefonia. Tentando de novo…');
  });
  ua.on('registrationFailed', (e: { cause?: string }) =>
    estado().definirRegistro('falhou', `O ramal não registrou (${e.cause ?? 'motivo desconhecido'}).`),
  );
  ua.on('newRTCSession', (e: RTCSessionEvent) => aoNovaSessao(e));

  estado().definirRamal(credencial.ramal);
  ua.start();
}

function opcoesMidia() {
  return {
    mediaConstraints: { audio: true, video: false },
    pcConfig: {
      iceServers: (credencial?.iceServers ?? []).map((s) => ({
        urls: s.urls,
        username: s.username ?? undefined,
        credential: s.credential ?? undefined,
      })),
    },
  };
}

function aoNovaSessao(e: RTCSessionEvent) {
  const nova = e.session;

  // Já em ligação: recusa a segunda como ocupado em vez de interromper a que está em curso.
  if (sessao && e.originator === 'remote') {
    nova.terminate({ status_code: 486, reason_phrase: 'Busy Here' });
    return;
  }

  sessao = nova;
  const remoto = nova.remote_identity;
  estado().definirChamada({
    direcao: e.originator === 'remote' ? 'entrada' : 'saida',
    numero: remoto?.uri?.user ?? '',
    nome: remoto?.display_name || null,
    fase: e.originator === 'remote' ? 'tocando' : 'chamando',
    inicio: null,
    mudo: false,
    emEspera: false,
  });

  if (e.originator === 'remote') tocarCampainha();

  const ligarAudio = (pc: RTCPeerConnection) => {
    pc.addEventListener('track', (ev) => {
      if (audioRemoto && ev.streams[0]) audioRemoto.srcObject = ev.streams[0];
    });
  };
  if (nova.connection) ligarAudio(nova.connection as unknown as RTCPeerConnection);
  nova.on('peerconnection', (ev: { peerconnection: RTCPeerConnection }) => ligarAudio(ev.peerconnection));

  nova.on('accepted', () => {
    pararCampainha();
    estado().atualizarChamada({ fase: 'emCurso', inicio: Date.now() });
  });
  const encerrar = () => {
    pararCampainha();
    if (sessao === nova) sessao = null;
    if (audioRemoto) audioRemoto.srcObject = null;
    estado().definirChamada(null);
  };
  nova.on('ended', encerrar);
  nova.on('failed', encerrar);
}

export function ligarPara(numero: string) {
  const alvo = numero.replace(/[^\d*#+]/g, '');
  if (!ua || !credencial || !alvo || sessao) return;
  ua.call(`sip:${alvo}@${credencial.dominio}`, opcoesMidia());
}

export function atender() {
  pararCampainha();
  sessao?.answer(opcoesMidia());
}

export function desligar() {
  try {
    sessao?.terminate();
  } catch {
    /* já encerrada */
  }
}

export function alternarMudo() {
  if (!sessao) return;
  const mudo = sessao.isMuted().audio;
  if (mudo) sessao.unmute({ audio: true });
  else sessao.mute({ audio: true });
  estado().atualizarChamada({ mudo: !mudo });
}

export function alternarEspera() {
  if (!sessao) return;
  const emEspera = sessao.isOnHold().local;
  if (emEspera) sessao.unhold();
  else sessao.hold();
  estado().atualizarChamada({ emEspera: !emEspera });
}

export function enviarDigito(digito: string) {
  sessao?.sendDTMF(digito);
}

/** Campainha sintetizada (dois bipes a cada 3 s): não depende de arquivo de áudio. */
function tocarCampainha() {
  pararCampainha();
  try {
    const ctx = new AudioContext();
    const bipe = (quando: number) => {
      const osc = ctx.createOscillator();
      const ganho = ctx.createGain();
      osc.frequency.value = 480;
      ganho.gain.value = 0.15;
      osc.connect(ganho).connect(ctx.destination);
      osc.start(ctx.currentTime + quando);
      osc.stop(ctx.currentTime + quando + 0.4);
    };
    const ciclo = () => {
      bipe(0);
      bipe(0.6);
    };
    ciclo();
    const id = window.setInterval(ciclo, 3000);
    campainha = {
      ctx,
      parar: () => {
        window.clearInterval(id);
        void ctx.close();
      },
    };
  } catch {
    /* sem áudio disponível: a tela ainda mostra a chamada */
  }
}

function pararCampainha() {
  campainha?.parar();
  campainha = null;
}
