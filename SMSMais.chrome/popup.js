// Popup: estado da extensão e da sessão do SMSMarica.
document.getElementById('versao').textContent = `versão ${chrome.runtime.getManifest().version}`;

const TEXTOS = {
  conectado: 'Conectado ao SMSMarica.',
  enviando: 'Enviando capturas…',
  recebido: 'Capturas registradas.',
  offline: 'SMSMarica fora de alcance — capturas guardadas.',
  desconectado: 'Sem sessão no SMSMarica. Entre no painel para liberar o SISREG.',
};

chrome.runtime.sendMessage({ tipo: 'estado' }, (estado) => {
  const el = document.getElementById('aba');
  if (chrome.runtime.lastError || !estado) {
    el.textContent = 'Extensão ativa. Abra o SISREG para começar.';
    return;
  }
  el.className = estado.auth ? 'ok' : 'nao';
  el.textContent =
    (TEXTOS[estado.estado] ?? '') +
    (estado.usuario ? ` (${estado.usuario})` : '') +
    (estado.pendentes ? ` · ${estado.pendentes} na fila` : '');
});
