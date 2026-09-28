// Cérebro da extensão (service worker). Faz o que a página observada não pode:
//  - guarda a sessão do SMSMais (login), lida do painel smsmarica.online;
//  - observa o tráfego dos sítios configurados (webRequest) e recebe respostas do content.js;
//  - envia as capturas em lote para a NOSSA API (aqui não há trava de CSP nem CORS);
//  - mantém o estado do "LED" (conectado / enviando / recebido / offline) e o difunde.
//
// Só observa: nunca dispara requisição para o sistema observado.

import { CONFIG, ROTA_CAPTURAS, SITIOS } from './config.js';
import { ENDPOINTS, ETAPAS, CAMPOS_SENSIVEIS, PADRAO_CAMPO_SIGILOSO } from './endpoints.js';
import * as Prime from './prime.js';

// Observa TODOS os sítios configurados (SISREG, Prime/Eco, …). Cada captura leva o `sitio`.
const FILTRO = { urls: SITIOS.map((s) => `*://${s.host}/*`) };
// Tipos de recurso capturados. O sítio em captura PROFUNDA (`tudo`) abre a mão: só ficam de
// fora os recursos de enfeite (imagem, fonte, css, mídia), que não dizem nada sobre a
// operação e inundariam a fila.
const TIPOS = new Set(['main_frame', 'sub_frame', 'xmlhttprequest', 'other']);
// `script` ficou de FORA: no Prime eram .axd do Telerik, bootstrap e sweetalert — 39 das
// primeiras 449 linhas do acervo, sem uma informacao sobre a operacao.
const TIPOS_TUDO = new Set([
  'main_frame', 'sub_frame', 'xmlhttprequest', 'other',
  'object', 'ping', 'csp_report', 'websocket', 'webbundle',
]);
// Cabeçalhos que viajam com credencial: guardamos que EXISTIRAM, nunca o valor.
const CABECALHOS_SIGILOSOS = new Set(['cookie', 'set-cookie', 'authorization', 'proxy-authorization']);

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
function modoDe(sitio) {
  return sitio?.modo ?? 'analise';
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
    operador: operadorPorAba.get(tabId)?.operador ?? null,
    unidadeOperador: operadorPorAba.get(tabId)?.unidade ?? null,
  };
  empilhar(item);
  talvezEnviar();
  chrome.tabs.sendMessage(tabId, { tipo: 'trafego', item }, { frameId: 0 }).catch(() => {});
}

// ------------------------------------------------- eventos de negócio do Prime
//
// Aqui a captura deixa de ser burra: o POST vira um evento NOMEADO, com as chaves que o hub
// precisa. O que cada verbo significa está em prime.js; este trecho cuida do que é do
// navegador — de onde vem cada pedaço e como eles se juntam.

// Por aba: o paciente que o operador selecionou (é o que amarra o agendamento a alguém) e o
// evento que está esperando o id do registro criado.
const contextoPorAba = new Map(); // tabId -> { pacienteId, aguardando }
const ESPERA_ID_MS = 30_000; // passado isto, o evento sai sem o id, marcado

function contexto(tabId) {
  let c = contextoPorAba.get(tabId);
  if (!c) contextoPorAba.set(tabId, (c = { pacienteId: null, aguardando: null }));
  return c;
}

// Percent-decode consciente do charset. O Prime é latin-1: `decodeURIComponent` estoura ou
// devolve U+FFFD em "N%FAmero". Tenta UTF-8 (o certo para o resto do mundo) e, se o resultado
// for lixo, relê os mesmos bytes como latin-1 — que é o que a página realmente mandou.
function decodificarValor(bruto) {
  const t = String(bruto).replace(/\+/g, ' ');
  try {
    const utf8 = decodeURIComponent(t);
    if (!utf8.includes('�')) return utf8;
  } catch {
    /* sequência inválida em UTF-8 — cai para latin-1 */
  }
  return t.replace(/%([0-9a-f]{2})/gi, (_, h) => String.fromCharCode(parseInt(h, 16)));
}

