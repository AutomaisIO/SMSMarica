// Roda em TODOS os frames do SISREG.
//  - Qualquer frame: ao carregar, copia o HTML da tela e manda ao service worker
//    (é o RETORNO das operações — o webRequest só vê o envio). Também repassa os
//    corpos de AJAX que o capture-hook interceptou neste frame.
//  - Só o frame de cima: desenha o selo discreto do SMSMarica com o LED de status
//    e o blur que bloqueia o SISREG enquanto o SMSMarica não estiver conectado.

(() => {
  const NOTOPO = window === window.top;

  // ---------------------------------------------------- captura (todos os frames)
  function capturarTela() {
    try {
      const html = document.documentElement?.outerHTML ?? '';
      chrome.runtime.sendMessage({
        tipo: 'resposta',
        dados: {
          caminho: location.pathname,
          url: location.href,
          titulo: document.title || null,
          formularios: document.forms.length,
          campos: document.querySelectorAll('input,select,textarea').length,
          html: html.slice(0, 2_000_000),
        },
      }).catch(() => {});
    } catch {
      /* frame inacessível */
    }
  }
  // document_end já garante o DOM; um atraso pega telas que montam via JS.
  capturarTela();
  setTimeout(capturarTela, 1200);

  // Corpos de AJAX vindos do mundo da página (capture-hook), só deste frame.
  window.addEventListener('message', (e) => {
    if (e.source !== window || !e.data?.__smsmaisHook) return;
    const { tipo, ...dados } = e.data;
    chrome.runtime.sendMessage({ tipo: 'ajax', dados }).catch(() => {});
  });

  if (!NOTOPO) return; // o resto é só do frame de cima

  // ------------------------------------------------------------- selo + blur (UI)
  if (document.getElementById('smsmais-ponte')) return;
  const host = document.createElement('div');
  host.id = 'smsmais-ponte';
  host.style.cssText = 'all:initial;position:fixed;inset:0;z-index:2147483647;pointer-events:none;';
  const raiz = host.attachShadow({ mode: 'open' });
  raiz.innerHTML = `
    <style>
      :host { font: 13px/1.4 system-ui, sans-serif; }
      * { box-sizing: border-box; }
      /* selo discreto */
      .selo { position: fixed; right: 14px; bottom: 14px; pointer-events: auto;
        display: flex; align-items: center; gap: 8px; padding: 6px 11px;
        background: #fff; border: 1px solid #e2e2e6; border-left: 3px solid var(--marca, #C8102E);
        border-radius: 999px; box-shadow: 0 3px 12px rgba(0,0,0,.14); cursor: default; user-select: none; }
      .selo img { height: 18px; width: auto; }
      .selo .nome { font-weight: 600; color: #1f2328; }
      .led { width: 10px; height: 10px; border-radius: 50%; background: #b0b4bb; flex: none;
        transition: background .2s; }
      .led.on { background: #1a9d4b; }
      .led.send { background: #e0a400; animation: pisca .8s infinite; }
      .led.recv { background: #1a9d4b; box-shadow: 0 0 0 4px rgba(26,157,75,.25); }
      .led.off { background: #d23b3b; }
      @keyframes pisca { 50% { opacity: .3; } }
      .selo .quem { color: #6b7280; font-size: 11px; max-width: 140px; overflow: hidden;
        text-overflow: ellipsis; white-space: nowrap; }
      /* blur */
      .capa { position: fixed; inset: 0; pointer-events: auto; display: none;
        align-items: center; justify-content: center;
        background: rgba(20,20,25,.35); backdrop-filter: blur(7px); -webkit-backdrop-filter: blur(7px); }
      .capa.mostra { display: flex; }
      .cartao { background: #fff; border-radius: 16px; padding: 30px 34px; max-width: 380px; text-align: center;
        box-shadow: 0 18px 50px rgba(0,0,0,.3); border-top: 5px solid var(--marca, #C8102E); }
      .cartao img { height: 40px; margin-bottom: 6px; }
      .cartao h2 { margin: 8px 0 4px; font-size: 18px; color: #1f2328; }
      .cartao p { margin: 0 0 20px; color: #57606a; font-size: 13px; line-height: 1.5; }
      .cartao button { background: var(--marca, #C8102E); color: #fff; border: 0; border-radius: 9px;
        padding: 11px 20px; font: inherit; font-weight: 600; cursor: pointer; width: 100%; }
      .cartao .estado { margin-top: 14px; font-size: 12px; color: #8a929c; }
      /* lista de dev (alt+clique no selo) */
      .dev { position: fixed; right: 14px; bottom: 56px; width: 430px; max-height: 55vh; display: none;
        flex-direction: column; background: #fff; border: 1px solid #d0d7de; border-radius: 10px;
        box-shadow: 0 8px 24px rgba(0,0,0,.18); overflow: hidden; pointer-events: auto; }
      .dev.mostra { display: flex; }
      .dev header { padding: 6px 10px; background: #f6f8fa; border-bottom: 1px solid #d0d7de;
        font-size: 11px; color: #57606a; }
      .dev ul { list-style: none; margin: 0; padding: 0; overflow: auto; }
      .dev li { display: flex; gap: 6px; padding: 5px 10px; border-bottom: 1px solid #eaeef2; font-size: 11px; }
      .dev .met { font-weight: 600; width: 34px; }
      .dev .cam { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
      .dev .ev { color: #b91c1c; }
    </style>
    <div class="selo" title="SMSMarica">
      <img data-ref="logo" hidden>
      <span class="nome" data-ref="nome">SMSMarica</span>
      <span class="led" data-ref="led"></span>
      <span class="quem" data-ref="quem"></span>
    </div>
    <div class="dev" data-ref="dev"><header>capturas (modo dev — alt+clique no selo)</header><ul data-ref="lista"></ul></div>
    <div class="capa" data-ref="capa">
      <div class="cartao">
        <img data-ref="logoBlur" hidden>
        <h2 data-ref="tituloBlur">SMSMarica</h2>
        <p>Para usar o SISREG, entre primeiro no <b data-ref="nomeBlur">SMSMarica</b>.
           As operações feitas no SISREG são registradas.</p>
        <button data-ref="entrar">Entrar no SMSMarica</button>
        <div class="estado" data-ref="estadoBlur"></div>
      </div>
    </div>`;

  const $ = (s) => raiz.querySelector(s);
  const led = $('[data-ref=led]');
  const capa = $('[data-ref=capa]');
  const dev = $('[data-ref=dev]');
  const lista = $('[data-ref=lista]');

  let painelOrigin = 'https://smsmarica.online';
  const CLASSES = { conectado: 'on', enviando: 'send', recebido: 'recv', offline: 'off', desconectado: '' };
  const TITULOS = {
    conectado: 'Conectado ao SMSMarica',
    enviando: 'Enviando ao SMSMarica…',
    recebido: 'Registrado no SMSMarica',
    offline: 'SMSMarica fora de alcance (as capturas ficam guardadas)',
    desconectado: 'Sem sessão no SMSMarica',
  };

  function pintar(estado) {
    if (estado.painelOrigin) painelOrigin = estado.painelOrigin;
    const cor = estado.marca?.corPrimaria || '#C8102E';
    raiz.host.style.setProperty('--marca', cor);
    host.style.setProperty('--marca', cor);
    const nome = estado.marca?.nomeCurto || 'SMSMarica';
    $('[data-ref=nome]').textContent = nome;
    $('[data-ref=nomeBlur]').textContent = nome;
    $('[data-ref=tituloBlur]').textContent = nome;
    if (estado.marca?.logoUrl) {
      for (const ref of ['logo', 'logoBlur']) {
        const img = $(`[data-ref=${ref}]`);
        img.src = estado.marca.logoUrl;
        img.hidden = false;
      }
    }
    led.className = 'led ' + (CLASSES[estado.estado] ?? '');
    $('.selo').title = TITULOS[estado.estado] ?? '';
    $('[data-ref=quem]').textContent = estado.usuario ? `· ${estado.usuario}` : '';
    $('[data-ref=estadoBlur]').textContent = estado.pendentes
      ? `${estado.pendentes} captura(s) aguardando envio`
      : '';
    capa.classList.toggle('mostra', !estado.auth);
  }

  $('[data-ref=entrar]').addEventListener('click', () => {
    window.open(painelOrigin, '_blank', 'noopener');
  });

  // alt+clique no selo abre a lista de capturas (só para depurar; usuário final não usa).
  $('.selo').addEventListener('click', (e) => {
    if (e.altKey) dev.classList.toggle('mostra');
  });

  function addLinha(item) {
    const li = document.createElement('li');
    li.innerHTML =
      `<span class="met">${item.metodo}</span>` +
      `<span class="cam">${item.nome} — ${item.caminho}</span>` +
      (item.evento ? `<span class="ev">${item.evento}</span>` : '') +
      `<span>${item.status ?? '…'}</span>`;
    lista.prepend(li);
    while (lista.children.length > 100) lista.lastChild.remove();
  }

  chrome.runtime.onMessage.addListener((msg) => {
    if (msg.tipo === 'estado') pintar(msg.estado);
    if (msg.tipo === 'requisicao') addLinha(msg.item);
  });
  chrome.runtime.sendMessage({ tipo: 'estado' }).then((estado) => estado && pintar(estado)).catch(() => {});

  // Lê a barra "Operador:" (aparece após o login) e informa ao service worker.
  function lerOperador() {
    const txt = document.body?.innerText ?? '';
    const m = txt.match(/Operador:\s*([^\n\r]+?)\s{2,}|Operador:\s*([^\n\r]+)/i);
    const nome = (m?.[1] || m?.[2] || '').trim();
    if (nome) chrome.runtime.sendMessage({ tipo: 'operador', operador: nome }).catch(() => {});
  }
  lerOperador();
  setTimeout(lerOperador, 1500);

  document.documentElement.append(host);
})();
