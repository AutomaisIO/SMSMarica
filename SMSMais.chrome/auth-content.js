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

  let ultimo = '';
  function sincronizar() {
    const sessao = lerSessao();
    const assinatura = sessao ? sessao.token : '';
    if (assinatura === ultimo) return; // nada mudou
    ultimo = assinatura;
    chrome.runtime.sendMessage({ tipo: 'auth', sessao }).catch(() => {});
  }

  sincronizar();
  // Login/logout acontecem depois da carga: reconfere ao voltar o foco e de tempos em tempos.
  window.addEventListener('focus', sincronizar);
  document.addEventListener('visibilitychange', () => !document.hidden && sincronizar());
  window.addEventListener('storage', (e) => e.key === CHAVE && sincronizar());
  setInterval(sincronizar, 15000);
})();
