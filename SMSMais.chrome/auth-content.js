// Roda no painel do SMSMais (smsmarica.online). Lê a sessão que o painel já guarda
// e entrega ao service worker — que é quem sabe se o usuário está logado. Não escreve
// nada no painel; só lê a mesma chave que o próprio painel usa.

(() => {
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

  // Reenvia mesmo sem mudança, como batimento: o service worker pode ter perdido a sessão
  // (restauração atrasada, 401, extensão recarregada) e o painel continua logado. O background
  // ignora a repetição da mesma sessão, então o custo é uma mensagem.
  let ultimo = '';
  let ultimoEnvio = 0;
  const BATIMENTO_MS = 60000;
  function sincronizar() {
    const sessao = lerSessao();
    const assinatura = sessao ? sessao.token : '';
    // Sem sessão só se envia na TRANSIÇÃO (logout de verdade): outros subdomínios
    // (arquivos., secretario.) não têm a chave e, no batimento, deslogariam a extensão.
    const mudou = assinatura !== ultimo;
    if (!mudou && (!sessao || Date.now() - ultimoEnvio < BATIMENTO_MS)) return;
    ultimo = assinatura;
    ultimoEnvio = Date.now();
    try {
      chrome.runtime.sendMessage({ tipo: 'auth', sessao }).catch(() => {});
    } catch {
      /* extensão recarregada: este script ficou órfão; o novo é injetado pelo background */
    }
  }

  sincronizar();
  // Login/logout acontecem depois da carga: reconfere ao voltar o foco e de tempos em tempos.
  window.addEventListener('focus', sincronizar);
  document.addEventListener('visibilitychange', () => !document.hidden && sincronizar());
  window.addEventListener('storage', (e) => e.key === CHAVE && sincronizar());
  setInterval(sincronizar, 15000);
})();
