/**
 * Abre uma janela popup separada — fora do conjunto de abas do navegador
 * sempre que o browser respeitar. Modern Chrome/Edge/Firefox interpretam
 * `popup=yes` (ou simplesmente o uso de width/height) como "abrir popup
 * sem URL bar e sem tabs". Em iOS Safari isso vira aba; é o melhor que
 * conseguimos do lado web sem instalar plug-in.
 *
 * Retorna `false` quando o popup foi bloqueado para que o caller possa
 * avisar o usuário.
 *
 * Gestão de foco: cada janela é registrada por `nome`. Ao chamar de novo com
 * o MESMO nome enquanto a janela ainda está aberta, apenas trazemos a janela
 * existente para o foco (sem reabrir/recarregar) — é o comportamento esperado
 * de "clicar de novo no botão e a janela já aberta vir para a frente". Vale
 * para o visualizador de imagem, anamnese, documentos e PDFs.
 *
 * Importante: não usamos `noopener`/`noreferrer` nas features porque a
 * spec exige que `window.open` retorne `null` quando qualquer um deles
 * está presente — o que faria o caller achar que o popup foi bloqueado
 * mesmo quando abriu com sucesso.
 */
const registroJanelas = new Map<string, Window>();

export function abrirJanelaSolta(
  url: string,
  nome: string,
  largura = 1600,
  altura = 900,
): boolean {
  // Já há uma janela aberta com este nome? Traz para frente, sem recarregar.
  // `window.open('', nome)` re-aponta a janela existente pelo nome (URL vazia =
  // não navega) e, na maioria dos browsers, a restaura/levanta acima das demais
  // mesmo minimizada — o `focus()` sozinho não restaura janela minimizada.
  const existente = registroJanelas.get(nome);
  if (existente && !existente.closed) {
    try {
      window.open('', nome);
    } catch {
      /* alguns browsers podem bloquear; o focus abaixo ainda ajuda */
    }
    existente.focus();
    return true;
  }

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
  ].join(',');
  const janela = window.open(url, nome, features);
  if (!janela) return false;
  registroJanelas.set(nome, janela);
  janela.focus();
  janela.opener = null;
  return true;
}
