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

  // Operador do SISREG (barra "Operador:/Perfil:/Unidade:"). Depois do login o topo vira um
  // frameset SEM corpo de texto, então a barra fica em ALGUM frame — por isso lemos em todos.
  function lerOperador() {
    const txt = document.body?.innerText ?? '';
    const m = txt.match(/Operador\s*:\s*([^\n\r]+)/i);
    if (!m) return;
    const nome = m[1].split(/\s{2,}|Perfil\s*:|Unidade\s*:|Data\s*:/i)[0].trim();
    if (nome) chrome.runtime.sendMessage({ tipo: 'operador', operador: nome }).catch(() => {});
  }
  lerOperador();
  setTimeout(lerOperador, 1500);
  setTimeout(lerOperador, 4000);

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
      .dev header { padding: 6px 30px 6px 10px; background: #f6f8fa; border-bottom: 1px solid #d0d7de;
        font-size: 11px; color: #57606a; }
      .dev .fechar-dev { all: unset; position: absolute; top: 3px; right: 8px; cursor: pointer;
        font-size: 16px; line-height: 1; color: #57606a; padding: 2px 4px; }
      .dev .fechar-dev:hover { color: #1f2328; }
      .dev ul { list-style: none; margin: 0; padding: 0; overflow: auto; }
      .dev li { display: flex; gap: 8px; align-items: baseline; padding: 8px 10px;
        border-bottom: 1px solid #eaeef2; font-size: 12px; }
      .dev .met { font-weight: 600; width: 34px; }
      .dev .cam { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
      .dev .ev { color: #b91c1c; }
      .dev .cmd { font-weight: 700; color: #fff; background: var(--marca, #C8102E);
        border-radius: 5px; padding: 2px 7px; font-size: 11px; letter-spacing: .3px; }
      .dev .hora { color: #8a929c; font-size: 11px; }
      /* menu do selo (só no Prime) */
      .selo .abrir-menu { border: 0; background: #f3f4f6; color: #1f2328; border-radius: 999px;
        width: 24px; height: 24px; font: 700 14px/1 system-ui, sans-serif; cursor: pointer; margin-left: 2px; }
      .selo .abrir-menu:hover { background: #e5e7eb; }
      .menu { position: fixed; z-index: 3; right: 14px; bottom: 58px; min-width: 250px; display: none; flex-direction: column;
        background: #fff; border: 1px solid #d0d7de; border-radius: 10px; padding: 6px;
        box-shadow: 0 8px 24px rgba(0,0,0,.18); pointer-events: auto; }
      .menu.mostra { display: flex; }
      .menu button { all: unset; cursor: pointer; padding: 9px 11px; border-radius: 7px; font-size: 13px; color: #1f2328; }
      .menu button:hover { background: #f3f4f6; }
      .menu button b { color: var(--marca, #C8102E); }
    </style>
    <div class="selo" title="SMSMarica">
      <img data-ref="logo" hidden>
      <span class="nome" data-ref="nome">SMSMarica</span>
      <span class="led" data-ref="led"></span>
      <button class="abrir-menu" data-ref="abrirMenu" title="Menu" hidden>☰</button>
    </div>
    <div class="menu" data-ref="menu">
      <button data-acao="agenda"><b>Agenda SISREG → Prime</b><br><small>montar a agenda do mês de um profissional</small></button>
      <button data-acao="trafego" data-ref="itemTrafego">Mostrar o tráfego enviado</button>
    </div>
    <div class="dev" data-ref="dev">
      <button class="fechar-dev" data-ref="fecharDev" title="Ocultar (fica oculto nas próximas páginas)">×</button>
      <header data-ref="devcab">Enviado ao SMSMarica</header>
      <ul data-ref="lista"></ul>
    </div>
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
    // Qual sítio é esta aba (rótulo no selo + se tem blur).
    const meuSitio = (estado.sitios || []).find((s) => s.host === location.host);
    $('[data-ref=nome]').textContent = meuSitio ? `${nome} · ${meuSitio.label}` : nome;
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
    $('[data-ref=estadoBlur]').textContent = estado.pendentes
      ? `${estado.pendentes} captura(s) aguardando envio`
      : '';
    // Blur só nos sítios marcados com blur (ex.: SISREG). Ecossistemas = captura passiva.
    capa.classList.toggle('mostra', !estado.auth && !!meuSitio?.blur);
    // Cabeçalho da janela de tráfego, honesto por modo.
    $('[data-ref=devcab]').innerHTML =
      meuSitio?.modo === 'minimo'
        ? 'Enviado ao SMSMarica <b>— só o comando + o nº da solicitação</b> (sem dados do paciente)'
        : `Modo análise — tráfego bruto para o log${meuSitio ? ` (${meuSitio.label})` : ''}`;
  }

  $('[data-ref=entrar]').addEventListener('click', () => {
    window.open(painelOrigin, '_blank', 'noopener');
  });

  // Janela do que é enviado ao SMSMarica (transparência). A escolha de mostrar/ocultar fica
  // GUARDADA por site. Padrão: visível no SISREG (mostra que só sai comando + número) e oculta nos
  // demais — no Prime (modo análise) ela enchia a tela a cada leitura da agenda.
  const CHAVE_TRAFEGO = 'trafegoVisivel';
  const padraoTrafego = location.host === 'sisregiii.saude.gov.br';
  function mostrarTrafego(visivel, guardar = true) {
    dev.classList.toggle('mostra', visivel);
    const item = $('[data-ref=itemTrafego]');
    if (item) item.textContent = visivel ? 'Ocultar o tráfego enviado' : 'Mostrar o tráfego enviado';
    if (!guardar) return;
    chrome.storage.local
      .get(CHAVE_TRAFEGO)
      .then((r) => chrome.storage.local.set({ [CHAVE_TRAFEGO]: { ...(r[CHAVE_TRAFEGO] ?? {}), [location.host]: visivel } }))
      .catch(() => {});
  }
  mostrarTrafego(padraoTrafego, false);
  chrome.storage.local
    .get(CHAVE_TRAFEGO)
    .then((r) => {
      const v = r[CHAVE_TRAFEGO]?.[location.host];
      if (typeof v === 'boolean') mostrarTrafego(v, false);
    })
    .catch(() => {});
  $('.selo').addEventListener('click', () => mostrarTrafego(!dev.classList.contains('mostra')));
  $('[data-ref=fecharDev]').addEventListener('click', () => mostrarTrafego(false));

  // Menu do selo — só no Prime, onde mora o assistente de agenda (prime-agenda.js, mesmo mundo
  // isolado desta extensão, que se expõe em globalThis.__smsmaisAgenda).
  const menu = $('[data-ref=menu]');
  const noPrime = location.host === 'marica.ecosistemas.com.br' && location.pathname.startsWith('/Prime/');
  $('[data-ref=abrirMenu]').hidden = !noPrime;
  $('[data-ref=abrirMenu]').addEventListener('click', (e) => {
    e.stopPropagation(); // não alterna a janela de tráfego
    // O menu e a janela de tráfego ocupam o mesmo canto: abrir um recolhe o outro.
    if (menu.classList.toggle('mostra')) dev.classList.remove('mostra');
  });
  menu.addEventListener('click', (e) => {
    const acao = e.target.closest('button')?.dataset.acao;
    if (!acao) return;
    menu.classList.remove('mostra');
    if (acao === 'trafego') mostrarTrafego(!dev.classList.contains('mostra'));
    if (acao === 'agenda') {
      dev.classList.remove('mostra'); // o painel da agenda ocupa o mesmo canto
      if (globalThis.__smsmaisAgenda) globalThis.__smsmaisAgenda.abrir();
      else console.warn('[SMSMais] assistente de agenda não carregou nesta página');
    }
  });

  // Uma linha na janela de tráfego: "→ agendou · solicitação NNNN".
  function addTrafego(item) {
    const li = document.createElement('li');
    const comando = item.comando === 'agendou' ? 'AGENDOU' : item.comando === 'cancelou' ? 'CANCELOU' : (item.comando || '?');
    li.innerHTML =
      `<span class="cmd">${comando}</span>` +
      `<span class="cam">solicitação <b>${item.numero ?? '(nº na confirmação)'}</b></span>` +
      `<span class="hora">${new Date(item.quando).toLocaleTimeString('pt-BR')}</span>`;
    lista.prepend(li);
    while (lista.children.length > 100) lista.lastChild.remove();
  }

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
    if (msg.tipo === 'requisicao') addLinha(msg.item); // modo análise (raw)
    if (msg.tipo === 'trafego') addTrafego(msg.item); // modo mínimo (comando + número)
  });
  chrome.runtime.sendMessage({ tipo: 'estado' }).then((estado) => estado && pintar(estado)).catch(() => {});

  document.documentElement.append(host);
})();
