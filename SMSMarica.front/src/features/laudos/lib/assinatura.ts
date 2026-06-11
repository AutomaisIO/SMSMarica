import { apiBaseAbsoluto } from '@/shared/api/httpClient';

/**
 * URL do instalador (MSI) do agente, mostrada no modal "Assinador não encontrado".
 * Default = Release "latest" do repo público de distribuição (publicado pelo CI
 * build-instalador). Configurável via VITE_ASSINADOR_DOWNLOAD_URL.
 */
export const urlDownloadAssinador: string =
  import.meta.env.VITE_ASSINADOR_DOWNLOAD_URL ||
  'https://github.com/AutomaisIO/Automais.Assinador.agente/releases/latest/download/AutomaisAssinador.msi';

/**
 * Segundos para concluir que o agente não pegou o job (não instalado/rodando).
 * Generoso de propósito: o primeiro lançamento extrai o exe self-contained,
 * passa por varredura de AV e enumera a loja de certificados antes de reivindicar
 * o job — em máquina fria (e via AnyDesk) isso passa de 10s com folga. Só conta
 * "não encontrado" se NADA disso aconteceu (o job nunca saiu de "Iniciada").
 */
export const TIMEOUT_AGENTE_SEGUNDOS = 40;

/**
 * Verdadeiro quando a URL do backend resolve para a própria origem do front
 * (caso do proxy `/api` do Vite em dev sem `VITE_API_BASE_URL`). O agente é um
 * processo externo que NÃO passa pelo proxy do Vite, então assinaria contra uma
 * URL inexistente — melhor avisar do que falhar em silêncio.
 */
export function agenteServerInvalido(): boolean {
  return typeof window !== 'undefined' && apiBaseAbsoluto.startsWith(window.location.origin);
}

/**
 * Lança o agente local de assinatura via protocolo `automais-assinador://`.
 * O Windows entrega a chave + a URL do servidor ao agente por linha de comando
 * (sem socket/localhost). O agente reivindica o job pela chave, assina com o
 * certificado da loja do Windows e devolve a assinatura.
 */
export function lancarAgenteAssinatura(chave: string): void {
  if (agenteServerInvalido()) {
    throw new Error(
      'O agente de assinatura não usa o proxy do Vite. Defina VITE_API_BASE_URL com a ' +
        'URL absoluta do backend (ex.: http://localhost:5080) e recarregue a página.',
    );
  }
  const url =
    `automais-assinador://assinar?chave=${encodeURIComponent(chave)}` +
    `&server=${encodeURIComponent(apiBaseAbsoluto)}`;
  window.location.href = url;
}