// Corpo urlencoded (o que o hook copia do `send()`) → o mesmo formato de `campos` do webRequest.
function camposDoCorpo(texto) {
  if (!texto || typeof texto !== 'string') return null;
  const inicio = texto.trimStart()[0];
  if (inicio === '{' || inicio === '[') return null; // JSON não é formulário
  const campos = {};
  for (const par of texto.split('&')) {
    if (!par) continue;
    const i = par.indexOf('=');
    const chave = decodificarValor(i < 0 ? par : par.slice(0, i));
    const valor = i < 0 ? '' : decodificarValor(par.slice(i + 1));
    (campos[chave] ??= []).push(valor);
  }
  for (const k of Object.keys(campos)) {
    if (CAMPOS_SENSIVEIS.has(k) || PADRAO_CAMPO_SIGILOSO.test(k)) campos[k] = ['••••'];
  }
  return campos;
}

// Põe o evento na fila. Vai com `evento`/`etapa`/`escrita` preenchidos porque no hub essas
// três são COLUNAS (a de evento é indexada) — é o que torna a consulta barata e estável,
// em vez de cavar o jsonb a cada pergunta.
function emitirNegocio(tabId, sitioId, r, extras = {}) {
  const chaves = { ...r.chaves, ...extras.chaves };
  const identidade = chaves.agendaId ?? chaves.pacienteId ?? JSON.stringify(r.dados).slice(0, 80);
  const chaveDedupe = `${sitioId}:${r.evento}:${identidade}`;
  const agora = Date.now();
  if (eventosRecentes.get(chaveDedupe) && agora - eventosRecentes.get(chaveDedupe) < 15000) return;
  eventosRecentes.set(chaveDedupe, agora);

  const item = {
    kind: 'evento',
    sitio: sitioId,
    evento: r.evento,
    etapa: r.etapa,
    escrita: r.escrita,
    verbo: r.verbo,
    caminho: r.caminho ?? null,
    quando: new Date().toISOString(),
    operador: operadorPorAba.get(tabId)?.operador ?? null,
    unidadeOperador: operadorPorAba.get(tabId)?.unidade ?? null,
    ...chaves,
    dados: r.dados,
    ...extras.marcas,
  };
  empilhar(item);
  talvezEnviar();
  chrome.tabs.sendMessage(tabId, { tipo: 'negocio', item }, { frameId: 0 }).catch(() => {});
}

// Um envio (do webRequest ou do corpo do XHR) passa por aqui para virar evento.
function examinarEnvioPrime(tabId, sitioId, caminho, campos, origem) {
  if (!campos) return;
  const c = contexto(tabId);

  // Seleção de paciente não é operação, mas é o que dá o `pacienteId` do agendamento.
  const selecionado = Prime.pacienteSelecionado(campos);
  if (selecionado) c.pacienteId = selecionado;

  const r = Prime.reconhecer(caminho, campos);
  if (!r) return;
  r.caminho = caminho;

  // O agendamento não carrega o paciente no corpo: ele é o que foi selecionado antes na aba.
  if (r.evento === 'paciente-agendado') r.chaves.pacienteId = c.pacienteId ?? null;

  if (!r.esperaId) {
    emitirNegocio(tabId, sitioId, r);
    return;
  }

  // O id do registro criado só existe na resposta. Guarda e espera.
  if (c.aguardando && c.aguardando.r.evento === r.evento && origem === 'corpo') {
    // Mesmo evento relido do corpo do XHR: fica com esta versão, que tem o acento certo.
    c.aguardando.r.dados = r.dados;
    return;
  }
  c.aguardando = { r, sitioId, desde: Date.now() };
}

// A resposta chegou: se havia evento esperando um id, é aqui que ele se completa.
function examinarRespostaPrime(tabId, texto) {
  const c = contextoPorAba.get(tabId);
  if (!c?.aguardando) return;
  const id = Prime.idDoPacienteNaResposta(texto);
  if (!id) return;
  const { r, sitioId } = c.aguardando;
  c.aguardando = null;
  c.pacienteId = id; // o recém-criado passa a ser o paciente corrente da aba
  r.chaves.pacienteId = id;
  emitirNegocio(tabId, sitioId, r);
}

