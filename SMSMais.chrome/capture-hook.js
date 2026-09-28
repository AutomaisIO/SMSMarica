// Roda no MUNDO DA PÁGINA (world: MAIN), em todos os frames, antes de o sistema observado usar
// a rede. Intercepta fetch/XHR/sendBeacon só para COPIAR a resposta. Não altera nada: repassa
// exatamente o que a página pediu e recebeu, via window.postMessage, para o content.js (mundo
// isolado) levar ao service worker. O webRequest já vê o ENVIO; aqui pegamos o RETORNO.
//
// Também anota o que o OPERADOR fez (clique, __doPostBack, troca de URL sem recarregar). Num
// ASP.NET WebForms tudo vira o mesmo POST para a mesma .aspx: sem saber qual botão foi clicado,
// duas operações diferentes ficam indistinguíveis no acervo.

(() => {
  const MARCA = '__smsmais_hooked__';
  if (window[MARCA]) return;
  window[MARCA] = true;

  const TETO = 4_000_000; // não copiar corpos gigantes

  function anunciar(dados) {
    try {
      window.postMessage({ __smsmaisHook: true, ...dados }, '*');
    } catch {
      /* corpo não serializável — ignora */
    }
  }

  function cortar(texto) {
    const t = texto ?? '';
    return t.length > TETO
      ? { corpo: t.slice(0, TETO), truncado: true, tamanhoOriginal: t.length }
      : { corpo: t, truncado: false, tamanhoOriginal: t.length };
  }

  // --- fetch ---
  const fetchOrig = window.fetch;
  if (typeof fetchOrig === 'function') {
    window.fetch = function (...args) {
      const req = args[0];
      const url = typeof req === 'string' ? req : req?.url ?? '';
      const metodo = (typeof req === 'object' && req?.method) || args[1]?.method || 'GET';
      const envioBruto = args[1]?.body;
      const envio = typeof envioBruto === 'string' ? envioBruto.slice(0, TETO) : null;
      return fetchOrig.apply(this, args).then((resp) => {
        resp
          .clone()
          .text()
          .then((texto) =>
            anunciar({ tipo: 'ajax', via: 'fetch', url, metodo, status: resp.status, envio, ...cortar(texto) }),
          )
          .catch(() => {});
        return resp;
      });
    };
  }

  // --- XMLHttpRequest ---
  const abrir = XMLHttpRequest.prototype.open;
  const enviar = XMLHttpRequest.prototype.send;
  XMLHttpRequest.prototype.open = function (metodo, url) {
    this.__smsmais = { metodo, url };
    return abrir.apply(this, arguments);
  };
  XMLHttpRequest.prototype.send = function (corpoEnvio) {
    // O corpo do ENVIO também é lido aqui, e não só pelo webRequest: o Chrome entrega o corpo
    // urlencoded já decodificado como UTF-8, e o Prime fala latin-1 — os acentos chegam lá
    // corrompidos ("N�mero do prontu�rio") e não há como recuperá-los depois. Aqui o corpo
    // ainda é a string que a página montou, com o acento certo.
    if (typeof corpoEnvio === 'string') {
      this.__smsmaisEnvio = corpoEnvio.length > TETO ? corpoEnvio.slice(0, TETO) : corpoEnvio;
    }
    this.addEventListener('load', () => {
      const i = this.__smsmais ?? {};
      let corpo = '';
      try {
        corpo = this.responseType === '' || this.responseType === 'text' ? this.responseText : '';
      } catch {
        /* responseText indisponível para este responseType */
      }
      anunciar({
        tipo: 'ajax',
        via: 'xhr',
        url: i.url,
        metodo: i.metodo,
        status: this.status,
        tipoResposta: this.responseType || 'text',
        envio: this.__smsmaisEnvio ?? null,
        ...cortar(corpo),
      });
    });
    return enviar.apply(this, arguments);
  };

  // --- sendBeacon (telemetria/keepalive; o webRequest o vê como tipo "ping") ---
  if (typeof navigator.sendBeacon === 'function') {
    const beaconOrig = navigator.sendBeacon.bind(navigator);
    navigator.sendBeacon = function (url, dados) {
      try {
        anunciar({ tipo: 'interacao', acao: 'beacon', url: String(url), dados: typeof dados === 'string' ? dados.slice(0, 20000) : null });
      } catch {
        /* nada a fazer */
      }
      return beaconOrig(url, dados);
    };
  }

  // ---------------------------------------------------- o que o operador fez
  const texto = (el) => (el?.innerText || el?.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 120);

  function descrever(el) {
    if (!el || el.nodeType !== 1) return null;
    return {
      tag: el.tagName?.toLowerCase() ?? null,
      id: el.id || null,
      nome: el.getAttribute?.('name') || null,
      classe: (el.className && typeof el.className === 'string' ? el.className : '').slice(0, 120) || null,
      titulo: el.getAttribute?.('title') || null,
      valor: el.tagName === 'INPUT' || el.tagName === 'BUTTON' ? (el.value ?? null) : null,
      href: el.getAttribute?.('href')?.slice(0, 500) || null,
      rotulo: texto(el) || null,
    };
  }

  // Clique em algo acionável. Capturado na fase de captura para pegar antes de a página
  // cancelar/reescrever o evento.
  document.addEventListener(
    'click',
    (e) => {
      const alvo = e.target?.closest?.('a,button,input,[onclick],[role=button],td,li') ?? e.target;
      anunciar({ tipo: 'interacao', acao: 'clique', url: location.href, elemento: descrever(alvo) });
    },
    true,
  );

  // Submit de formulário (o WebForms usa um só, mas o Telerik cria outros).
  //
  // O corpo vai junto: um submit NORMAL não passa pelo hook de XHR, e o que o webRequest
  // entrega vem decodificado como UTF-8 pelo Chrome — no Prime, que fala latin-1, os acentos
  // chegam destruídos e não há como recuperá-los. Aqui o FormData ainda é o que a página montou.
  document.addEventListener(
    'submit',
    (e) => {
      const f = e.target;
      let corpo = null;
      try {
        if (f && typeof FormData === 'function') {
          const fd = new FormData(f);
          const pares = [];
          for (const [k, v] of fd.entries()) {
            if (typeof v === 'string') pares.push([k, v.length > 20000 ? v.slice(0, 20000) : v]);
            else pares.push([k, `<arquivo ${v?.name ?? ''}>`]);
          }
          corpo = pares;
        }
      } catch {
        /* form sem FormData utilizável */
      }
      anunciar({
        tipo: 'interacao',
        acao: 'submit',
        url: location.href,
        formulario: { nome: f?.name || null, id: f?.id || null, action: f?.action || null, metodo: f?.method || null },
        corpoEnvio: corpo,
      });
    },
    true,
  );

  // __doPostBack é O verbo do ASP.NET WebForms: diz QUAL controle disparou o postback e com que
  // argumento (página da grade, linha selecionada, coluna ordenada). Sem ele, duas operações
  // diferentes viram o mesmo POST para a mesma .aspx e ficam indistinguíveis no acervo.
  //
  // Não dá para interceptar a atribuição com um accessor: o ASP.NET declara `function
  // __doPostBack(...)` no escopo global, e uma declaração de função REDEFINE a propriedade,
  // apagando o accessor em silêncio. Por isso esperamos ela aparecer e a envolvemos.
  function envolverPostBack() {
    const fn = window.__doPostBack;
    if (typeof fn !== 'function' || fn.__smsmaisEnvolvido) return typeof fn === 'function';
    const embrulho = function (alvo, argumento) {
      anunciar({ tipo: 'interacao', acao: 'postback', url: location.href, alvo: alvo ?? null, argumento: argumento ?? null });
      return fn.apply(this, arguments);
    };
    embrulho.__smsmaisEnvolvido = true;
    try {
      window.__doPostBack = embrulho;
      return true;
    } catch {
      return false; // não gravável — segue sem esta anotação
    }
  }
  if (!envolverPostBack()) {
    const inicio = Date.now();
    const relogio = setInterval(() => {
      if (envolverPostBack() || Date.now() - inicio > 20000) clearInterval(relogio);
    }, 150);
    document.addEventListener('DOMContentLoaded', envolverPostBack, { once: true });
    window.addEventListener('load', envolverPostBack, { once: true });
  }

  // Troca de URL sem recarregar (o content.js usa isto para recapturar a tela).
  for (const metodo of ['pushState', 'replaceState']) {
    const orig = history[metodo];
    history[metodo] = function () {
      const r = orig.apply(this, arguments);
      anunciar({ tipo: 'interacao', acao: 'navegacao', via: metodo, url: location.href });
      return r;
    };
  }
  window.addEventListener('popstate', () => anunciar({ tipo: 'interacao', acao: 'navegacao', via: 'popstate', url: location.href }));
  window.addEventListener('hashchange', () => anunciar({ tipo: 'interacao', acao: 'navegacao', via: 'hashchange', url: location.href }));
})();
