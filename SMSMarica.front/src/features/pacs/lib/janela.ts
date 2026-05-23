/**
 * Abre uma janela popup separada — fora do conjunto de abas do navegador
 * sempre que o browser respeitar. Modern Chrome/Edge/Firefox interpretam
 * `popup=yes` (ou simplesmente o uso de width/height) como "abrir popup
 * sem URL bar e sem tabs". Em iOS Safari isso vira aba; é o melhor que
 * conseguimos do lado web sem instalar plug-in.
 *
 * Retorna `false` quando o popup foi bloqueado para que o caller possa
 * avisar o usuário.
 */
export function abrirJanelaSolta(
  url: string,
  nome: string,
  largura = 1600,
  altura = 900,
): boolean {
  const w = Math.min(largura, window.screen.availWidth);
  const h = Math.min(altura, window.screen.availHeight);
  const left = Math.max(0, Math.floor((window.screen.availWidth - w) / 2));
  const top = Math.max(0, Math.floor((window.screen.availHeight - h) / 2));
  const features = [
    'popup=yes',
    `width=${w}`,
    `height=${h}`,
    `left=${left}`,
    `top=${top}`,
    'resizable=yes',
    'scrollbars=yes',
    'toolbar=no',
    'menubar=no',
    'location=no',
    'status=no',
    'noopener',
    'noreferrer',
  ].join(',');
  const janela = window.open(url, nome, features);
  return janela !== null;
}
