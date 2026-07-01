/**
 * Favorito de menu da tela Início (Tarefa "Favoritar menu").
 *
 * Persiste, por usuário, a rota de um menu marcado como favorito no
 * localStorage. Ao abrir o app, se houver favorito, o usuário é levado direto
 * a esse menu (ver InicioPage) — respeitando o encadeamento com o destino
 * padrão interno do menu (ver `resolverDestinoMenu` em menuConfig).
 *
 * Sem backend/migration: tudo em localStorage. A chave inclui o id do usuário
 * quando disponível para não vazar favorito entre contas na mesma máquina.
 */

const PREFIXO = 'smsmarica.menuFavorito';
/** Guarda por sessão do navegador para redirecionar só uma vez ao abrir o app. */
const CHAVE_SESSAO = 'smsmarica.menuFavorito.redirecionado';

function chave(usuarioId?: string): string {
  return usuarioId ? `${PREFIXO}.${usuarioId}` : PREFIXO;
}

export function lerFavorito(usuarioId?: string): string | null {
  try {
    return localStorage.getItem(chave(usuarioId));
  } catch {
    return null;
  }
}

export function definirFavorito(to: string, usuarioId?: string): void {
  try {
    localStorage.setItem(chave(usuarioId), to);
  } catch {
    /* localStorage indisponível — ignora */
  }
}

export function removerFavorito(usuarioId?: string): void {
  try {
    localStorage.removeItem(chave(usuarioId));
  } catch {
    /* ignora */
  }
}

/** Alterna o favorito: se `to` já é o favorito, remove; senão, define. */
export function alternarFavorito(to: string, usuarioId?: string): string | null {
  const atual = lerFavorito(usuarioId);
  if (atual === to) {
    removerFavorito(usuarioId);
    return null;
  }
  definirFavorito(to, usuarioId);
  return to;
}

export function jaRedirecionouNaSessao(): boolean {
  try {
    return sessionStorage.getItem(CHAVE_SESSAO) === '1';
  } catch {
    return false;
  }
}

export function marcarRedirecionadoNaSessao(): void {
  try {
    sessionStorage.setItem(CHAVE_SESSAO, '1');
  } catch {
    /* ignora */
  }
}