// Evento que ficou esperando id tempo demais sai assim mesmo — MARCADO. Perder a operação
// seria pior do que registrá-la sem o id; e o rótulo impede que alguém a leia como completa.
function varrerEsperasPrime() {
  const limite = Date.now() - ESPERA_ID_MS;
  for (const [tabId, c] of contextoPorAba) {
    if (!c.aguardando || c.aguardando.desde > limite) continue;
    const { r, sitioId } = c.aguardando;
    c.aguardando = null;
    emitirNegocio(tabId, sitioId, r, { marcas: { idAusente: true } });
  }
}

// ---------------------------------------------------------------- estado vivo
let buffer = []; // capturas ainda não confirmadas pela API
const pendentesReq = new Map(); // requestId -> item (aguardando a volta: cabeçalhos e status)
// tabId -> { operador, unidade }. PERSISTIDO: o service worker do MV3 dorme e um Map em memória
// morre junto, apagando o carimbo no meio do expediente sem ninguém perceber — a captura continua
// chegando, só que anônima. É o mesmo motivo pelo qual a fila é persistida.
const operadorPorAba = new Map();
let sessao = null; // { token, expiraEm, usuario }
let marca = CONFIG.MARCA_PADRAO;
let enviando = false;
let ultimoEnvioOk = 0;
let ultimaFalha = false;
let restauracao = null; // promessa da leitura do storage (sessão + fila guardada)

// -------------------------------------------------------------- identificação
async function installId() {
  const r = await chrome.storage.local.get('installId');
  if (r.installId) return r.installId;
  const id = crypto.randomUUID();
  await chrome.storage.local.set({ installId: id });
  return id;
}

// ---------------------------------------------------------------------- sessão
// A sessão fica em storage.LOCAL (não `session`): o Chrome apaga o storage de sessão ao fechar
// o navegador, e aí a máquina do piloto amanhecia sem login — capturando para o vazio. Aqui ela
// sobrevive ao reinício e só cai quando o token vence de verdade.
function autenticado() {
  return Boolean(sessao?.token) && new Date(sessao.expiraEm).getTime() > Date.now();
}

async function definirSessao(nova) {
  sessao = nova;
  await chrome.storage.local.set({ sessao: nova });
  await difundirEstado();
  buscarMarca(); // aproveita para (re)carregar a marca da nossa API
}

