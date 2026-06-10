import { apiBaseAbsoluto } from '@/shared/api/httpClient';

/**
 * Lança o agente local de assinatura via protocolo `automais-assinador://`.
 * O Windows entrega a chave + a URL do servidor ao agente por linha de comando
 * (sem socket/localhost). O agente reivindica o job pela chave, assina com o
 * certificado da loja do Windows e devolve a assinatura.
 */
export function lancarAgenteAssinatura(chave: string): void {
  const url =
    `automais-assinador://assinar?chave=${encodeURIComponent(chave)}` +
    `&server=${encodeURIComponent(apiBaseAbsoluto)}`;
  window.location.href = url;
}
