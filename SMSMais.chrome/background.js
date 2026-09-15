// Cérebro da extensão (service worker). Faz o que a página do SISREG não pode:
//  - guarda a sessão do SMSMais (login), lida do painel smsmarica.online;
//  - observa o tráfego do SISREG (webRequest) e recebe respostas do content.js;
//  - envia as capturas em lote para a NOSSA API (aqui não há trava de CSP nem CORS);
//  - mantém o estado do "LED" (conectado / enviando / recebido / offline) e o difunde.
//
// Só observa o SISREG: nunca dispara requisição para lá.

import { CONFIG, ROTA_CAPTURAS } from './config.js';
import { ENDPOINTS, ETAPAS, CAMPOS_SENSIVEIS } from './endpoints.js';

const FILTRO = { urls: ['*://sisregiii.saude.gov.br/*'] };
const TIPOS = new Set(['main_frame', 'sub_frame', 'xmlhttprequest', 'other']);

// ---------------------------------------------------------------- estado vivo
const buffer = []; // capturas ainda não confirmadas pela API
const MAX_BUFFER = 5000; // teto de segurança se a API ficar fora
const pendentesReq = new Map(); // requestId -> item (aguardando status)
const operadorPorAba = new Map(); // tabId -> operador do SISREG logado (carimba as capturas)
let sessao = null; // { token, expiraEm, usuario }
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

async function definirSessao(nova) {
  sessao = nova;
  await chrome.storage.session.set({ sessao: nova });
  await difundirEstado();
  buscarMarca(); // aproveita para (re)carregar a marca da nossa API
}

async function restaurar() {
  const r = await chrome.storage.session.get(['sessao', 'marca']);
  if (r.sessao) sessao = r.sessao;
  if (r.marca) marca = r.marca;
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
  return {
    auth: autenticado(),
    estado,
    pendentes: buffer.length,
    usuario: sessao?.usuario?.nome ?? sessao?.usuario?.nomeCompleto ?? null,
    marca,
    painelOrigin: CONFIG.PAINEL_ORIGIN,
  };
}

async function difundirEstado() {
  const estado = estadoAtual();
  const abas = await chrome.tabs.query({ url: '*://sisregiii.saude.gov.br/*' });
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
    campos,
    status: null,
  };
}

chrome.webRequest.onBeforeRequest.addListener(
  (details) => {
    if (details.tabId < 0 || !TIPOS.has(details.type)) return;
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
  if (a.name === 'enviar') enviarLote();
});

// ------------------------------------------------------------------- mensagens
chrome.runtime.onMessage.addListener((msg, sender, responder) => {
  const tabId = msg.tabId ?? sender.tab?.id;

  if (msg.tipo === 'auth') {
    // Vem do content script em smsmarica.online.
    definirSessao(msg.sessao ?? null);
    responder?.(true);
    return;
  }
  if (msg.tipo === 'estado') {
    responder(estadoAtual());
    return true;
  }
  if (msg.tipo === 'resposta' || msg.tipo === 'ajax') {
    // HTML da tela (content.js no load do frame) ou corpo de AJAX (capture-hook).
    empilhar({
      kind: msg.tipo,
      tabId,
      frameId: sender.frameId,
      quando: new Date().toISOString(),
      operador: operadorPorAba.get(tabId) ?? null,
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
});
chrome.runtime.onStartup.addListener(() => {
  restaurar().then(buscarMarca);
});

// Boot do service worker (também quando ele acorda).
restaurar().then(() => {
  buscarMarca();
  difundirEstado();
});
