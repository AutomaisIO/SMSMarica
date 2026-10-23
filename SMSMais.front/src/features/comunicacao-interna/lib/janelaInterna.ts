import { abrirJanelaSolta, focarJanelaSolta, janelaSoltaAberta } from '@/shared/lib/janela';
import { salvarPreferencias } from '@/shared/auth/preferenciasApi';
import { useInterno } from '@/features/comunicacao-interna/store/internoStore';

/** Nome fixo da janela solta — reabrir com o mesmo nome traz a existente para frente. */
export const NOME_JANELA_INTERNA = 'smsmarica-interno-janela';

/** Canal entre a janela principal e a solta (trocar conversa, eco de som/abrir sozinho). */
export const CANAL_INTERNO = 'smsmarica-interno';

function transmitir(msg: unknown) {
  if (!('BroadcastChannel' in window)) return;
  const canal = new BroadcastChannel(CANAL_INTERNO);
  canal.postMessage(msg);
  canal.close();
}

/**
 * Abre a Comunicação interna em JANELA SEPARADA (mesma mecânica da Central — abrirJanelaSolta).
 * Com `conversaId`, a janela nova seleciona pela URL e a já aberta troca pelo canal.
 */
export function abrirJanelaInterna(conversaId?: string | null): boolean {
  const url = conversaId ? `/interno/janela?conversa=${conversaId}` : '/interno/janela';
  const ok = abrirJanelaSolta(url, NOME_JANELA_INTERNA, 1000, 680);
  if (conversaId) transmitir({ tipo: 'abrir-conversa', id: conversaId });
  return ok;
}

/** True quando o código roda DENTRO da janela solta da Comunicação interna. */
export function ehJanelaInterna(): boolean {
  return window.name === NOME_JANELA_INTERNA;
}

/** A janela solta (aberta a partir DESTA janela) está aberta? Quem reage à mensagem é ela. */
export function janelaInternaAberta(): boolean {
  return janelaSoltaAberta(NOME_JANELA_INTERNA);
}

/** Tenta levantar a janela solta (best-effort — o navegador pode ignorar sem gesto). */
export function focarJanelaInterna(): void {
  focarJanelaSolta(NOME_JANELA_INTERNA);
}

/**
 * Liga/desliga o som da Comunicação interna: persiste no usuário e ecoa nas outras janelas. Só
 * quem originou a ação grava; o eco (useInternoHub) só aplica no store.
 */
export function definirSomInterno(ativo: boolean) {
  useInterno.getState().setSom(ativo);
  transmitir({ tipo: 'som', ativo });
  void salvarPreferencias({ somComunicacaoInternaSilenciado: !ativo }).catch(() => {
    /* silencioso — próxima alteração tenta de novo. */
  });
}

/** Liga/desliga o "abrir sozinho quando chega mensagem" (mesma mecânica do som). */
export function definirAbrirSozinho(ativo: boolean) {
  useInterno.getState().setAbrirSozinho(ativo);
  transmitir({ tipo: 'abrir-sozinho', ativo });
  void salvarPreferencias({ comunicacaoInternaNaoAbrirSozinho: !ativo }).catch(() => {
    /* silencioso — próxima alteração tenta de novo. */
  });
}
