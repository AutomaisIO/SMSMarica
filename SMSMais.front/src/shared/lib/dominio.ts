// Allowlist de domínio para conteúdo que o painel renderiza de forma "viva" (ex.: uma
// <img> que o navegador carrega automaticamente). Só liberamos recursos do próprio
// SMSMais: o domínio raiz, qualquer subdomínio (api./app./arquivos.…) e, em
// desenvolvimento, localhost/127.0.0.1. Qualquer outra origem é tratada como não
// confiável — evita que uma URL vinda de dado de terceiro (ex.: texto de ticket)
// dispare carregamento de um recurso externo (pixel de rastreio / vazamento de referer).

const DOMINIO_RAIZ = 'smsmarica.online';
const HOSTS_DEV = new Set(['localhost', '127.0.0.1', '[::1]']);

/** A URL aponta para um host do domínio SMSMais (ou localhost em dev)? */
export function ehUrlDominioConfiavel(url: string): boolean {
  let host: string;
  try {
    host = new URL(url, window.location.origin).hostname.toLowerCase();
  } catch {
    return false; // URL malformada / relativa sem base → não confiável
  }
  if (HOSTS_DEV.has(host)) return true;
  return host === DOMINIO_RAIZ || host.endsWith(`.${DOMINIO_RAIZ}`);
}
