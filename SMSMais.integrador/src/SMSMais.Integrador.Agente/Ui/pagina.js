// Injetado pelo agente em TODA página do Chrome dele (Page.addScriptToEvaluateOnNewDocument),
// antes de qualquer script da página. Faz o papel de content.js + auth-content.js da extensão:
//  - no painel do SMSMais: lê a sessão (localStorage) e entrega ao agente;
//  - nos sítios observados (todos os frames): copia o HTML da tela (o RETORNO) e lê o operador;
//  - só no frame de cima: desenha o selo com LED, o blur e a janela de tráfego.
// Fala com o agente pelo binding __smsmaisIntegradorBind (Runtime.addBinding). O iframe do SISREG
// é da mesma origem: se o próprio frame não tiver o binding, usa o do topo.
// O agente fala com a página por window.__smsmaisIntegrador.{estado,trafego,requisicao}.
(() => {
  const CFG = window.__smsmaisIntegradorCfg;
  if (!CFG || window.__smsmaisIntegradorFeito) return;
  window.__smsmaisIntegradorFeito = true;

  function bind(msg) {
    try {
      const fn = window.__smsmaisIntegradorBind || (window.top && window.top.__smsmaisIntegradorBind);
      if (fn) return Promise.resolve(fn(JSON.stringify(msg))).catch(() => {});
    } catch {
      /* frame sem binding ainda, ou topo de outra origem */
    }
    return Promise.resolve();
  }
  const pronto = (fn) =>
    document.readyState === 'loading' ? document.addEventListener('DOMContentLoaded', fn) : fn();

  const host = location.host;
  let hostPainel = '';
  try {
    hostPainel = new URL(CFG.painelOrigin).host;
  } catch {
    /* config sem painel */
  }

  // ------------------------------------------------------------- painel (sessão)
  if (host === hostPainel) {
    const CHAVE = 'smsmarica.auth';
    function lerSessao() {
      try {
        const bruto = localStorage.getItem(CHAVE);
        if (!bruto) return null;
        const o = JSON.parse(bruto);
        if (!o?.token || !o?.expiraEm) return null;
        if (new Date(o.expiraEm).getTime() < Date.now()) return null;
        // Só o necessário — nada de permissões/unidades.
        return { token: o.token, expiraEm: o.expiraEm, usuario: o.usuario ?? null };
      } catch {
        return null;
      }
    }
    let ultimo = '';
    function sincronizar(forcar) {
      const sessao = lerSessao();
      const assinatura = sessao ? sessao.token : '';
      if (!forcar && assinatura === ultimo) return; // nada mudou
      ultimo = assinatura;
      bind({ tipo: 'auth', sessao });
    }
    pronto(() => sincronizar(true));
    // Login/logout acontecem depois da carga: reconfere ao voltar o foco e de tempos em tempos.
    window.addEventListener('focus', () => sincronizar(false));
    document.addEventListener('visibilitychange', () => !document.hidden && sincronizar(false));
    window.addEventListener('storage', (e) => e.key === CHAVE && sincronizar(false));
    setInterval(() => sincronizar(false), 15000);
    return;
  }

  const sitio = (CFG.sitios || []).find((s) => s.host === host);
  if (!sitio) return; // fora da lista: este script não faz NADA aqui

  // ---------------------------------------------------- captura (todos os frames)
  function capturarTela() {
    try {
      const html = document.documentElement?.outerHTML ?? '';
      bind({
        tipo: 'resposta',
        dados: {
          caminho: location.pathname,
          url: location.href,
          titulo: document.title || null,
          formularios: document.forms.length,
          campos: document.querySelectorAll('input,select,textarea').length,
          html: html.slice(0, CFG.respostaMaxChars || 2_000_000),
        },
      });
    } catch {
      /* frame inacessível */
    }
  }

  // Operador do SISREG (barra "Operador:/Perfil:/Unidade:"). Depois do login o topo vira um
  // frameset SEM corpo de texto, então a barra fica em ALGUM frame — por isso lemos em todos.
  function lerOperador() {
    const txt = document.body?.innerText ?? '';
    const m = txt.match(/Operador\s*:\s*([^\n\r]+)/i);
    if (!m) return;
    const nome = m[1].split(/\s{2,}|Perfil\s*:|Unidade\s*:|Data\s*:/i)[0].trim();
    if (nome) bind({ tipo: 'operador', operador: nome });
  }

  pronto(() => {
    capturarTela();
    setTimeout(capturarTela, 1200); // pega telas que montam via JS
    lerOperador();
    setTimeout(lerOperador, 1500);
    setTimeout(lerOperador, 4000);
  });

  if (window !== window.top) return; // o resto é só do frame de cima

  // ------------------------------------------------------------- selo + blur (UI)
  pronto(() => {
    if (document.getElementById('smsmais-integrador')) return;
    const hostEl = document.createElement('div');
    hostEl.id = 'smsmais-integrador';
    hostEl.style.cssText = 'all:initial;position:fixed;inset:0;z-index:2147483647;pointer-events:none;';
    const raiz = hostEl.attachShadow({ mode: 'open' });
    raiz.innerHTML = `
      <style>
        :host { font: 13px/1.4 system-ui, sans-serif; }
        * { box-sizing: border-box; }
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
        .dev { position: fixed; right: 14px; bottom: 56px; width: 430px; max-height: 55vh; display: none;
          flex-direction: column; background: #fff; border: 1px solid #d0d7de; border-radius: 10px;
          box-shadow: 0 8px 24px rgba(0,0,0,.18); overflow: hidden; pointer-events: auto; }
        .dev.mostra { display: flex; }
        .dev header { padding: 6px 10px; background: #f6f8fa; border-bottom: 1px solid #d0d7de;
          font-size: 11px; color: #57606a; }
        .dev ul { list-style: none; margin: 0; padding: 0; overflow: auto; }
        .dev li { display: flex; gap: 8px; align-items: baseline; padding: 8px 10px;
          border-bottom: 1px solid #eaeef2; font-size: 12px; }
        .dev .met { font-weight: 600; width: 34px; }
        .dev .cam { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .dev .ev { color: #b91c1c; }
        .dev .cmd { font-weight: 700; color: #fff; background: var(--marca, #C8102E);
          border-radius: 5px; padding: 2px 7px; font-size: 11px; letter-spacing: .3px; }
        .dev .hora { color: #8a929c; font-size: 11px; }
      </style>
      <div class="selo" title="SMSMarica">
        <img data-ref="logo" hidden>
        <span class="nome" data-ref="nome">SMSMarica</span>
        <span class="led" data-ref="led"></span>
        <span class="quem" data-ref="quem"></span>
      </div>
      <div class="dev" data-ref="dev">
        <header data-ref="devcab">Enviado ao SMSMarica</header>
        <ul data-ref="lista"></ul>
      </div>
      <div class="capa" data-ref="capa">
        <div class="cartao">
          <img data-ref="logoBlur" hidden>
          <h2 data-ref="tituloBlur">SMSMarica</h2>
          <p>Para usar o ${sitio.label}, entre primeiro no <b data-ref="nomeBlur">SMSMarica</b>.
             As operações feitas aqui são registradas.</p>
          <button data-ref="entrar">Entrar no SMSMarica</button>
          <div class="estado" data-ref="estadoBlur"></div>
        </div>
      </div>`;

    const $ = (s) => raiz.querySelector(s);
    const led = $('[data-ref=led]');
    const capa = $('[data-ref=capa]');
    const dev = $('[data-ref=dev]');
    const lista = $('[data-ref=lista]');

    let painelOrigin = CFG.painelOrigin;
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
      hostEl.style.setProperty('--marca', cor);
      const nome = estado.marca?.nomeCurto || 'SMSMarica';
      const meuSitio = (estado.sitios || []).find((s) => s.host === location.host) || sitio;
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
      $('[data-ref=quem]').textContent = estado.usuario ? `· ${estado.usuario}` : '';
      $('[data-ref=estadoBlur]').textContent = estado.pendentes
        ? `${estado.pendentes} captura(s) aguardando envio`
        : '';
      // Blur só nos sítios marcados com blur (ex.: SISREG). Ecossistemas = captura passiva.
      capa.classList.toggle('mostra', !estado.auth && !!meuSitio?.blur);
      $('[data-ref=devcab]').innerHTML =
        meuSitio?.modo === 'minimo'
          ? 'Enviado ao SMSMarica <b>— só o comando + o nº da solicitação</b> (sem dados do paciente)'
          : `Modo análise — tráfego bruto para o log${meuSitio ? ` (${meuSitio.label})` : ''}`;
    }

    $('[data-ref=entrar]').addEventListener('click', () => {
      window.open(painelOrigin, '_blank', 'noopener');
    });

    // Clique no selo abre/fecha a janela do que é enviado ao SMSMarica (transparência).
    $('.selo').addEventListener('click', () => dev.classList.toggle('mostra'));
    dev.classList.add('mostra'); // já visível — mostra que só sai comando + número

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

    // Modo análise: uma linha por requisição (atualiza o status quando a resposta chega).
    function addLinha(item) {
      const id = item.requestId ? `r-${item.requestId}` : null;
      let li = id ? lista.querySelector(`[data-id="${id}"]`) : null;
      if (!li) {
        li = document.createElement('li');
        if (id) li.dataset.id = id;
        lista.prepend(li);
        while (lista.children.length > 100) lista.lastChild.remove();
      }
      li.innerHTML =
        `<span class="met">${item.metodo}</span>` +
        `<span class="cam">${item.nome} — ${item.caminho}</span>` +
        (item.evento ? `<span class="ev">${item.evento}</span>` : '') +
        `<span>${item.status ?? '…'}</span>`;
    }

    window.__smsmaisIntegrador = { estado: pintar, trafego: addTrafego, requisicao: addLinha };
    document.documentElement.append(hostEl);
    bind({ tipo: 'estado' }); // pede o estado atual ao agente (LED, marca, blur)
  });
})();