// Guarda a PROMESSA, não um booleano: quem chama enquanto a leitura ainda está em curso
// precisa esperar por ela. Com um booleano, o segundo chamador seguia em frente e podia gravar
// a fila por cima do que ainda não tinha sido lido — perdendo o acúmulo do desligamento.
function restaurar() {
  restauracao ??= (async () => {
    const r = await chrome.storage.local.get(['sessao', 'marca', 'fila', 'seq', 'operadores']);
    if (r.operadores) for (const [k, v] of Object.entries(r.operadores)) operadorPorAba.set(Number(k), v);
    if (r.sessao) sessao = r.sessao;
    if (r.marca) marca = r.marca;
    if (Number.isFinite(r.seq)) seqAtual = r.seq;
    if (Array.isArray(r.fila) && r.fila.length) {
      buffer = r.fila.concat(buffer);
      tamanhos = buffer.map(estimar);
      bytesFila = tamanhos.reduce((a, b) => a + b, 0);
    }
  })();
  return restauracao;
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
    await chrome.storage.local.set({ marca });
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
    sitios: SITIOS, // o content usa para saber o rótulo, se tem blur e se é captura profunda
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
// A fila é persistida porque o service worker do MV3 é DESLIGADO pelo Chrome quando fica
// ocioso. Sem isso, tudo que estava esperando envio (API fora, sem login) sumia sem aviso.
let salvarAgendado = null;
let salvando = false;
function salvarFila() {
  if (salvarAgendado) return;
  salvarAgendado = setTimeout(async () => {
    salvarAgendado = null;
    if (salvando) return salvarFila();
    salvando = true;
    try {
      await restaurar(); // nunca gravar por cima de uma fila que ainda não foi lida
      await chrome.storage.local.set({ fila: buffer, seq: seqAtual });
    } catch {
      // Cota estourada mesmo assim: descarta a metade mais antiga e tenta de novo.
      descartarMaisAntigos(Math.ceil(buffer.length / 2));
      salvarFila();
    } finally {
      salvando = false;
    }
  }, CONFIG.BUFFER_SALVAR_APOS_MS);
}

// Teto de itens NÃO basta: uma tela do Prime pode ter megabytes, e 5.000 delas encheriam o
// disco (com "unlimitedStorage" não há erro de cota para avisar). Por isso a fila também é
// medida em bytes. O tamanho é estimado no empilhamento — refazer JSON.stringify da fila
// inteira a cada captura sairia caro.
let tamanhos = []; // espelho de `buffer`: tamanho aproximado de cada item
let bytesFila = 0;

function estimar(evento) {
  // O peso está quase todo no conteúdo; o resto é um punhado de campos curtos.
  const grande = (evento.html?.length ?? 0) + (evento.corpo?.length ?? 0);
  return grande ? grande + 2048 : JSON.stringify(evento).length;
}

function descartarMaisAntigos(quantos) {
  const fora = tamanhos.splice(0, quantos);
  buffer.splice(0, quantos);
  for (const t of fora) bytesFila -= t;
  if (bytesFila < 0) bytesFila = 0;
}

// Número de ordem por instalação. O relógio não basta: numa tela de WebForms o clique, o
// postback e a resposta caem TODOS no mesmo segundo, e o `ocorrido_em` empata — reconstruir a
// sequência depois vira adivinhação. Este contador desempata e sobrevive ao desligamento do
// service worker (é persistido junto com a fila).
let seqAtual = 0;

function empilhar(evento) {
  evento.seq = ++seqAtual;
  buffer.push(evento);
  tamanhos.push(estimar(evento));
  bytesFila += tamanhos[tamanhos.length - 1];

  if (buffer.length > CONFIG.BUFFER_MAX_ITENS) {
    descartarMaisAntigos(buffer.length - CONFIG.BUFFER_MAX_ITENS);
  }
  while (bytesFila > CONFIG.BUFFER_MAX_BYTES && buffer.length > 1) {
    descartarMaisAntigos(1);
  }
  salvarFila();
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
      const inicio = texto.trimStart()[0];
      // Postback de WebForms/Telerik vem urlencoded; chamada .asmx vem JSON.
      if (inicio === '{' || inicio === '[') campos['(json)'] = [texto];
      else for (const [k, v] of new URLSearchParams(texto)) (campos[k] ??= []).push(v);
    } catch {
      campos['(corpo)'] = ['<não decodificável>'];
    }
  }
  for (const k of Object.keys(campos)) {
    if (CAMPOS_SENSIVEIS.has(k) || PADRAO_CAMPO_SIGILOSO.test(k)) campos[k] = ['••••'];
  }
  return campos;
}

// Mascara campo sigiloso dentro de um corpo urlencoded (o que o hook copia do `send()`).
// O Prime manda `hidSER2Senha`/`hidCadecoToken` em TODO postback: sem esta passada, o corpo
// do envio entraria no acervo com a credencial de outro sistema em texto claro.
function mascararCorpo(texto) {
  if (!texto || texto.length > 4_000_000) return texto;
  if (texto.trimStart()[0] === '{' || texto.trimStart()[0] === '[') return texto; // JSON: sai inteiro
  return texto.replace(/([^&=?]+)=([^&]*)/g, (inteiro, chave, valor) => {
    let nome = chave;
    try {
      nome = decodeURIComponent(chave.replace(/\+/g, ' '));
    } catch {
      /* chave com %% solto — usa como veio */
    }
    if (!valor) return inteiro; // vazio não é segredo, e some da análise se mascarado
    return CAMPOS_SENSIVEIS.has(nome) || PADRAO_CAMPO_SIGILOSO.test(nome) ? `${chave}=••••` : inteiro;
  });
}

function limparCabecalhos(lista) {
  if (!Array.isArray(lista)) return null;
  const saida = {};
  for (const h of lista) {
    const nome = (h.name || '').toLowerCase();
    saida[nome] = CABECALHOS_SIGILOSOS.has(nome) ? '<omitido>' : (h.value ?? '');
  }
  return saida;
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
    tipoRecurso: details.type,
    url: details.url,
    caminho: url.pathname,
    busca: url.search || null,
    conhecido: Boolean(endpoint),
    nome: endpoint?.nome ?? 'desconhecido',
    etapa,
    evento: gatilho?.evento ?? null,
    escrita: gatilho?.escrita ?? false,
    operador: operadorPorAba.get(details.tabId)?.operador ?? null,
    unidadeOperador: operadorPorAba.get(details.tabId)?.unidade ?? null,
    sitio: sitioDeUrl(details.url),
    campos,
    status: null,
  };
}

