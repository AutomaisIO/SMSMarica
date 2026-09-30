import { create } from 'zustand';

/**
 * Estado da conexão com o servidor — CONFIRMADO antes de acusar.
 *
 * Até 30/09/2026 qualquer requisição sem resposta virava na hora o toast "Não foi possível
 * conectar ao servidor". Medido no nginx naquele dia: 1.642 falhas em 4 rajadas de 40–48s (os
 * deploys) e 397 cancelamentos feitos pelo próprio painel — nenhum dos dois era a internet do
 * usuário. Agora uma falha de rede só ABRE uma verificação: o painel pergunta ao `/health/live`
 * em intervalos curtos e fica calado se o servidor responder dentro de {@link LIMIAR_AVISO_MS}.
 * Passado isso, entra a faixa fixa no topo (FaixaConexao), que some sozinha quando ele volta.
 *
 * Quem observa `retornos` refaz as consultas na volta — as telas se recuperam sem F5.
 */

/** Queda por falta de internet no computador, API fora do ar, ou nginx avisando que ela reinicia. */
export type CausaQueda = 'internet' | 'servidor' | 'reiniciando';

export type SituacaoConexao = 'ok' | 'verificando' | 'fora';

type ConexaoState = {
  situacao: SituacaoConexao;
  causa: CausaQueda | null;
  /** Sobe a cada volta depois de uma queda: sinal para refazer as consultas. */
  retornos: number;
};

export const useConexao = create<ConexaoState>(() => ({
  situacao: 'ok',
  causa: null,
  retornos: 0,
}));

/**
 * Quanto tempo de falha confirmada antes de mostrar a faixa. Cobre o reinício da API num deploy
 * (~10s depois do ajuste do desligamento) e o soluço de rede da unidade — ninguém precisa saber
 * de uma queda que se resolveu sozinha.
 */
export const LIMIAR_AVISO_MS = 15_000;

/** Intervalo entre sondagens: rápido no começo, depois a cada 5s até o servidor voltar. */
const ESPERAS_MS = [1_000, 2_000, 3_000, 5_000];

/** Sondagem sem resposta em 4s conta como falha — não prende a verificação. */
const TIMEOUT_SONDA_MS = 4_000;

let urlSaude = '/api/health/live';
let desde = 0;
let tentativa = 0;
let timer: ReturnType<typeof setTimeout> | undefined;
let sondando = false;

/** O httpClient informa a base da API (a mesma das requisições) — evita import circular. */
export function configurarSondaConexao(baseApi: string) {
  urlSaude = `${baseApi.replace(/\/+$/, '')}/health/live`;
}

function semInternet(): boolean {
  return typeof navigator !== 'undefined' && navigator.onLine === false;
}

/**
 * Uma requisição falhou sem resposta (ou o nginx respondeu que a API está reiniciando).
 * Não avisa ninguém: só começa a verificar. Chamar várias vezes durante a mesma queda é inofensivo.
 */
export function reportarFalhaDeConexao(causa: CausaQueda = 'servidor') {
  const estado = useConexao.getState();
  const causaEfetiva: CausaQueda = semInternet() ? 'internet' : causa;

  if (estado.situacao !== 'ok') {
    // Já verificando: só refina a causa quando chega uma mais específica que a genérica.
    if (causaEfetiva !== 'servidor' && estado.causa !== causaEfetiva) {
      useConexao.setState({ causa: causaEfetiva });
    }
    return;
  }

  desde = Date.now();
  tentativa = 0;
  useConexao.setState({ situacao: 'verificando', causa: causaEfetiva });
  agendarSonda();
}

/** Sonda agora, sem esperar o próximo intervalo (botão "Tentar agora", evento `online`). */
export function sondarAgora() {
  if (useConexao.getState().situacao === 'ok') return;
  clearTimeout(timer);
  void sondar();
}

function agendarSonda() {
  clearTimeout(timer);
  const espera = ESPERAS_MS[Math.min(tentativa, ESPERAS_MS.length - 1)];
  tentativa += 1;
  timer = setTimeout(() => void sondar(), espera);
}

async function sondar() {
  if (sondando) return;
  sondando = true;
  let causa: CausaQueda | null = null;

  try {
    const resposta = await fetch(urlSaude, {
      cache: 'no-store',
      signal: AbortSignal.timeout(TIMEOUT_SONDA_MS),
    });
    // 503 aqui é o nginx dizendo que a API está fora (error_page do vhost): servidor reiniciando.
    if (!resposta.ok) causa = resposta.status === 503 ? 'reiniciando' : 'servidor';
  } catch {
    causa = semInternet() ? 'internet' : 'servidor';
  } finally {
    sondando = false;
  }

  const estado = useConexao.getState();
  if (estado.situacao === 'ok') return;

  if (causa === null) {
    useConexao.setState({ situacao: 'ok', causa: null, retornos: estado.retornos + 1 });
    return;
  }

  const confirmada = Date.now() - desde >= LIMIAR_AVISO_MS;
  useConexao.setState({
    situacao: confirmada ? 'fora' : estado.situacao,
    // Mantém a causa mais informativa que a requisição original trouxe.
    causa: causa === 'servidor' && estado.causa === 'reiniciando' ? 'reiniciando' : causa,
  });
  agendarSonda();
}

if (typeof window !== 'undefined') {
  // Internet do computador voltou: pergunta na hora em vez de esperar o intervalo.
  window.addEventListener('online', () => sondarAgora());
  // Caiu: o React Query PAUSA as consultas sem internet, então nenhuma requisição falharia para
  // avisar — sem este gatilho o usuário clicaria e nada aconteceria, sem explicação.
  window.addEventListener('offline', () => reportarFalhaDeConexao('internet'));
}
