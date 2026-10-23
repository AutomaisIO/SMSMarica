import { create } from 'zustand';
import { api, type LocalizacaoViagem } from '@/lib/api';
import { classificarErro, codigosDoErro } from '@/lib/httpClient';

/**
 * Localização do paciente no dia da viagem (ADR-0079). O servidor diz se ele espera um embarque agora (a
 * ida ou o resgate) e se aceitou compartilhar; aqui fica o GPS do navegador, ligado só com a página
 * visível e só enquanto ele compartilha.
 *
 * Tudo o que der errado só esconde a função: servidor sem os endpoints (404), sem GPS, permissão negada,
 * rede ou limite. O resto do app segue igual (§9 do ADR).
 */
export type SituacaoGps = 'parado' | 'pedindoPermissao' | 'enviando' | 'semPermissao' | 'semGps';

type LocalizacaoState = {
  espera: LocalizacaoViagem | null;
  situacao: SituacaoGps;
  /** Quando a última posição foi aceita pelo servidor (ms). */
  ultimoEnvio: number | null;
  /** Texto amigável do último "Compartilhar" que falhou. */
  erro: string | null;
  /** O "Compartilhar" está em andamento. */
  iniciando: boolean;
  /** O servidor não tem a função (404): não consulta mais nesta sessão do app. */
  desligado: boolean;
  atualizar: () => Promise<void>;
  compartilhar: () => Promise<void>;
  parar: () => Promise<void>;
  /** Página escondida: desliga o GPS. O aceite continua e o GPS volta quando a página reaparece. */
  pausar: () => void;
  /** Saiu da conta: desliga tudo, sem falar com o servidor. */
  encerrar: () => void;
};

/** O app manda no máximo a cada 15 s, ou antes disso se andou 20 m (nunca antes de 10 s). */
const INTERVALO_MS = 15_000;
const INTERVALO_MINIMO_MS = 10_000;
const DISTANCIA_M = 20;

let watchId: number | null = null;
let ultimaAceita: { lat: number; lng: number; t: number } | null = null;
let ultimaTentativa = 0;
let enviandoPosicao = false;

function temGps(): boolean {
  return typeof navigator !== 'undefined' && !!navigator.geolocation;
}

function desligarGps() {
  if (watchId !== null && temGps()) navigator.geolocation.clearWatch(watchId);
  watchId = null;
  ultimaAceita = null;
}

function distanciaM(a: { lat: number; lng: number }, b: { lat: number; lng: number }): number {
  const r = 6_371_000;
  const rad = (g: number) => (g * Math.PI) / 180;
  const dLat = rad(b.lat - a.lat);
  const dLng = rad(b.lng - a.lng);
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(rad(a.lat)) * Math.cos(rad(b.lat)) * Math.sin(dLng / 2) ** 2;
  return 2 * r * Math.asin(Math.sqrt(h));
}

function paginaVisivel(): boolean {
  return typeof document === 'undefined' || document.visibilityState === 'visible';
}

/** Nunca a mensagem crua do servidor: um texto para cada caso conhecido. */
function mensagemDoErro(e: unknown): string {
  const codigos = codigosDoErro(e);
  if (codigos.includes('localizacao.sessao_por_link'))
    return 'Para compartilhar a sua localização, saia e entre no aplicativo com o código enviado por WhatsApp.';
  if (codigos.includes('localizacao.personificacao'))
    return 'Entrando como o paciente pelo Sandbox não é possível compartilhar a localização.';
  if (codigos.includes('localizacao.fora_da_janela')) return 'Agora não há um embarque esperando por você.';
  return 'Não foi possível começar agora. Tente de novo em instantes.';
}