chrome.webRequest.onBeforeRequest.addListener(
  (details) => {
    if (details.tabId < 0) return;
    const sitio = sitioDeUrlObj(details.url);
    const tipos = sitio?.tudo ? TIPOS_TUDO : TIPOS;
    if (!tipos.has(details.type)) return;

    if (modoDe(sitio) === 'minimo') {
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

    // modo 'analise': captura burra. A linha só entra na fila quando a requisição TERMINA
    // (onCompleted/onErrorOccurred), para sair inteira: envio + cabeçalhos + 302 + status.
    // Vira evento de negocio nomeado quando o sitio tem reconhecedor (hoje: o Prime).
    if (sitio?.verbos === 'prime') {
      try {
        const url = new URL(details.url);
        examinarEnvioPrime(details.tabId, sitio.id, url.pathname, lerCampos(details), 'webrequest');
      } catch {
        /* reconhecimento nunca pode derrubar a captura */
      }
    }

    const item = classificar(details);
    const anterior = pendentesReq.get(details.requestId);
    if (anterior?.redirecionamentos) item.redirecionamentos = anterior.redirecionamentos; // veio de um 302
    item.registradoEm = Date.now();
    pendentesReq.set(details.requestId, item);
    chrome.tabs.sendMessage(details.tabId, { tipo: 'requisicao', item }, { frameId: 0 }).catch(() => {});
  },
  FILTRO,
  ['requestBody'],
);

// Cabeçalhos de IDA (Referer, X-Requested-With, Content-Type — dizem se é postback, AJAX do
// Telerik ou chamada .asmx). Cookie/Authorization não são guardados.
chrome.webRequest.onSendHeaders.addListener(
  (details) => {
    const item = pendentesReq.get(details.requestId);
    if (item) item.cabecalhosEnvio = limparCabecalhos(details.requestHeaders);
  },
  FILTRO,
  ['requestHeaders'],
);

// Cabeçalhos de VOLTA. Content-Type e Content-Disposition dizem se a resposta é tela, JSON ou
// arquivo que o navegador baixou — e que, portanto, o content.js nunca vai ver.
chrome.webRequest.onHeadersReceived.addListener(
  (details) => {
    const item = pendentesReq.get(details.requestId);
    if (item) {
      item.cabecalhosResposta = limparCabecalhos(details.responseHeaders);
      item.statusResposta = details.statusCode;
    }
  },
  FILTRO,
  ['responseHeaders'],
);

// 302 é invisível para a página e decisivo no Prime (o gate de unidade responde com um Location
// para Default.aspx). O Chrome reaproveita o mesmo requestId no destino, então a cadeia é
// acumulada aqui e reanexada no onBeforeRequest seguinte.
chrome.webRequest.onBeforeRedirect.addListener(
  (details) => {
    const item = pendentesReq.get(details.requestId);
    if (!item) return;
    (item.redirecionamentos ??= []).push({
      de: details.url,
      para: details.redirectUrl,
      status: details.statusCode,
      quando: new Date(details.timeStamp).toISOString(),
    });
  },
  FILTRO,
  ['responseHeaders'],
);

function concluir(details, status) {
  const item = pendentesReq.get(details.requestId);
  if (!item) return;
  pendentesReq.delete(details.requestId);
  item.status = String(status);
  item.doCache = details.fromCache ?? false;
  delete item.registradoEm;
  empilhar(item);
  chrome.tabs.sendMessage(details.tabId, { tipo: 'requisicao', item }, { frameId: 0 }).catch(() => {});
  talvezEnviar();
}
chrome.webRequest.onCompleted.addListener((d) => concluir(d, d.statusCode), FILTRO);
chrome.webRequest.onErrorOccurred.addListener((d) => concluir(d, d.error), FILTRO);

// Requisição que nunca termina (aba fechada no meio, streaming) não pode segurar a captura.
function varrerPendentes() {
  const limite = Date.now() - CONFIG.REQUISICAO_TIMEOUT_MS;
  for (const [id, item] of pendentesReq) {
    if ((item.registradoEm ?? 0) > limite) continue;
    pendentesReq.delete(id);
    item.status = 'sem-conclusao';
    delete item.registradoEm;
    empilhar(item);
  }
}

// ------------------------------------------------------- relatórios que baixam
// No Prime o botão "Gerar relatório" faz window.open de uma página *RPT.aspx que responde com o
// ARQUIVO (CSV/PDF/XLS). O Chrome baixa e a aba nunca renderiza — nenhum content script roda
// ali, então o conteúdo não existe para a extensão. Registramos o que dá para registrar SEM
// pedir nada ao sistema: a URL com todos os parâmetros (é ela que ensina como o relatório é
// montado), o nome do arquivo, o tipo e o tamanho.
chrome.downloads.onCreated.addListener((item) => {
  const sitio = sitioDeUrlObj(item.finalUrl || item.url || '');
  if (!sitio || modoDe(sitio) === 'minimo') return;
  let caminho = null;
  let busca = null;
  try {
    const u = new URL(item.finalUrl || item.url);
    caminho = u.pathname;
    busca = u.search || null;
  } catch {
    /* url fora do padrão */
  }
  empilhar({
    kind: 'download',
    sitio: sitio.id,
    quando: new Date().toISOString(),
    url: item.finalUrl || item.url,
    caminho,
    busca,
    arquivo: item.filename || null,
    mime: item.mime || null,
    tamanho: item.fileSize ?? item.totalBytes ?? null,
    referrer: item.referrer || null,
    observacao: 'conteúdo do arquivo NÃO capturado — a extensão não refaz a requisição',
  });
  talvezEnviar();
});

// --------------------------------------------------------------- envio em lote
async function gzip(texto) {
  const stream = new Blob([texto]).stream().pipeThrough(new CompressionStream('gzip'));
  return new Response(stream).arrayBuffer();
}

// Autentica sozinha com a conta do piloto quando não há sessão do painel. Existe porque, sem o
// blur, numa máquina de consultório ninguém vai abrir o smsmarica.online — e a fila ficaria
// presa. Renova sempre que o token vence (8 h), então não precisa de intervenção diária.
async function garantirSessao() {
  if (autenticado()) return true;
  const { email, senha } = CONFIG.PILOTO || {};
  if (!email || !senha) return false;
  try {
    const r = await fetch(`${CONFIG.API_BASE}/identidade/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, senha }),
    });
    if (!r.ok) return false;
    const d = await r.json();
    if (!d?.token) return false;
    await definirSessao({ token: d.token, expiraEm: d.expiraEm, usuario: d.usuario ?? null });
    return autenticado();
  } catch {
    return false; // API fora — tenta no próximo ciclo
  }
}

async function enviarLote() {
  await restaurar();
  if (enviando || !buffer.length) return;
  if (!autenticado() && !(await garantirSessao())) return;
  enviando = true;
  await difundirEstado();

  // Fatia respeitando o teto de itens e de bytes. Um item sozinho maior que o teto vai assim
  // mesmo (é o caso de uma tela gigante) — melhor um lote grande do que uma fila travada.
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
      await definirSessao(null); // token venceu — volta a exigir login (a fila fica guardada)
    } else if (resp.ok) {
      descartarMaisAntigos(lote.length);
      salvarFila();
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
  if (buffer.length < CONFIG.LOTE_MAX_ITENS && Date.now() - ultimaTentativa < CONFIG.LOTE_INTERVALO_MS) return;
  ultimaTentativa = Date.now();
  enviarLote();
}

chrome.alarms.create('enviar', { periodInMinutes: 0.5 });
chrome.alarms.onAlarm.addListener((a) => {
  if (a.name === 'enviar') {
    varrerPendentes();
    varrerEsperasPrime();
    enviarLote();
  }
});

// ------------------------------------------------------------------- mensagens
// ------------------------------------------------- validação de WhatsApp (nossa API)
// Chama o SMSMarica com o token da sessão da extensão. Só leitura de status + envio/confirmação
// de OTP — tudo na NOSSA API. Se não há sessão, devolve `semSessao` para o content pedir login.
const soDig = (s) => (s ? String(s).replace(/\D/g, '') : '');

async function apiSms(caminho, opcoes = {}) {
  if (!autenticado()) return { _semSessao: true };
  const resp = await fetch(`${CONFIG.API_BASE}${caminho}`, {
    ...opcoes,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${sessao.token}`,
      ...(opcoes.headers || {}),
    },
  });
  if (resp.status === 401) {
    await definirSessao(null);
    return { _semSessao: true };
  }
  return resp;
}

