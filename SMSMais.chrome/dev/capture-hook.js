// Roda no MUNDO DA PÁGINA (world: MAIN), em todos os frames, antes do SISREG usar
// a rede. Intercepta fetch/XHR só para COPIAR a resposta (o SISREG usa sisreg_ajax
// para as listas encadeadas). Não altera nada: repassa exatamente o que a página
// pediu e recebeu, via window.postMessage, para o content.js (mundo isolado) levar
// ao service worker. O webRequest já vê o ENVIO; aqui pegamos o RETORNO.

(() => {
  const MARCA = '__smsmais_hooked__';
  if (window[MARCA]) return;
  window[MARCA] = true;

  const TETO = 2_000_000; // não copiar corpos gigantes

  function anunciar(dados) {
    try {
      window.postMessage({ __smsmaisHook: true, ...dados }, '*');
    } catch {
      /* corpo não serializável — ignora */
    }
  }

  // --- fetch ---
  const fetchOrig = window.fetch;
  if (typeof fetchOrig === 'function') {
    window.fetch = function (...args) {
      const req = args[0];
      const url = typeof req === 'string' ? req : req?.url ?? '';
      const metodo = (typeof req === 'object' && req?.method) || args[1]?.method || 'GET';
      return fetchOrig.apply(this, args).then((resp) => {
        resp
          .clone()
          .text()
          .then((texto) =>
            anunciar({ tipo: 'ajax', via: 'fetch', url, metodo, status: resp.status, corpo: texto.slice(0, TETO) }),
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
  XMLHttpRequest.prototype.send = function () {
    this.addEventListener('load', () => {
      const i = this.__smsmais ?? {};
      let corpo = '';
      try {
        corpo = this.responseType === '' || this.responseType === 'text' ? this.responseText : '';
      } catch {
        /* responseText indisponível para este responseType */
      }
      anunciar({ tipo: 'ajax', via: 'xhr', url: i.url, metodo: i.metodo, status: this.status, corpo: corpo.slice(0, TETO) });
    });
    return enviar.apply(this, arguments);
  };
})();
