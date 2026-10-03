// Cérebro da extensão (service worker). Faz o que a página do SISREG não pode:
//  - guarda a sessão do SMSMais (login), lida do painel smsmarica.online;
//  - observa o tráfego do SISREG (webRequest) e recebe respostas do content.js;
//  - envia as capturas em lote para a NOSSA API (aqui não há trava de CSP nem CORS);
//  - mantém o estado do "LED" (conectado / enviando / recebido / offline) e o difunde.
//
// Só observa o SISREG: nunca dispara requisição para lá.

import { CONFIG, ROTA_CAPTURAS, SITIOS } from './config.js';
import { ENDPOINTS, ETAPAS, CAMPOS_SENSIVEIS } from './endpoints.js';

// Observa TODOS os sítios configurados (SISREG, Ecossistemas, …). Cada captura leva o `sitio`.
const FILTRO = { urls: SITIOS.map((s) => `*://${s.host}/*`) };
const TIPOS = new Set(['main_frame', 'sub_frame', 'xmlhttprequest', 'other']);

// Descobre de qual sítio é uma URL (para carimbar a origem da captura).
function sitioDeUrlObj(url) {
  try {
    const host = new URL(url).host;
    return SITIOS.find((s) => s.host === host) ?? null;
  } catch {
    return null;
  }
}
function sitioDeUrl(url) {
  return sitioDeUrlObj(url)?.id ?? null;
}

const soDigitos = (s) => (s ? String(s).replace(/\D/g, '') : '');