async function tratarWhatsApp(msg) {
  await restaurar();
  const cpf = soDig(msg.cpf);
  if (msg.tipo === 'wa-status') {
    if (!cpf || cpf.length !== 11) return { existe: false, verificado: false };
    const r1 = await apiSms(`/pacientes/por-cpf/${cpf}`);
    if (r1._semSessao) return { semSessao: true };
    if (r1.status === 404) return { existe: false, verificado: false };
    if (!r1.ok) return { erro: `status ${r1.status}` };
    const existencia = await r1.json();
    if (!existencia?.id) return { existe: true, verificado: false };
    const r2 = await apiSms(`/pacientes/${existencia.id}`);
    if (r2._semSessao) return { semSessao: true };
    if (!r2.ok) return { existe: true, verificado: false };
    const p = await r2.json();
    return {
      existe: true,
      verificado: Boolean(p.telefoneVerificado),
      numeroVerificado: p.telefoneVerificado ?? null,
      vinculo: p.telefoneVerificadoVinculo ?? null,
    };
  }
  if (msg.tipo === 'wa-enviar') {
    const r = await apiSms('/telefones/validacao/enviar', {
      method: 'POST',
      body: JSON.stringify({ cpf, numero: msg.numero }),
    });
    if (r._semSessao) return { semSessao: true };
    if (r.ok) return { ok: true, ...(await r.json()) };
    return { erro: (await r.json().catch(() => null))?.detail || `erro ${r.status}` };
  }
  if (msg.tipo === 'wa-confirmar') {
    const r = await apiSms('/telefones/validacao/confirmar', {
      method: 'POST',
      body: JSON.stringify({ cpf, numero: msg.numero, codigo: msg.codigo }),
    });
    if (r._semSessao) return { semSessao: true };
    if (r.ok) return { ok: true, ...(await r.json()) };
    return { erro: (await r.json().catch(() => null))?.detail || `código inválido (${r.status})` };
  }
  return { erro: 'tipo desconhecido' };
}