export const useLocalizacao = create<LocalizacaoState>((set, get) => {
  function ligarGps() {
    if (watchId !== null) return;
    if (!temGps()) {
      set({ situacao: 'semGps' });
      return;
    }
    set({ situacao: 'pedindoPermissao' });
    try {
      watchId = navigator.geolocation.watchPosition(
        (pos) => void enviar(pos),
        (e) => {
          if (e.code === e.PERMISSION_DENIED) {
            desligarGps();
            set({ situacao: 'semPermissao' });
          }
          // Sem sinal ou demora: o navegador tenta de novo sozinho.
        },
        { enableHighAccuracy: true, maximumAge: 10_000, timeout: 60_000 },
      );
    } catch {
      watchId = null;
      set({ situacao: 'semGps' });
    }
  }

  async function enviar(pos: GeolocationPosition) {
    const espera = get().espera;
    if (!espera?.compartilhando || enviandoPosicao) return;
    const agora = Date.now();
    if (agora - ultimaTentativa < INTERVALO_MINIMO_MS) return;
    const atual = { lat: pos.coords.latitude, lng: pos.coords.longitude, t: agora };
    if (ultimaAceita && agora - ultimaAceita.t < INTERVALO_MS && distanciaM(ultimaAceita, atual) < DISTANCIA_M) return;

    enviandoPosicao = true;
    ultimaTentativa = agora;
    try {
      await api.enviarLocalizacao(espera.sessaoId, {
        latitude: atual.lat,
        longitude: atual.lng,
        precisaoM: Number.isFinite(pos.coords.accuracy) ? Math.round(pos.coords.accuracy) : null,
        capturadaEm: new Date(pos.timestamp || agora).toISOString(),
        simulada: false,
      });
      ultimaAceita = atual;
      set({ situacao: 'enviando', ultimoEnvio: agora });
    } catch (e) {
      const { status } = classificarErro(e);
      if (status === 409 || status === 404) {
        // A espera acabou (embarcou, cancelou) ou o servidor não tem a função: para e pergunta de novo.
        desligarGps();
        set({ situacao: 'parado' });
        void get().atualizar();
      }
      // 400 (posição antiga), 429 ou rede: fica para a próxima posição.
    } finally {
      enviandoPosicao = false;
    }
  }

  return {
    espera: null,
    situacao: 'parado',
    ultimoEnvio: null,
    erro: null,
    iniciando: false,
    desligado: false,

    atualizar: async () => {
      if (get().desligado) return;
      try {
        const espera = await api.localizacao();
        set({
          espera,
          ultimoEnvio: get().ultimoEnvio ?? (espera?.ultimoEnvioEm ? Date.parse(espera.ultimoEnvioEm) : null),
        });
        if (espera?.compartilhando) {
          // Aceitou nesta viagem (talvez antes de recarregar a página): o GPS volta com a página visível.
          if (paginaVisivel()) ligarGps();
        } else {
          desligarGps();
          set({ situacao: 'parado', ultimoEnvio: null });
        }
      } catch (e) {
        if (classificarErro(e).status === 404) {
          desligarGps();
          set({ desligado: true, espera: null, situacao: 'parado' });
        }
        // Outros erros (rede, limite, termo ainda não aceito): fica como estava; a próxima consulta tenta.
      }
    },

    compartilhar: async () => {
      const espera = get().espera;
      if (!espera || get().iniciando) return;
      set({ iniciando: true, erro: null });
      try {
        const nova = await api.iniciarLocalizacao(espera.sessaoId);
        set({ espera: nova });
        ligarGps();
      } catch (e) {
        set({ erro: mensagemDoErro(e) });
        void get().atualizar();
      } finally {
        set({ iniciando: false });
      }
    },

    parar: async () => {
      const espera = get().espera;
      desligarGps();
      set({
        situacao: 'parado',
        ultimoEnvio: null,
        espera: espera ? { ...espera, compartilhando: false, desde: null, ultimoEnvioEm: null } : null,
      });
      if (!espera?.compartilhando) return;
      try {
        await api.pararLocalizacao(espera.sessaoId);
      } catch {
        // O servidor encerra sozinho quando a espera acaba.
      }
    },

    pausar: () => {
      desligarGps();
      if (get().situacao === 'enviando' || get().situacao === 'pedindoPermissao') set({ situacao: 'parado' });
    },

    encerrar: () => {
      desligarGps();
      ultimaTentativa = 0;
      set({ espera: null, situacao: 'parado', ultimoEnvio: null, erro: null, iniciando: false, desligado: false });
    },
  };
});
