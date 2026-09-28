// Roda em TODOS os frames dos sítios observados (SISREG, Prime/Eco).
//  - Qualquer frame: ao carregar, copia o HTML da tela e manda ao service worker
//    (é o RETORNO das operações — o webRequest só vê o envio). Também repassa os
//    corpos de AJAX e as interações que o capture-hook interceptou neste frame.
//  - Só o frame de cima: desenha o selo discreto do SMSMarica com o LED de status
//    e o blur que bloqueia o site enquanto o SMSMarica não estiver conectado.

(() => {
  const NOTOPO = window === window.top;

  // ---------------------------------------------------- captura (todos os frames)
  const TETO_HTML = 4_000_000;
  let profundo = false; // sítio em captura PROFUNDA (config.js: `tudo`)
  let ultimoDigest = null; // evita reenviar a MESMA tela (a captura roda mais de uma vez)

  // Digest barato (FNV-1a de 32 bits) só para comparar telas entre si — não é segurança.
  function digest(s) {
    let h = 0x811c9dc5;
    for (let i = 0; i < s.length; i++) {
      h ^= s.charCodeAt(i);
      h = (h + ((h << 1) + (h << 4) + (h << 7) + (h << 8) + (h << 24))) >>> 0;
    }
    return `${h.toString(16)}:${s.length}`;
  }

  function capturarTela(motivo) {
    try {
      const html = document.documentElement?.outerHTML ?? '';
      const d = digest(html);
      if (d === ultimoDigest) return; // nada mudou desde a última captura
      ultimoDigest = d;
      chrome.runtime.sendMessage({
        tipo: 'resposta',
        dados: {
          caminho: location.pathname,
          url: location.href,
          busca: location.search || null,
          titulo: document.title || null,
          motivo: motivo ?? 'carga',
          formularios: document.forms.length,
          campos: document.querySelectorAll('input,select,textarea').length,
          // Quando corta, vai MARCADO: um pedaço de tela analisado como se fosse o todo
          // leva a conclusão errada.
          truncado: html.length > TETO_HTML,
          tamanhoOriginal: html.length,
          html: html.slice(0, TETO_HTML),
        },
      }).catch(() => {});
    } catch {
      /* frame inacessível */
    }
  }
  // document_end já garante o DOM; um atraso pega telas que montam via JS.
  capturarTela('carga');
  setTimeout(() => capturarTela('carga-tardia'), 1200);

  // Mensagens do mundo da página (capture-hook), só deste frame: corpos de AJAX e o que o
  // operador fez (clique, postback, troca de URL).
  // Recaptura com freio: uma grade do Telerik repinta em rajada, e sem piso de intervalo
  // isso vira uma tela de centenas de KB por segundo na fila. O digest já corta a duplicata
  // exata; o piso corta a enxurrada de telas quase iguais.
  const PISO_RECAPTURA_MS = 5000;
  let recapturaAgendada = null;
  let ultimaRecaptura = 0;
  function recapturarEmBreve(motivo) {
    if (!profundo) return;
    const espera = Math.max(900, ultimaRecaptura + PISO_RECAPTURA_MS - Date.now());
    clearTimeout(recapturaAgendada);
    recapturaAgendada = setTimeout(() => {
      ultimaRecaptura = Date.now();
      capturarTela(motivo);
    }, espera);
  }

  window.addEventListener('message', (e) => {
    if (e.source !== window || !e.data?.__smsmaisHook) return;
    const { __smsmaisHook, tipo, ...dados } = e.data;
    chrome.runtime.sendMessage({ tipo: tipo === 'interacao' ? 'interacao' : 'ajax', dados }).catch(() => {});
    // AJAX do Telerik reescreve a tela sem recarregar: o webRequest vê a requisição, mas a
    // TELA resultante só existe no DOM. Recaptura depois que o eco do DOM assenta.
    // No PC do MÉDICO queremos também a tela depois do AJAX — é ali que anamnese, diagnóstico
    // e prescrição aparecem renderizados, e é justamente o que nunca capturamos. O digest corta
    // duplicata exata e o piso de 5 s corta a rajada, então o custo fica controlado.
    if (tipo === 'ajax' || dados.acao === 'navegacao' || dados.acao === 'postback') {
      recapturarEmBreve(tipo === 'ajax' ? 'pos-ajax' : `pos-${dados.acao}`);
    }
  });

  // Todo frame pergunta o estado uma vez, só para saber se este sítio é captura profunda.
  chrome.runtime
    .sendMessage({ tipo: 'estado' })
    .then((estado) => {
      const meu = (estado?.sitios || []).find((s) => s.host === location.host);
      if (meu?.tudo && meu.modo !== 'minimo') profundo = true;
    })
    .catch(() => {});

  // Operador do SISREG (barra "Operador:/Perfil:/Unidade:"). Depois do login o topo vira um
  // frameset SEM corpo de texto, então a barra fica em ALGUM frame — por isso lemos em todos.
  //
  // No Prime a barra não existe, e por isso `payload.operador` vinha VAZIO em TODAS as capturas
  // do Prime (visto em 23/09/2026 na conferência: sem operador não dá para cruzar o que a
  // extensão capturou com o relatório de atendidos por profissional, só por contagem).
  // O Prime expõe quem está logado **só no gate de unidade** (`/Prime/login.aspx`, depois do
  // POST de login): `#LoginView1_LoginName1` traz o CPF e `LoginView1$ddlUnidade` a unidade
  // escolhida. Lemos ali e o background segura pelo resto da sessão da aba.
  function lerOperador() {
    const doPrime = document.querySelector('#LoginView1_LoginName1');
    if (doPrime) {
      const cpf = (doPrime.textContent || '').replace(/\D/g, '');
      const sel = document.querySelector('select[name="LoginView1$ddlUnidade"]');
      const unidade = sel && sel.value !== '-1'
        ? { id: sel.value, nome: sel.options[sel.selectedIndex]?.text?.trim() || null }
        : null;
      if (cpf) chrome.runtime.sendMessage({ tipo: 'operador', operador: cpf, unidade }).catch(() => {});
      return;
    }
    const txt = document.body?.innerText ?? '';
    const m = txt.match(/Operador\s*:\s*([^\n\r]+)/i);
    if (!m) return;
    const nome = m[1].split(/\s{2,}|Perfil\s*:|Unidade\s*:|Data\s*:/i)[0].trim();
    if (nome) chrome.runtime.sendMessage({ tipo: 'operador', operador: nome }).catch(() => {});
  }
  lerOperador();
  setTimeout(lerOperador, 1500);
  setTimeout(lerOperador, 4000);
  // No gate do Prime a unidade só é escolhida DEPOIS da carga — os três disparos acima pegariam
  // o select ainda em "-1". Reler quando o operador escolhe.
  document.addEventListener('change', (e) => {
    if (e.target?.name === 'LoginView1$ddlUnidade') lerOperador();
  }, true);

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
        <p>Para usar o <b data-ref="sistemaBlur">sistema</b>, entre primeiro no
           <b data-ref="nomeBlur">SMSMarica</b>. As operações feitas aqui são registradas.</p>
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
    $('[data-ref=quem]').textContent = estado.usuario ? `· ${estado.usuario}` : '';
    $('[data-ref=estadoBlur]').textContent = estado.pendentes
      ? `${estado.pendentes} captura(s) aguardando envio`
      : '';
    // Blur nos sítios marcados com blur: é ele que GARANTE que há sessão do SMSMarica para
    // enviar. Sem login, a captura só enche a fila e nada chega ao hub.
    $('[data-ref=sistemaBlur]').textContent = meuSitio?.label ?? 'sistema';
    capa.classList.toggle('mostra', !estado.auth && !!meuSitio?.blur);
    // Cabeçalho da janela de tráfego, honesto por modo.
    $('[data-ref=devcab]').innerHTML =
      meuSitio?.modo === 'minimo'
        ? 'Enviado ao SMSMarica <b>— só o comando + o nº da solicitação</b> (sem dados do paciente)'
        : `Modo análise — <b>tudo que vai e volta</b> é registrado para estudo${meuSitio ? ` (${meuSitio.label})` : ''}`;
    // A janela só fica aberta sozinha no modo mínimo, onde ela É a prova de que só sai comando
    // + número. No modo análise ela é ruído para quem está trabalhando: abre no clique do selo.
    if (meuSitio?.modo === 'minimo') dev.classList.add('mostra');
  }

  $('[data-ref=entrar]').addEventListener('click', () => {
    window.open(painelOrigin, '_blank', 'noopener');
  });

  // Clique no selo abre/fecha a janela do que é enviado ao SMSMarica (transparência).
  $('.selo').addEventListener('click', () => dev.classList.toggle('mostra'));

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
    const alvo = item.conhecido ? `${item.nome} — ${item.caminho}` : item.caminho;
    const redir = item.redirecionamentos?.length ? ` ↷${item.redirecionamentos.length}` : '';
    li.innerHTML =
      `<span class="met">${item.metodo}</span>` +
      `<span class="cam">${alvo}</span>` +
      (item.evento ? `<span class="ev">${item.evento}</span>` : '') +
      `<span>${item.status ?? '…'}${redir}</span>`;
    lista.prepend(li);
    while (lista.children.length > 100) lista.lastChild.remove();
  }

  // Evento de negócio reconhecido (Prime): "ACOLHEU · agenda 80c607c7".
  const ROTULOS = {
    'paciente-criado': 'CRIOU',
    'paciente-agendado': 'AGENDOU',
    'paciente-acolhido': 'ACOLHEU',
    'paciente-desagendado': 'DESAGENDOU',
  };
  function addNegocio(item) {
    const li = document.createElement('li');
    const id = item.agendaId ?? item.pacienteId ?? '';
    const alvo = item.agendaId ? 'agenda' : item.pacienteId ? 'paciente' : '';
    li.innerHTML =
      `<span class="cmd">${ROTULOS[item.evento] ?? item.evento}</span>` +
      `<span class="cam">${alvo} <b>${String(id).slice(0, 8) || '—'}</b>` +
      (item.idAusente ? ' <i>(sem id)</i>' : '') +
      '</span>' +
      `<span class="hora">${new Date(item.quando).toLocaleTimeString('pt-BR')}</span>`;
    lista.prepend(li);
    while (lista.children.length > 100) lista.lastChild.remove();
  }

  chrome.runtime.onMessage.addListener((msg) => {
    if (msg.tipo === 'estado') pintar(msg.estado);
    if (msg.tipo === 'requisicao') addLinha(msg.item); // modo análise (raw)
    if (msg.tipo === 'trafego') addTrafego(msg.item); // modo mínimo (comando + número)
    if (msg.tipo === 'negocio') addNegocio(msg.item); // operação reconhecida
  });
  chrome.runtime.sendMessage({ tipo: 'estado' }).then((estado) => estado && pintar(estado)).catch(() => {});

  document.documentElement.append(host);
})();