chrome.runtime.onMessage.addListener((msg, sender, responder) => {
  const tabId = msg.tabId ?? sender.tab?.id;

  if (msg.tipo === 'auth') {
    // Vem do content script em smsmarica.online. ESPERA a restauração: é esta mensagem que
    // acorda o service worker, e o `restaurar()` do boot, terminando depois, gravava por cima a
    // sessão velha (vencida) do storage — o painel logado e a extensão dizendo que não.
    const nova = msg.sessao ?? null;
    restaurar()
      .then(() => {
        // O content repete o envio de tempos em tempos (batimento): mesma sessão, nada a fazer.
        if (nova?.token && nova.token === sessao?.token && autenticado()) return;
        return definirSessao(nova);
      })
      .finally(() => responder?.(true));
    return true;
  }
  if (msg.tipo === 'estado') {
    responder(estadoAtual());
    return true;
  }
  // Validação de WhatsApp na tela do Prime: o content script pede, o service worker fala com a
  // NOSSA API (nunca com o Prime). Sempre com a sessão do SMSMarica que a extensão já guarda.
  if (msg.tipo === 'wa-status' || msg.tipo === 'wa-enviar' || msg.tipo === 'wa-confirmar') {
    tratarWhatsApp(msg).then(responder).catch((e) => responder({ erro: String(e?.message || e) }));
    return true; // resposta assíncrona
  }
  if (msg.tipo === 'resposta' || msg.tipo === 'ajax' || msg.tipo === 'interacao') {
    const sitio = sitioDeUrlObj(sender.url ?? sender.tab?.url);

    if (modoDe(sitio) === 'minimo') {
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

    // Reconhecimento de negócio, antes de tratar o item como matéria bruta.
    if (sitio?.verbos === 'prime') {
      try {
        // O corpo do XHR é a ÚNICA fonte com o acento certo (o webRequest entrega o urlencoded
        // já estragado como UTF-8). Relendo aqui, o nome do paciente sai como a página mandou.
        if (typeof msg.dados?.envio === 'string') {
          const u = new URL(msg.dados.url ?? sender.url, sender.url ?? undefined);
          examinarEnvioPrime(tabId, sitio.id, u.pathname, camposDoCorpo(msg.dados.envio), 'corpo');
        }
        // A resposta é onde nasce o id do paciente recém-criado.
        examinarRespostaPrime(tabId, msg.dados?.corpo ?? msg.dados?.html ?? '');
      } catch {
        /* reconhecimento nunca pode derrubar a captura */
      }
    }

    // modo 'analise': captura burra (retorno cru — HTML da tela, corpo de AJAX, ou a interação
    // do operador que originou tudo: clique, __doPostBack, troca de URL sem recarregar).
    const dados = { ...msg.dados };
    // O corpo do envio vem do mundo da página e NÃO passou pela máscara do webRequest.
    if (typeof dados.envio === 'string') dados.envio = mascararCorpo(dados.envio);
    if (Array.isArray(dados.corpoEnvio)) {
      dados.corpoEnvio = dados.corpoEnvio.map(([k, v]) => {
        const nome = String(k).split('$').pop();
        return [k, CAMPOS_SENSIVEIS.has(nome) || PADRAO_CAMPO_SIGILOSO.test(nome) ? '••••' : v];
      });
    }
    // `caminho` é coluna de consulta no hub; sem isto, ajax/clique/postback ficam com ela vazia
    // e toda pergunta sobre eles exige abrir o payload.
    if (!dados.caminho && dados.url) {
      try {
        const u = new URL(dados.url, sender.url ?? undefined);
        dados.caminho = u.pathname;
        dados.busca ??= u.search || null;
      } catch {
        /* url relativa sem base — deixa como está */
      }
    }

    empilhar({
      kind: msg.tipo === 'interacao' ? (msg.dados?.acao ?? 'interacao') : msg.tipo,
      tabId,
      frameId: sender.frameId,
      quando: new Date().toISOString(),
      operador: operadorPorAba.get(tabId)?.operador ?? null,
    unidadeOperador: operadorPorAba.get(tabId)?.unidade ?? null,
      sitio: sitio?.id ?? null,
      ...dados,
    });
    talvezEnviar();
    return;
  }
  if (msg.tipo === 'operador') {
    // O content leu quem está logado — passa a carimbar as capturas seguintes daquela aba.
    // SISREG: a barra "Operador:". Prime: o CPF do gate de unidade (+ a unidade escolhida).
    if (msg.operador) {
      operadorPorAba.set(tabId, { operador: msg.operador, unidade: msg.unidade ?? null });
      salvarOperadores();
    }
    return;
  }
});

function salvarOperadores() {
  const obj = {};
  for (const [k, v] of operadorPorAba) obj[k] = v;
  chrome.storage.local.set({ operadores: obj }).catch(() => {});
}

chrome.tabs.onRemoved.addListener((tabId) => {
  operadorPorAba.delete(tabId);
  salvarOperadores();
  contextoPorAba.delete(tabId);
});

chrome.runtime.onInstalled.addListener(() => {
  restaurar().then(buscarMarca);
  injetarLeitorDeSessao();
});

// Instalar/atualizar a extensão NÃO injeta content script nas abas já abertas — e o que estava
// nelas ficou órfão. Sem isto, o painel aberto e logado só era "visto" depois de recarregar a aba.
async function injetarLeitorDeSessao() {
  try {
    const abas = await chrome.tabs.query({ url: ['https://smsmarica.online/*', 'https://*.smsmarica.online/*'] });
    for (const aba of abas) {
      chrome.scripting.executeScript({ target: { tabId: aba.id }, files: ['auth-content.js'] }).catch(() => {});
    }
  } catch {
    /* sem abas do painel — segue */
  }
}
chrome.runtime.onStartup.addListener(() => {
  restaurar().then(buscarMarca);
});

// Boot do service worker (também quando ele acorda).
restaurar().then(() => {
  buscarMarca();
  difundirEstado();
  enviarLote(); // acordou com fila guardada do desligamento anterior? manda agora.
});
