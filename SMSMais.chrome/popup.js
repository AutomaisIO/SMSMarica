// Popup: estado da extensão e da sessão do SMSMarica.
document.getElementById('versao').textContent = `versão ${chrome.runtime.getManifest().version}`;

const TEXTOS = {
  conectado: 'Conectado ao SMSMarica.',
  enviando: 'Enviando capturas…',
  recebido: 'Capturas registradas.',
  offline: 'SMSMarica fora de alcance — capturas guardadas.',
  desconectado: 'Sem sessão no SMSMarica. Entre no painel para liberar os sistemas.',
};

const MODOS = {
  minimo: 'só o comando + o nº da solicitação',
  analise: 'tudo que vai e volta (estudo)',
};

chrome.runtime.sendMessage({ tipo: 'estado' }, (estado) => {
  const el = document.getElementById('aba');
  if (chrome.runtime.lastError || !estado) {
    el.textContent = 'Extensão ativa. Abra um dos sistemas para começar.';
    return;
  }

  // Fila parada com sessão caída é o modo de falha silencioso do piloto: destaque.
  const travada = !estado.auth && estado.pendentes > 0;
  el.className = travada ? 'alerta' : estado.auth ? 'ok' : 'nao';
  el.textContent =
    (TEXTOS[estado.estado] ?? '') +
    (estado.usuario ? ` (${estado.usuario})` : '') +
    (estado.pendentes ? ` · ${estado.pendentes} na fila` : '');

  const ul = document.getElementById('sitios');
  for (const s of estado.sitios ?? []) {
    const li = document.createElement('li');
    li.textContent = `${s.label}: ${MODOS[s.modo] ?? s.modo}`;
    ul.append(li);
  }
});
