// Injeta, no campo de celular da tela de cadastro do Prime, o SELO de WhatsApp verificado + um
// botão "Validar" que dispara o OTP pela NOSSA API (SMSMarica). Roda no mundo isolado, só em
// marica.ecosistemas.com.br. Não fala com o Prime — só lê os campos da tela (CPF e celular) e
// conversa com o service worker, que chama o SMSMarica. A regra de ouro segue de pé.
//
// Estados do selo:
//   cinza  — número não verificado no SMSMarica (mostra "Validar")
//   verde  — este número está verificado (ícone WhatsApp ✓)
//   âmbar  — verificado, mas OUTRO número (o cadastro mudou o celular)

(() => {
  if (window.__smsmais_wa__) return;
  window.__smsmais_wa__ = true;

  const soDig = (s) => (s || '').replace(/\D/g, '');
  const ult = (s, n = 10) => soDig(s).slice(-n); // compara pelos últimos dígitos (ignora DDI/DDD)

  // Acha o input pelo sufixo do id do Telerik (id termina em _<sufixo>).
  function campo(sufixo) {
    return (
      document.querySelector(`input[id$='_${sufixo}']`) ||
      document.querySelector(`input[name$='$${sufixo}']`)
    );
  }

  function pergunta(tipo, extra) {
    return new Promise((resolve) => {
      chrome.runtime.sendMessage({ tipo, ...extra }, (r) => resolve(r || { erro: 'sem resposta' }));
    });
  }

  // ------------------------------------------------------------------ UI (selo + botão)
  let host, raiz, badge, botao, texto;
  function montar(refCelular) {
    if (host && document.contains(host)) return;
    host = document.createElement('span');
    host.id = 'smsmais-wa';
    host.style.cssText = 'display:inline-flex;vertical-align:middle;margin-left:8px;';
    raiz = host.attachShadow({ mode: 'open' });
    raiz.innerHTML = `
      <style>
        * { box-sizing: border-box; font: 12px/1.3 system-ui, sans-serif; }
        .selo { display:inline-flex; align-items:center; gap:6px; }
        .led { width:10px; height:10px; border-radius:50%; background:#b0b4bb; flex:none; }
        .led.on { background:#1a9d4b; } .led.warn { background:#e0a400; }
        .txt { color:#57606a; white-space:nowrap; }
        .txt.on { color:#1a7f37; font-weight:600; }
        button { border:0; border-radius:7px; padding:5px 10px; font:inherit; font-weight:600;
          cursor:pointer; background:#25D366; color:#fff; }
        button:disabled { background:#b7c3bd; cursor:default; }
      </style>
      <span class="selo">
        <span class="led" data-ref="led"></span>
        <span class="txt" data-ref="txt">WhatsApp</span>
        <button data-ref="btn" hidden>Validar</button>
      </span>`;
    badge = raiz.querySelector('[data-ref=led]');
    texto = raiz.querySelector('[data-ref=txt]');
    botao = raiz.querySelector('[data-ref=btn]');
    botao.addEventListener('click', aoValidar);
    // Coloca logo depois do campo de celular.
    refCelular.insertAdjacentElement('afterend', host);
  }

  function pintar(estado, msg) {
    if (!badge) return;
    badge.className = 'led' + (estado === 'ok' ? ' on' : estado === 'warn' ? ' warn' : '');
    texto.className = 'txt' + (estado === 'ok' ? ' on' : '');
    texto.textContent = msg;
    botao.hidden = estado === 'ok';
  }

  // -------------------------------------------------------------------- checar status
  let ultimoCheque = '';
  async function checar() {
    const inpCpf = campo('txtCPF');
    const inpCel = campo('txtCelular');
    if (!inpCel) return;
    montar(inpCel);
    const cpf = soDig(inpCpf?.value);
    const cel = inpCel.value || '';
    if (cpf.length !== 11 || ult(cel).length < 10) {
      pintar('', 'informe CPF e celular');
      return;
    }
    const chave = cpf + '|' + ult(cel);
    if (chave === ultimoCheque) return; // nada mudou
    ultimoCheque = chave;
    pintar('', 'verificando…');
    const r = await pergunta('wa-status', { cpf });
    if (r.semSessao) return pintar('', 'entre no SMSMarica');
    if (r.erro) return pintar('', 'falha ao checar');
    if (r.verificado && ult(r.numeroVerificado) === ult(cel)) {
      pintar('ok', 'WhatsApp verificado ✓');
    } else if (r.verificado) {
      pintar('warn', 'verificado em OUTRO número');
    } else {
      pintar('', 'não verificado');
    }
  }

  // -------------------------------------------------------------------- validar (OTP)
  async function aoValidar() {
    const cpf = soDig(campo('txtCPF')?.value);
    const numero = campo('txtCelular')?.value || '';
    if (cpf.length !== 11 || ult(numero).length < 10) {
      return abrirModal({ erro: 'Preencha CPF e celular antes de validar.' });
    }
    botao.disabled = true;
    texto.textContent = 'enviando código…';
    const r = await pergunta('wa-enviar', { cpf, numero });
    botao.disabled = false;
    if (r.semSessao) return pintar('', 'entre no SMSMarica');
    if (r.erro) return abrirModal({ erro: r.erro });
    abrirModal({ cpf, numero, mascara: r.mascara, expira: r.expiraEmSegundos });
  }

  // -------------------------------------------------------------------- modal do código
  function abrirModal({ cpf, numero, mascara, expira, erro }) {
    document.getElementById('smsmais-wa-modal')?.remove();
    const m = document.createElement('div');
    m.id = 'smsmais-wa-modal';
    m.style.cssText =
      'all:initial;position:fixed;inset:0;z-index:2147483647;display:flex;align-items:center;' +
      'justify-content:center;background:rgba(20,20,25,.45);';
    const r = m.attachShadow({ mode: 'open' });
    r.innerHTML = `
      <style>
        * { box-sizing:border-box; font:14px/1.4 system-ui, sans-serif; }
        .cart { background:#fff; border-radius:14px; padding:24px 26px; width:360px; max-width:92vw;
          box-shadow:0 18px 50px rgba(0,0,0,.3); border-top:5px solid #25D366; }
        h3 { margin:0 0 4px; font-size:16px; color:#1f2328; }
        p { margin:0 0 16px; color:#57606a; font-size:13px; }
        input { width:100%; padding:11px; font-size:20px; letter-spacing:6px; text-align:center;
          border:1px solid #d0d7de; border-radius:9px; }
        .linha { display:flex; gap:8px; margin-top:16px; }
        button { flex:1; border:0; border-radius:9px; padding:11px; font:inherit; font-weight:600; cursor:pointer; }
        .ok { background:#25D366; color:#fff; } .cancel { background:#eef0f2; color:#1f2328; }
        .msg { margin-top:12px; font-size:13px; min-height:18px; }
        .msg.err { color:#b91c1c; } .msg.good { color:#1a7f37; font-weight:600; }
      </style>
      <div class="cart">
        <h3>Validar WhatsApp</h3>
        <p data-ref="sub"></p>
        <input data-ref="cod" inputmode="numeric" maxlength="8" placeholder="------" ${erro ? 'disabled' : ''}>
        <div class="msg ${erro ? 'err' : ''}" data-ref="msg">${erro || ''}</div>
        <div class="linha">
          <button class="cancel" data-ref="fechar">Fechar</button>
          <button class="ok" data-ref="confirmar" ${erro ? 'disabled' : ''}>Confirmar</button>
        </div>
      </div>`;
    const q = (s) => r.querySelector(s);
    q('[data-ref=sub]').textContent = erro
      ? 'Não foi possível enviar o código.'
      : `Enviamos um código por WhatsApp para ${mascara || 'o número informado'}. Peça o código ao paciente e digite abaixo.`;
    q('[data-ref=fechar]').addEventListener('click', () => m.remove());
    const inp = q('[data-ref=cod]');
    if (!erro) inp.focus();
    q('[data-ref=confirmar]').addEventListener('click', async () => {
      const codigo = soDig(inp.value);
      if (codigo.length < 4) return;
      const msg = q('[data-ref=msg]');
      msg.className = 'msg';
      msg.textContent = 'confirmando…';
      const res = await pergunta('wa-confirmar', { cpf, numero, codigo });
      if (res.ok && res.validado) {
        msg.className = 'msg good';
        msg.textContent = 'Número verificado ✓';
        ultimoCheque = '';
        checar();
        setTimeout(() => m.remove(), 1200);
      } else {
        msg.className = 'msg err';
        msg.textContent = res.erro || 'Código incorreto.';
      }
    });
    document.documentElement.append(m);
  }

  // -------------------------------------------------------------------- gatilhos
  // A tela é AJAX (Telerik): o campo aparece/reaparece. Observa e rechecа, com folga.
  let agendado = null;
  function agendarCheque() {
    clearTimeout(agendado);
    agendado = setTimeout(checar, 600);
  }
  const obs = new MutationObserver(agendarCheque);
  obs.observe(document.documentElement, { childList: true, subtree: true });
  document.addEventListener('change', (e) => {
    const id = e.target?.id || '';
    if (id.endsWith('_txtCelular') || id.endsWith('_txtCPF')) {
      ultimoCheque = '';
      agendarCheque();
    }
  }, true);
  agendarCheque();
})();