// Nº da solicitação na tela de CONFIRMAÇÃO da marcação (modo mínimo do "agendou").
// A tela tem "Solicita&ccedil;&atilde;o: NNNN". Defensivo: alguns padrões, sem PII.
function numeroDaConfirmacaoMarcar(html) {
  if (!html) return null;
  const texto = html.replace(/&#\d+;|&[a-z]+;/gi, ' '); // entidades HTML → espaço
  const m =
    texto.match(/Solicita\w*\s*[:\-nº.]*\s*(\d{4,})/i) ||
    texto.match(/N[ºo.]?\s*(?:da\s*)?Solicita\w*\s*[:\-]*\s*(\d{4,})/i);
  return m ? m[1] : null;
}

// MODO MÍNIMO: envia SÓ o comando + o número (sem PII). Mostra na janela de tráfego.
// Dedupe: o mesmo comando+número não repete (o content manda a resposta 2x; ações repetem).
const eventosRecentes = new Map(); // "sitio:comando:numero" -> timestamp
function emitirEvento(tabId, sitioId, comando, numero) {
  const chave = `${sitioId}:${comando}:${numero || '?'}`;
  const agora = Date.now();
  if (eventosRecentes.get(chave) && agora - eventosRecentes.get(chave) < 15000) return;
  eventosRecentes.set(chave, agora);

  const item = {
    kind: 'evento',
    sitio: sitioId,
    comando,
    numero: numero || null,
    quando: new Date().toISOString(),
    operador: operadorPorAba.get(tabId) ?? null,
  };
  empilhar(item);
  talvezEnviar();
  chrome.tabs.sendMessage(tabId, { tipo: 'trafego', item }, { frameId: 0 }).catch(() => {});
}

// ---------------------------------------------------------------- estado vivo
const buffer = []; // capturas ainda não confirmadas pela API
const MAX_BUFFER = 5000; // teto de segurança se a API ficar fora
const pendentesReq = new Map(); // requestId -> item (aguardando status)
const operadorPorAba = new Map(); // tabId -> operador do SISREG logado (carimba as capturas)
let sessao = null; // { token, expiraEm }
let marca = CONFIG.MARCA_PADRAO;
let enviando = false;
let ultimoEnvioOk = 0;
let ultimaFalha = false;

// -------------------------------------------------------------- identificação
async function installId() {
  const r = await chrome.storage.local.get('installId');
  if (r.installId) return r.installId;
  const id = crypto.randomUUID();
  await chrome.storage.local.set({ installId: id });
  return id;
}

// ---------------------------------------------------------------------- sessão
function autenticado() {
  return Boolean(sessao?.token) && new Date(sessao.expiraEm).getTime() > Date.now();
}

// A sessão fica no storage.local (e não no .session): o .session é apagado a cada recarga da
// extensão (toda atualização de versão) e a cada reinício do navegador, e o painel já aberto não
// reenvia o login sozinho — o operador via "Sem sessão no SMSMarica" toda hora (30/09/2026).
// Exposição igual à do próprio painel, que guarda o mesmo token no localStorage; vale até vencer
// (8 h), sai no logout do painel e em qualquer 401 da API.
//
// Guarda-se só o token e o vencimento — nunca o nome de quem logou no painel, que não é
// necessariamente quem está operando o computador (01/10/2026).
const soCredencial = (s) => (s?.token && s?.expiraEm ? { token: s.token, expiraEm: s.expiraEm } : null);

async function definirSessao(nova) {
  sessao = soCredencial(nova);
  await chrome.storage.local.set({ sessao });
  await difundirEstado();
  buscarMarca(); // aproveita para (re)carregar a marca da nossa API
}

async function restaurar() {
  const [l, s] = await Promise.all([chrome.storage.local.get('sessao'), chrome.storage.session.get('marca')]);
  if (l.sessao && new Date(l.sessao.expiraEm).getTime() > Date.now()) {
    sessao = soCredencial(l.sessao);
    // Sessão guardada por versão antiga ainda traz o nome: regrava sem ele.
    if (l.sessao.usuario) await chrome.storage.local.set({ sessao });
  }
  if (s.marca) marca = s.marca;
}

// Recarregar/atualizar a extensão desliga o auth-content.js das abas do painel já abertas (ele só
// volta quando a aba recarrega). Reinjeta nelas, para um login novo no painel chegar aqui sem F5.
async function religarPainelAberto() {
  const abas = await chrome.tabs.query({ url: [`${CONFIG.PAINEL_ORIGIN}/*`] }).catch(() => []);
  for (const aba of abas) {
    chrome.scripting.executeScript({ target: { tabId: aba.id }, files: ['auth-content.js'] }).catch(() => {});
  }
}

// ------------------------------------------------------------------- a marca
async function buscarMarca() {
  try {
    const resp = await fetch(`${CONFIG.API_BASE}/publico/instituicao`);
    if (!resp.ok) return;
    const i = await resp.json();
    marca = {
      nomeCurto: i.nomeCurto || CONFIG.MARCA_PADRAO.nomeCurto,
      corPrimaria: i.corPrimaria || CONFIG.MARCA_PADRAO.corPrimaria,
      logoUrl: i.logoMidiaId ? `${CONFIG.API_BASE}/midias/${i.logoMidiaId}` : null,
    };
    await chrome.storage.session.set({ marca });
    await difundirEstado();
  } catch {
    /* API fora — segue com a marca padrão */
  }
}

// --------------------------------------------------------------- o LED / estado
function estadoAtual() {
  let estado;
  if (!autenticado()) estado = 'desconectado';
  else if (enviando) estado = 'enviando';
  else if (ultimaFalha) estado = 'offline';
  else if (Date.now() - ultimoEnvioOk < 1500) estado = 'recebido';
  else estado = 'conectado';
  // O nome de quem entrou no painel NÃO vai para as páginas (regra do Bernardo, 01/10/2026): a
  // sessão guardada é de quem logou naquele navegador — muitas vezes quem instalou a extensão —
  // e não de quem está operando. Mostrá-lo no selo atribuía o trabalho à pessoa errada.
  return {
    auth: autenticado(),
    estado,
    pendentes: buffer.length,
    marca,
    painelOrigin: CONFIG.PAINEL_ORIGIN,
    sitios: SITIOS, // o content usa para saber o rótulo e se este site tem blur
  };
}

async function difundirEstado() {
  const estado = estadoAtual();
  const abas = await chrome.tabs.query({ url: FILTRO.urls });
  for (const aba of abas) {
    chrome.tabs.sendMessage(aba.id, { tipo: 'estado', estado }, { frameId: 0 }).catch(() => {});
  }
}

// ----------------------------------------------------------- captura (entrada)
function empilhar(evento) {
  buffer.push(evento);
  if (buffer.length > MAX_BUFFER) buffer.splice(0, buffer.length - MAX_BUFFER);
}

function lerCampos(details) {
  const campos = {};
  try {
    for (const [k, v] of new URL(details.url).searchParams) (campos[k] ??= []).push(v);
  } catch {
    /* url sem query */
  }
  const corpo = details.requestBody;
  if (corpo?.formData) {
    for (const [k, v] of Object.entries(corpo.formData)) campos[k] = v;
  } else if (corpo?.raw?.length) {
    try {
      const texto = corpo.raw.map((p) => (p.bytes ? new TextDecoder().decode(p.bytes) : '')).join('');
      for (const [k, v] of new URLSearchParams(texto)) (campos[k] ??= []).push(v);
    } catch {
      campos['(corpo)'] = ['<não decodificável>'];
    }
  }
  for (const k of Object.keys(campos)) if (CAMPOS_SENSIVEIS.has(k)) campos[k] = ['••••'];
  return campos;
}

function classificar(details) {
  const url = new URL(details.url);
  const endpoint = ENDPOINTS[url.pathname];
  const campos = lerCampos(details);
  const etapa = campos.etapa?.[0] ?? null;
  const gatilho = etapa ? ETAPAS[etapa] ?? null : null;
  return {
    kind: 'requisicao',
    requestId: details.requestId,
    tabId: details.tabId,
    frameId: details.frameId,
    quando: new Date(details.timeStamp).toISOString(),
    metodo: details.method,
    caminho: url.pathname,
    conhecido: Boolean(endpoint),
    nome: endpoint?.nome ?? 'desconhecido',
    etapa,
    evento: gatilho?.evento ?? null,
    escrita: gatilho?.escrita ?? false,
    operador: operadorPorAba.get(details.tabId) ?? null,
    sitio: sitioDeUrl(details.url),
    campos,
    status: null,
  };
}

chrome.webRequest.onBeforeRequest.addListener(
  (details) => {
    if (details.tabId < 0 || !TIPOS.has(details.type)) return;
    const sitio = sitioDeUrlObj(details.url);
    const modo = sitio?.modo ?? 'analise';

    if (modo === 'minimo') {
      // Só comandos de escrita cujo NÚMERO já está no envio (cancelar). O "agendou" tem o número
      // só na resposta — tratado no handler de 'resposta'. Nada de raw sai daqui.
      const campos = lerCampos(details);
      const etapa = campos.etapa?.[0];
      const cfg = etapa ? ETAPAS[etapa] : null;
      if (cfg?.comando && cfg.numeroDe === 'envio') {
        const numero = soDigitos(campos[cfg.campoNumero]?.[0]);
        emitirEvento(details.tabId, sitio.id, cfg.comando, numero);
      }
      return;
    }

    // modo 'analise': captura burra (envio cru).
    const item = classificar(details);
    pendentesReq.set(details.requestId, item);
    empilhar(item);
    chrome.tabs.sendMessage(details.tabId, { tipo: 'requisicao', item }, { frameId: 0 }).catch(() => {});
  },
  FILTRO,
  ['requestBody'],
);

function concluir(details, status) {
  const item = pendentesReq.get(details.requestId);
  if (!item) return;
  pendentesReq.delete(details.requestId);
  item.status = status;
  chrome.tabs.sendMessage(details.tabId, { tipo: 'requisicao', item }, { frameId: 0 }).catch(() => {});
}
chrome.webRequest.onCompleted.addListener((d) => concluir(d, d.statusCode), FILTRO);
chrome.webRequest.onErrorOccurred.addListener((d) => concluir(d, d.error), FILTRO);

// --------------------------------------------------------------- envio em lote
async function gzip(texto) {
  const stream = new Blob([texto]).stream().pipeThrough(new CompressionStream('gzip'));
  return new Response(stream).arrayBuffer();
}

async function enviarLote() {
  if (enviando || !buffer.length || !autenticado()) return;
  enviando = true;
  await difundirEstado();

  // Fatia respeitando o teto de itens e de bytes.
  let corte = 0;
  let bytes = 0;
  for (const ev of buffer) {
    const t = JSON.stringify(ev).length;
    if (corte >= CONFIG.LOTE_MAX_ITENS || (corte > 0 && bytes + t > CONFIG.LOTE_MAX_BYTES)) break;
    bytes += t;
    corte++;
  }
  const lote = buffer.slice(0, corte);
  const corpo = JSON.stringify({
    installId: await installId(),
    versao: chrome.runtime.getManifest().version,
    enviadoEm: new Date().toISOString(),
    itens: lote,
  });

  try {
    const resp = await fetch(`${CONFIG.API_BASE}${ROTA_CAPTURAS}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Encoding': 'gzip',
        Authorization: `Bearer ${sessao.token}`,
      },
      body: await gzip(corpo),
    });
    if (resp.status === 401) {
      await definirSessao(null); // token venceu — volta a exigir login
    } else if (resp.ok) {
      buffer.splice(0, lote.length);
      ultimoEnvioOk = Date.now();
      ultimaFalha = false;
    } else {
      ultimaFalha = true;
    }
  } catch {
    ultimaFalha = true; // API fora do ar — segura no buffer e tenta depois
  } finally {
    enviando = false;
    await difundirEstado();
  }
}

// Gatilho oportunista: enquanto o usuário navega, cada captura tenta esvaziar a fila
// respeitando o intervalo mínimo. O alarme é só o batimento de fundo (o Chrome limita
// alarmes a ~30s, curto demais para o feedback do LED).
let ultimaTentativa = 0;
function talvezEnviar() {
  if (Date.now() - ultimaTentativa < CONFIG.LOTE_INTERVALO_MS) return;
  ultimaTentativa = Date.now();
  enviarLote();
}

chrome.alarms.create('enviar', { periodInMinutes: 0.5 });
chrome.alarms.onAlarm.addListener((a) => {
  if (a.name !== 'enviar') return;
  enviarLote();
  conferirVersaoNoDisco();
});

// ----------------------------------------------------------- recarga automática
// O atualizador do PC troca os arquivos desta pasta e termina pelo manifest.json. A extensão
// carregada sem compactação lê os arquivos do disco a cada pedido, então buscar o próprio
// manifest mostra a versão que está NO DISCO. Diferente da que está rodando: a extensão envia o
// que ainda tem guardado e se recarrega sozinha — o ↻ do chrome://extensions sem ninguém clicar
// (medido em 01/10/2026 no Chrome 154: até 30 s depois da troca, e o manifest novo é relido).
// Exige o "Modo do desenvolvedor" ligado: com ele desligado o Chrome desativa a extensão.
async function conferirVersaoNoDisco() {
  try {
    const resp = await fetch(chrome.runtime.getURL('manifest.json'), { cache: 'no-store' });
    const noDisco = (await resp.json()).version;
    if (!noDisco || noDisco === chrome.runtime.getManifest().version) return;
    await enviarLote().catch(() => {});
    chrome.runtime.reload();
  } catch {
    /* manifest no meio da troca: o próximo batimento confere de novo */
  }
}

// ------------------------------------------------- leitura da API (assistente de agenda)
// O assistente "Agenda SISREG → Prime" (prime-agenda.js) precisa das escalas que o SMSMarica já
// tem. A página do Prime não pode chamar a nossa API (CORS), então o pedido passa por aqui, com o
// login do painel. Só GET, e só as rotas da agenda regulada — nada de escrita por este atalho.
const ROTAS_LEITURA = ['/agenda/'];

async function lerApi(caminho) {
  if (typeof caminho !== 'string' || !ROTAS_LEITURA.some((p) => caminho.startsWith(p))) {
    return { ok: false, erro: 'Rota não permitida para a extensão.' };
  }
  if (!sessao) await restaurar(); // o service worker pode ter acabado de acordar
  if (!autenticado()) return { ok: false, status: 401, erro: 'Sem sessão no SMSMarica: entre no painel.' };
  try {
    const resp = await fetch(`${CONFIG.API_BASE}${caminho}`, {
      headers: { Authorization: `Bearer ${sessao.token}` },
    });
    if (resp.status === 401) {
      await definirSessao(null);
      return { ok: false, status: 401, erro: 'A sessão do SMSMarica venceu: entre no painel de novo.' };
    }
    if (resp.status === 403) {
      return { ok: false, status: 403, erro: 'Seu usuário do SMSMarica não tem acesso ao módulo Agenda.' };
    }
    if (!resp.ok) return { ok: false, status: resp.status, erro: `A API respondeu ${resp.status}.` };
    return { ok: true, dados: await resp.json() };
  } catch (e) {
    return { ok: false, erro: `SMSMarica fora de alcance (${e?.message ?? e}).` };
  }
}

// ------------------------------------------------------------------- mensagens
chrome.runtime.onMessage.addListener((msg, sender, responder) => {
  const tabId = msg.tabId ?? sender.tab?.id;

  if (msg.tipo === 'api-get') {
    lerApi(msg.caminho).then(responder);
    return true; // resposta assíncrona
  }

  if (msg.tipo === 'auth') {
    // Vem do content script em smsmarica.online.
    definirSessao(msg.sessao ?? null);
    responder?.(true);
    return;
  }
  if (msg.tipo === 'estado') {
    // O service worker pode ter acabado de acordar (recarga da página): responder antes de ler a
    // sessão guardada dizia "sem sessão" por engano.
    (sessao ? Promise.resolve() : restaurar()).then(() => responder(estadoAtual()));
    return true;
  }
  if (msg.tipo === 'resposta' || msg.tipo === 'ajax') {
    const sitio = sitioDeUrlObj(sender.url ?? sender.tab?.url);
    const modo = sitio?.modo ?? 'analise';

    if (modo === 'minimo') {
      // Agendar: o número da solicitação só existe na tela de confirmação da marcação.
      // Extrai só o número (nada de HTML/PII sai daqui) e emite o evento mínimo.
      if (
        msg.tipo === 'resposta' &&
        msg.dados?.caminho === '/cgi-bin/marcar' &&
        /Chave de Confirma/i.test(msg.dados?.html ?? '')
      ) {
        emitirEvento(tabId, sitio.id, 'agendou', numeroDaConfirmacaoMarcar(msg.dados.html));
      }
      return; // minimo: nada de raw
    }

    // modo 'analise': captura burra (retorno cru — HTML da tela ou corpo de AJAX).
    empilhar({
      kind: msg.tipo,
      tabId,
      frameId: sender.frameId,
      quando: new Date().toISOString(),
      operador: operadorPorAba.get(tabId) ?? null,
      sitio: sitio?.id ?? null,
      ...msg.dados,
    });
    if (buffer.length >= CONFIG.LOTE_MAX_ITENS) enviarLote();
    else talvezEnviar();
    return;
  }
  if (msg.tipo === 'operador') {
    // O content leu a barra "Operador:" — passa a carimbar as capturas seguintes daquela aba.
    if (msg.operador) operadorPorAba.set(tabId, msg.operador);
    return;
  }
});

chrome.tabs.onRemoved.addListener((tabId) => operadorPorAba.delete(tabId));

chrome.runtime.onInstalled.addListener(() => {
  buscarMarca();
  religarPainelAberto();
});
chrome.runtime.onStartup.addListener(() => {
  restaurar().then(buscarMarca);
});

// Boot do service worker (também quando ele acorda).
restaurar().then(() => {
  buscarMarca();
  difundirEstado();
});
