// Assistente "Agenda SISREG → Prime" — painel (mundo isolado, só no Prime, frame de cima).
//
// O que resolve: a agenda do Prime é MENSAL por profissional e a recepção a montava à mão a partir
// do SISREG — o que está no Prime hoje NÃO é referência (medido 30/09/2026 no CDT: 28 agendas de
// outubro, 9 profissionais com paciente marcado no SISREG e sem agenda no Prime, novembro inteiro
// por fazer). Daqui para frente a referência é o SISREG.
//
// Duas telas:
//  1. SITUAÇÃO DO MÊS — cruza quem tem escala no SISREG (nossa API) com as agendas do mês no Prime
//     (lidas na tela Consultar Agenda) e mostra quem NÃO está cadastrado, com o botão Cadastrar.
//  2. CADASTRO GUIADO de um profissional — os compromissos que o SISREG pede, cada um marcado
//     "No Prime" (com as datas) ou "Não está no Prime", e o preenchimento da janela um por vez.
//
// Regra: quem grava é o operador. O painel preenche e mostra o que ficou; o operador confere e
// clica "Salvar" na janela do compromisso e, no fim, "Salvar" no rodapé da agenda. Nada de criar
// todas as agendas de uma vez — um profissional, um mês, um compromisso por clique.
//
// Dados: SISREG pela NOSSA API via service worker (mensagem 'api-get'; a página do Prime não fala
// com a API por causa do CORS). Prime: fetch na própria origem (só leitura: pesquisa + expandir).
// Preenchimento: pelo prime-agenda-main.js (mundo da página, onde vivem os objetos Telerik).

(() => {
  if (globalThis.__smsmaisAgenda) return;

  const CHAVE = 'agendaPrime';
  // Versão deste código na página. Recarregar a extensão NÃO troca o código das páginas já abertas:
  // o painel velho segue rodando (e a página do Prime também) até um F5 — foi o que fez um teste
  // de 30/09 rodar a 0.5.21 com a 0.5.22 instalada. O cabeçalho mostra a versão, e o painel avisa
  // quando a extensão foi atualizada por baixo dele.
  const VERSAO = (() => {
    try {
      return chrome.runtime.getManifest().version;
    } catch {
      return '?';
    }
  })();
  const extensaoViva = () => {
    try {
      return !!chrome.runtime?.id;
    } catch {
      return false;
    }
  };
  const PAINEL = 'https://smsmarica.online';
  const DIAS = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb'];
  const MESES = ['JAN', 'FEV', 'MAR', 'ABR', 'MAI', 'JUN', 'JUL', 'AGO', 'SET', 'OUT', 'NOV', 'DEZ'];
  const TIPOS = [
    ['1', 'Consulta'],
    ['4', 'Sala de Procedimento'],
    ['9', 'Exame Imagem'],
    ['10', 'Curativo'],
    ['8', 'Coleta'],
    ['2', 'Consulta Bucal'],
    ['6', 'Atividade em Grupo'],
  ];
  const VALIDADE_CACHE_MS = 6 * 3600e3;
  // Sobe quando a regra de montar a proposta muda: proposta guardada de versão velha é refeita.
  const VERSAO_PROPOSTA = 3; // 3: horário/vagas deixam de ser editáveis (propostas antigas podiam ter edição)

  // ---------------------------------------------------------------- utilidades
  const norm = (s) =>
    String(s ?? '')
      .normalize('NFD')
      .replace(/[̀-ͯ]/g, '')
      .toUpperCase()
      .replace(/\s+/g, ' ')
      .trim();
  const esc = (s) =>
    String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
  const doisDig = (n) => String(n).padStart(2, '0');
  const hm = (s) => (s ? String(s).slice(0, 5) : null);
  const minutos = (h) => {
    const [a, b] = String(h).split(':').map(Number);
    return a * 60 + b;
  };
  const deMinutos = (m) => `${doisDig(Math.floor(m / 60))}:${doisDig(m % 60)}`;
  const diaSemana = (iso) => {
    const [a, m, d] = iso.split('-').map(Number);
    return new Date(a, m - 1, d).getDay();
  };
  const br = (iso) => {
    const [a, m, d] = iso.split('-');
    return `${d}/${m}/${a}`;
  };
  const brCurto = (iso) => br(iso).slice(0, 5);
  const isoDe = (dt) => `${dt.getFullYear()}-${doisDig(dt.getMonth() + 1)}-${doisDig(dt.getDate())}`;
  const somaDias = (iso, n) => {
    const [a, m, d] = iso.split('-').map(Number);
    return isoDe(new Date(a, m - 1, d + n));
  };
  const agoraCurto = (ms) => {
    const h = new Date(ms);
    return `${doisDig(h.getDate())}/${doisDig(h.getMonth() + 1)} ${doisDig(h.getHours())}:${doisDig(h.getMinutes())}`;
  };

  function mesPadrao() {
    const hoje = new Date();
    return isoDe(new Date(hoje.getFullYear(), hoje.getMonth() + 1, 1)).slice(0, 7);
  }
  function intervalo(mes) {
    const [a, m] = mes.split('-').map(Number);
    const ultimo = new Date(a, m, 0).getDate();
    return { a, m, de: `${mes}-01`, ate: `${mes}-${doisDig(ultimo)}`, seguinte: isoDe(new Date(a, m, 1)) };
  }
  const rotuloMes = (mes) => {
    const [a, m] = mes.split('-').map(Number);
    return `${MESES[m - 1]}/${a}`;
  };

  // Tipo do Prime pelo procedimento do SISREG (regra do Bernardo, 30/09/2026): consulta → "Consulta";
  // exame → "Sala de Procedimento". Consulta = CONSULTA… ou OCI… (a OCI é a avaliação com
  // especialista); TODO o resto que o SISREG escala é exame/procedimento. Antes era uma lista de
  // nomes de exame e o que não estava nela caía em Consulta. O operador troca no cartão se precisar.
  // Nome do profissional exatamente como o Prime já escreve (outro compromisso dele no mês) — o do
  // SISREG pode vir diferente. SÓ o nome: o TIPO não se copia do Prime, porque o que está lá foi
  // feito à mão e não é referência (30/09: a colposcopia da Dra. Ana Lucia estava como "Consulta" e
  // a 0.5.27 copiou o erro). O tipo vem do procedimento do SISREG (tipoPadrao) ou da escolha do operador.
  function comoNoPrime() {
    const doPrime = temPrime() && st.prof ? primePorProfissional().get(norm(st.prof.nome)) : null;
    return doPrime ? { nome: doPrime.nome } : null;
  }
  const tipoDe = (c) => c.tipo;
  const rotuloTipo = (v) => TIPOS.find(([k]) => k === v)?.[1] ?? v;

  function tipoPadrao(procedimento) {
    return /CONSULTA|(^|\W)OCI\b/.test(norm(procedimento)) ? '1' : '4';
  }

  // ------------------------------------------------------------------ canais
  async function api(caminho) {
    const r = await chrome.runtime.sendMessage({ tipo: 'api-get', caminho });
    if (!r?.ok) throw new Error(r?.erro || `falha ${r?.status ?? ''}`);
    return r.dados;
  }
  function pagina(cmd, dados, tetoMs = 120000) {
    return new Promise((resolve) => {
      const id = crypto.randomUUID();
      const ouvir = (e) => {
        if (e.source !== window || e.data?.__smsmaisAgendaResp !== true || e.data.id !== id) return;
        window.removeEventListener('message', ouvir);
        clearTimeout(relogio);
        resolve(e.data);
      };
      const relogio = setTimeout(() => {
        window.removeEventListener('message', ouvir);
        resolve({ ok: false, erro: 'A página do Prime não respondeu (ela recarregou?). Tente de novo.' });
      }, tetoMs);
      window.addEventListener('message', ouvir);
      window.postMessage({ __smsmaisAgendaCmd: true, id, cmd, dados }, '*');
    });
  }

  // ------------------------------------------------------------------ estado
  let st = {
    aberto: false,
    mes: mesPadrao(),
    unidade: null, // { id, nome, cnes }
    cacheProf: null, // SISREG: { chave, lista, em }
    cachePrime: null, // Prime: { chave, agendas, falhas, em }
    prof: null, // { cpf, nome } — quando preenchido, a tela é o cadastro guiado
    proposta: null, // { chave, titulo, horaIni, horaFim, compromissos, avisos, em }
  };
  let unidades = null; // [{valor, rotulo}] do /agenda/opcoes
  const progresso = { sisreg: '', prime: '', outro: '' };
  let erroGeral = '';
  let indo = null;
  let modal = null; // { titulo, texto, detalhe } — erro que o operador precisa ver (ver falhar) // selo clicado cuja ida ao Prime ainda está em curso (gira e troca o cursor do painel)
  let auth = null; // estado vindo do service worker
  let ultimoFiltro = '';
  const msgs = {}; // mensagens por cartão (índice) e 'geral'

  const ocupado = () => [progresso.sisreg, progresso.prime, progresso.outro].filter(Boolean).join(' · ');
  const salvar = () => {
    // Com a extensão recarregada por baixo, chrome.* lança na hora ("Extension context invalidated").
    try {
      return chrome.storage.local.set({ [CHAVE]: st }).catch(() => {});
    } catch {
      return Promise.resolve();
    }
  };

  function cabecalhoPrime() {
    const m = (document.body?.innerText ?? '').match(/(\d{7})\s*-\s*([^\n/]+?)\s*\//);
    return m ? { cnes: m[1], nome: m[2].trim() } : null;
  }
  const chaveSisreg = () => `${st.unidade?.id}|${st.mes}`;
  const chavePrime = () => `${cabecalhoPrime()?.cnes ?? st.unidade?.cnes}|${st.mes}`;
  const propostaValida = (cpf) =>
    st.proposta?.versao === VERSAO_PROPOSTA && st.proposta.chave === `${st.unidade?.id}|${st.mes}|${cpf}`;
  const temSisreg = () => st.cacheProf?.chave === chaveSisreg();
  const temPrime = () => st.cachePrime?.chave === chavePrime();

  // ------------------------------------------------------------- SMSMarica
  // A unidade é SEMPRE a do cabeçalho do Prime — não há escolha nem troca no painel (regra do
  // Bernardo, 30/09/2026). Trocou a unidade no Prime, o painel troca junto e volta ao começo.
  // O casamento é pelo nome (o /agenda/opcoes não traz CNES); o CNES do cabeçalho é a chave de "mudou".
  let semUnidade = null; // CNES do cabeçalho que não casou com nenhuma unidade do SISREG
  const unidadeDesatualizada = () => {
    const cab = cabecalhoPrime();
    return !!cab && st.unidade?.cnes !== cab.cnes && semUnidade !== cab.cnes;
  };

  async function resolverUnidade() {
    const cab = cabecalhoPrime();
    if (!cab || st.unidade?.cnes === cab.cnes) return;
    unidades ??= (await api('/agenda/opcoes')).unidades ?? [];
    const alvo = norm(cab.nome);
    const achada =
      unidades.find((u) => norm(u.rotulo) === alvo) ||
      unidades.find((u) => norm(u.rotulo).includes(alvo) || alvo.includes(norm(u.rotulo)));
    st.unidade = achada ? { id: achada.valor, nome: achada.rotulo, cnes: cab.cnes } : null;
    semUnidade = achada ? null : cab.cnes;
    st.prof = null;
    st.proposta = null;
    salvar();
  }

  async function lerSisreg(forcar = false) {
    if (!forcar && temSisreg() && Date.now() - st.cacheProf.em < VALIDADE_CACHE_MS) return;
    const { de, ate } = intervalo(st.mes);
    progresso.sisreg = 'SISREG: lendo as escalas da unidade (≈30 s)…';
    render();
    try {
      const lista = await api(
        `/agenda/ranking?de=${de}&ate=${ate}&unidadeId=${st.unidade.id}&eixo=profissional&limite=300`,
      );
      st.cacheProf = {
        chave: chaveSisreg(),
        em: Date.now(),
        lista: (lista ?? []).map((p) => ({ cpf: p.chave, nome: p.rotulo, vagas: p.vagas, agendados: p.agendados })),
      };
      salvar();
    } finally {
      progresso.sisreg = '';
    }
  }

  // --------------------------------------------------------------- Prime
  // Leitura da tela Agenda → Consultar Agenda, só GET/POST de pesquisa e de "expandir" (o detalhe
  // de cada agenda traz os compromissos com o profissional). Medido 30/09: outubro do CDT = 28
  // agendas em 3 páginas, 50 s. Cada POST usa o formulário da resposta ANTERIOR — pular página a
  // partir de um estado velho volta a grade vazia, sem erro.
  const URL_LISTA = '/Prime/Agendamento/AgendaList.aspx';
  const P = 'ctl00$ctl00$DefaultContent$ChildDefaultContent$';
  const PID = 'ctl00_ctl00_DefaultContent_ChildDefaultContent_';
  const lerHtml = (h) => new DOMParser().parseFromString(h, 'text/html');

  function camposDoForm(doc) {
    const f = doc.querySelector('form');
    const p = new URLSearchParams();
    for (const el of f.elements) {
      if (!el.name || el.disabled) continue;
      const t = (el.type || '').toLowerCase();
      if (['submit', 'image', 'button', 'file', 'reset'].includes(t)) continue;
      if ((t === 'checkbox' || t === 'radio') && !el.checked) continue;
      if (el.tagName === 'SELECT') {
        const o = el.selectedOptions[0] || el.options[0];
        if (o) p.append(el.name, o.value);
        continue;
      }
      p.append(el.name, el.value ?? '');
    }
    return p;
  }
  // Sessão do Prime derrubada: o Prime redireciona para ".../sessaoexpirada" ou "/login.aspx" (medido
  // no lab Automais.prime, client.py). Quem pega este erro recarrega a página e o próprio Prime leva
  // à entrada (ver executar).
  const SessaoPrimeCaiu = class extends Error {};
  const conferirSessao = (r) => {
    if (/sessaoexpirada|\/login\.aspx/i.test(r.url || '')) throw new SessaoPrimeCaiu('A sessão do Prime caiu.');
    return r;
  };

  async function postLista(p) {
    const r = conferirSessao(await fetch(URL_LISTA, { method: 'POST', body: p, credentials: 'same-origin', signal: AbortSignal.timeout(120000) }));
    const h = await r.text();
    if (!/rgrdEventos/.test(h)) throw new Error(`o Prime respondeu sem a grade de agendas (${r.status})`);
    return lerHtml(h);
  }
  const linhasGrade = (doc) => {
    const g = doc.querySelector('[id$="rgrdEventos_ctl00"]');
    return g ? [...g.querySelectorAll(':scope > tbody > tr')] : [];
  };

  // Pesquisa do mês na Consultar Agenda (1 GET + 1 POST). Devolve a 1ª página da grade.
  async function pesquisarMes() {
    const [aa, mm] = st.mes.split('-');
    const doc = lerHtml(await conferirSessao(await fetch(URL_LISTA, { credentials: 'same-origin' })).text());
    if (!doc.querySelector(`[name="${P}rdpPeriodoInicio"]`)) {
      throw new Error('Não consegui abrir Agenda → Consultar Agenda no Prime (a sessão caiu?).');
    }
    const p = camposDoForm(doc);
    for (const k of ['rdpPeriodoInicio', 'rdpPeriodoFim']) {
      p.set(P + k, `${aa}-${mm}-01`);
      p.set(`${P}${k}$dateInput`, `${mm}/${aa}`);
      p.set(
        `${PID}${k}_dateInput_ClientState`,
        JSON.stringify({
          enabled: true,
          emptyMessage: '',
          validationText: `${aa}-${mm}-01-00-00-00`,
          valueAsString: `${aa}-${mm}-01-00-00-00`,
          minDateStr: '1980-01-01-00-00-00',
          maxDateStr: '2099-12-31-00-00-00',
          lastSetTextBoxValue: `${mm}/${aa}`,
        }),
      );
    }
    p.set('__EVENTTARGET', `${P}btnConsultar`);
    p.set('__EVENTARGUMENT', '');
    return postLista(p);
  }

  // Próxima página da grade, a partir do formulário da ÚLTIMA resposta (pular de página a partir de
  // um estado velho volta a grade vazia, sem erro). null = acabou.
  async function proximaPagina(base, ultimo) {
    const atual = Number(base.querySelector('.rgCurrentPage')?.textContent || '1');
    const prox = [...ultimo.querySelectorAll('.rgNumPart a')].find((a) => a.textContent.trim() === String(atual + 1));
    const m = prox && /__doPostBack\('([^']+)'/.exec(prox.getAttribute('href') || '');
    if (!m) return null;
    const q = camposDoForm(ultimo);
    q.set('__EVENTTARGET', m[1]);
    q.set('__EVENTARGUMENT', '');
    return postLista(q);
  }

  const tituloDaLinha = (tr) => tr.querySelectorAll(':scope > td')[2]?.textContent.trim().replace(/\s+/g, ' ') ?? '';
  const linhasDeAgenda = (doc) =>
    linhasGrade(doc).filter((t) => t.querySelector(':scope > td > input.rgExpand, :scope > td > input.rgCollapse'));

  // "Expandir" a linha: o detalhe traz os compromissos, com o profissional de cada um.
  async function expandirLinha(base, tr) {
    const tds = [...tr.querySelectorAll(':scope > td')].map((td) => td.textContent.trim().replace(/\s+/g, ' '));
    const btn = tr.querySelector(':scope > td > input.rgExpand, :scope > td > input.rgCollapse');
    const q = camposDoForm(base);
    q.set('__EVENTTARGET', '');
    q.set('__EVENTARGUMENT', '');
    q.set(btn.name, btn.value);
    const d2 = await postLista(q);
    const trs = linhasGrade(d2);
    // O nome do botão é só letras, dígitos e "$" (ctl00$...$GECBtnExpandColumn): vale entre aspas.
    const i = trs.findIndex((t) => t.querySelector(`:scope > td > input[name="${btn.name}"]`));
    const det = trs[i + 1];
    const comp = det
      ? [...det.querySelectorAll('tr')]
          .map((x) => [...x.querySelectorAll(':scope > td')].map((td) => td.textContent.trim().replace(/\s+/g, ' ')))
          .filter((a) => a.length >= 7 && /\d\d\/\d\d\/\d{4}/.test(a[0]))
      : [];
    return {
      doc: d2,
      agenda: {
        titulo: tds[2],
        mes: tds[3],
        id: idDaLinha(tr),
        compromissos: comp.map((c) => ({
          data: c[0],
          hIni: c[1],
          hFim: c[2],
          tipo: c[3],
          recorrencia: c[4],
          extras: c[5],
          profissional: c[6],
          dias: diasDaRecorrencia(c[4], c[0]),
        })),
      },
    };
  }

  // Varre as agendas do mês e expande as que passam no filtro (por título). `aoLer` avisa o progresso.
  async function varrerMes(filtro, aoLer) {
    let doc = await pesquisarMes();
    const agendas = [];
    const falhas = [];
    for (let pag = 1; doc && pag <= 30; pag++) {
      const base = doc;
      let ultimo = base;
      for (const tr of linhasDeAgenda(base)) {
        if (!filtro(tituloDaLinha(tr))) continue;
        try {
          const r = await expandirLinha(base, tr);
          ultimo = r.doc;
          agendas.push(r.agenda);
        } catch (e) {
          falhas.push(`${tituloDaLinha(tr)} (${e.message})`);
        }
        aoLer?.(agendas.length + falhas.length);
      }
      doc = await proximaPagina(base, ultimo);
    }
    return { agendas, falhas };
  }

  async function lerPrime(forcar = false) {
    if (!forcar && temPrime() && Date.now() - st.cachePrime.em < VALIDADE_CACHE_MS) return;
    progresso.prime = 'Prime: abrindo Consultar Agenda…';
    render();
    try {
      const { agendas, falhas } = await varrerMes(
        () => true,
        (n) => {
          progresso.prime = `Prime: ${n} agenda(s) lida(s)…`;
          render();
        },
      );
      st.cachePrime = { chave: chavePrime(), em: Date.now(), agendas, falhas };
      salvar();
    } finally {
      progresso.prime = '';
    }
  }

  // Reconferência rápida de UM profissional: relê só as agendas dele (e a que tem o título da
  // proposta, se já foi criada) e troca essas na leitura guardada. É o que tira o "falta" depois do
  // Salvar do rodapé sem reler o mês inteiro.
  async function relerPrimeDoProfissional() {
    if (!st.prof) return;
    if (!temPrime()) return lerPrime(true);
    const doPrime = primePorProfissional().get(norm(st.prof.nome));
    const titulos = new Set([...(doPrime?.titulos ?? []), st.proposta?.titulo].filter(Boolean).map(norm));
    progresso.prime = 'Prime: conferindo a agenda deste profissional…';
    render();
    try {
      const { agendas, falhas } = await varrerMes((tit) => titulos.has(norm(tit)));
      // Sai o que havia desses títulos e entra o que acabou de ser lido: cobre agenda nova (criada
      // agora), alterada (compromisso a mais) e excluída (some da leitura).
      st.cachePrime.agendas = [...st.cachePrime.agendas.filter((a) => !titulos.has(norm(a.titulo))), ...agendas];
      if (falhas.length) st.cachePrime.falhas = [...(st.cachePrime.falhas ?? []), ...falhas];
      st.conferidoEm = Date.now();
      limparCobertos();
      salvar();
    } finally {
      progresso.prime = '';
    }
  }

  // O que o Prime agora tem não precisa mais de mensagem nem do selo "falta salvar a agenda".
  function limparCobertos() {
    const pr = st.proposta;
    if (!pr || !st.prof || !temPrime()) return;
    const doPrime = primePorProfissional().get(norm(st.prof.nome));
    pr.compromissos.forEach((c, i) => {
      if (situacaoNoPrime(c, doPrime).estado === 'coberto') {
        delete msgs[i];
        st.soNaTela = (st.soNaTela ?? []).filter((k) => k !== c.chave);
      }
    });
  }

  // "Salvar" clicado no Prime (a página e o painel dividem o DOM): quando o Prime termina, o painel
  // relê a agenda do profissional e o cartão se atualiza sozinho (pedido do Bernardo, 30/09).
  // Dois "Salvar": o da JANELA do compromisso só põe o compromisso no calendário (ainda não grava —
  // o cartão mostra "Falta salvar a agenda"); o da AGENDA grava. Se o Salvar recarregar a página,
  // a carga seguinte relê (ver iniciar: conferirApos).
  let salvando = false;
  document.addEventListener(
    'click',
    (e) => {
      const el = e.target?.closest?.('input[type=submit], input[type=button], input[type=image], button, a, .RadButton');
      if (!el || host?.contains(el)) return;
      const texto = String(el.value || el.title || el.textContent || '').trim();
      if (!/^salvar$/i.test(texto)) return;
      if (!st.aberto || !st.prof || !propostaValida(st.prof.cpf) || salvando || !extensaoViva()) return;
      const naJanela = !!el.closest('[id$="RadDock1"]');
      const pre = st.preenchido?.cpf === st.prof.cpf && Date.now() - st.preenchido.em < 60 * 60e3 ? st.preenchido : null;
      if (naJanela && pre) st.soNaTela = [...new Set([...(st.soNaTela ?? []), pre.chave])];
      st.conferirApos = { cpf: st.prof.cpf, em: Date.now() };
      salvar();
      if (naJanela) {
        // Nada foi gravado ainda: só o selo "Falta salvar a agenda" no cartão.
        render({ forcar: true });
        return;
      }
      salvando = true;
      pagina('aguardarPrime', {}, 70000)
        .then((r) => {
          if (r.ok && !r.janelaAberta) return executar(relerPrimeDoProfissional);
          render({ forcar: true });
        })
        .finally(() => {
          salvando = false;
        });
    },
    true,
  );

  // O botão "Ver agenda inteira" da linha já traz o endereço da agenda no onclick:
  // WebForm_PostBackOptions(..., "AgendaEdit.aspx?id=<guid>", ...). Abrir por GET funciona.
  function idDaLinha(tr) {
    const oc = tr.querySelector('input[type=image]')?.getAttribute('onclick') || '';
    return /AgendaEdit\.aspx\?id=([0-9a-f-]{36})/i.exec(oc)?.[1] ?? null;
  }

  // Procura no Prime, no mês, uma agenda com este título exato e devolve o id. Serve para não criar
  // agenda em dobro quando o operador acabou de salvar uma e a leitura guardada ficou velha.
  async function acharAgendaPorTitulo(titulo) {
    let doc = await pesquisarMes();
    for (let pag = 1; doc && pag <= 30; pag++) {
      const tr = linhasDeAgenda(doc).find((t) => norm(tituloDaLinha(t)) === norm(titulo));
      if (tr) return idDaLinha(tr);
      doc = await proximaPagina(doc, doc);
    }
    return null;
  }

  // "Ocorre semanalmente , a cada 1 semana(s), todo(a) quinta-feira, encerrando em 01/11/2026."
  // Sem repetição ("-"), vale só o dia da data.
  const NOMES_DIA = [['DOMINGO', 0], ['SEGUNDA', 1], ['TERCA', 2], ['QUARTA', 3], ['QUINTA', 4], ['SEXTA', 5], ['SABADO', 6]];
  function diasDaRecorrencia(texto, dataBr) {
    const n = norm(texto);
    if (/DIARIAMENTE/.test(n)) return [1, 2, 3, 4, 5];
    const m = /TODO\(A\) (.*?)(, ENCERRANDO|\.|$)/.exec(n);
    if (m) return NOMES_DIA.filter(([k]) => m[1].includes(k)).map(([, v]) => v);
    const p = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(String(dataBr ?? '').trim());
    return p ? [new Date(+p[3], +p[2] - 1, +p[1]).getDay()] : [];
  }

  // Profissional (nome normalizado) -> compromissos dele no Prime no mês, com o título da agenda.
  function primePorProfissional() {
    const mapa = new Map();
    for (const a of st.cachePrime?.agendas ?? []) {
      for (const c of a.compromissos) {
        for (const nome of String(c.profissional || '').split(/\s*,\s*/).filter(Boolean)) {
          const k = norm(nome);
          if (!mapa.has(k)) mapa.set(k, { nome, titulos: new Set(), agendas: [], compromissos: [] });
          const x = mapa.get(k);
          if (!x.titulos.has(a.titulo)) x.agendas.push({ titulo: a.titulo, id: a.id ?? null });
          x.titulos.add(a.titulo);
          x.compromissos.push({ ...c, titulo: a.titulo, agendaId: a.id ?? null });
        }
      }
    }
    return mapa;
  }

  // Um compromisso do SISREG já está no Prime? Coberto = mesmo profissional, todos os dias e o
  // horário dentro de um compromisso do Prime. Parecido = cruza dia e horário, mas não cobre.
  // Por DIA DA SEMANA: o dia está coberto se algum compromisso do Prime (do mesmo profissional)
  // naquele dia contém o horário do SISREG. Caso medido 30/09: Alberto tem no Prime um bloco grande
  // "qui 08:00–12:00" feito à mão — o ECG "seg–qui 08:00–08:20" do SISREG está coberto só na quinta;
  // criar seg–qui duplicaria a quinta. Por isso o preenchimento vai só nos dias que faltam.
  function situacaoNoPrime(c, doPrime) {
    const lista = doPrime?.compromissos ?? [];
    const cobreDia = (d) => lista.find((pc) => pc.dias.includes(d) && pc.hIni <= c.hIni && pc.hFim >= c.hFim);
    const cobertos = c.dias.filter((d) => cobreDia(d));
    const faltando = c.dias.filter((d) => !cobreDia(d));
    // Cada data leva junto a agenda do Prime que a tem (o selo da data abre essa agenda).
    const agenda = (pc) => ({ titulo: pc.titulo, id: pc.agendaId });
    const porData = new Map();
    for (const d of cobertos) {
      const pc = cobreDia(d);
      for (const x of datasNoPrime(pc)) if (diaSemana(x) === d && !porData.has(x)) porData.set(x, agenda(pc));
    }
    const datas = [...porData.keys()].sort().map((d) => ({ d, ...porData.get(d) }));
    // Coberto por um bloco MAIOR que o do SISREG (feito à mão): mostra qual, para a divergência
    // ficar à vista — quem corrige é o SISREG ou o Prime, não a extensão.
    // Tipo diferente do que o procedimento pede (ex.: exame cadastrado como "Consulta") também.
    const dentro = new Map();
    for (const d of cobertos) {
      const pc = cobreDia(d);
      const outroTipo = norm(pc.tipo) !== norm(rotuloTipo(c.tipo));
      if (pc.hIni !== c.hIni || pc.hFim !== c.hFim || outroTipo) {
        dentro.set(`${DIAS[d]} ${pc.hIni}–${pc.hFim}${outroTipo ? ` · ${pc.tipo}` : ''}`, agenda(pc));
      }
    }
    // Nos dias que faltam, compromisso do Prime que cruza o horário sem cobrir = sobreposição.
    const sobrepoe = [];
    for (const d of faltando) {
      for (const pc of lista) {
        if (pc.dias.includes(d) && pc.hIni < c.hFim && pc.hFim > c.hIni) sobrepoe.push(`${DIAS[d]} ${pc.hIni}–${pc.hFim}`);
      }
    }
    const estado = !faltando.length ? 'coberto' : cobertos.length ? 'parcial' : 'falta';
    return {
      estado,
      datas,
      faltando,
      dentro: [...dentro].map(([rot, a]) => ({ rot, ...a })),
      sobrepoe: [...new Set(sobrepoe)],
    };
  }

  // Primeira e última data do SISREG no mês, só nos dias da semana pedidos (os que faltam no Prime).
  function faixaSisreg(c, dias) {
    const valem = [];
    for (let d = c.primeira; d <= c.ultima; d = somaDias(d, 1)) {
      if (dias.includes(diaSemana(d)) && !c.faltam.includes(d)) valem.push(d);
    }
    return valem.length ? [valem[0], valem[valem.length - 1]] : null;
  }

  // Datas do mês em que um compromisso do Prime acontece: do início ("data") até "encerrando em"
  // (sem incluir), nos dias da repetição; sem repetição, só a própria data.
  function datasNoPrime(pc) {
    const br2iso = (s) => {
      const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(String(s ?? '').trim());
      return m ? `${m[3]}-${m[2]}-${m[1]}` : null;
    };
    const ini = br2iso(pc.data);
    if (!ini) return [];
    const ate = /ENCERRANDO EM (\d{2}\/\d{2}\/\d{4})/.exec(norm(pc.recorrencia));
    const fim = ate ? br2iso(ate[1]) : somaDias(ini, 1);
    const { de, seguinte } = intervalo(st.mes);
    const out = [];
    for (let d = ini < de ? de : ini; d < fim && d < seguinte; d = somaDias(d, 1)) {
      if (pc.dias.includes(diaSemana(d))) out.push(d);
    }
    return out;
  }

  // ------------------------------------------------------------- proposta
  // Ocupação de cada bloco no dia-exemplo: pela hora do agendamento (e, se dois blocos cobrem a
  // mesma hora, pelo nome do procedimento). Nada do paciente sai daqui — só a contagem.
  function ocupacaoPorBloco(blocos, ocupantes) {
    const cont = blocos.map(() => 0);
    for (const o of ocupantes ?? []) {
      const bruto = String(o.dataAgendadaUtc ?? '');
      const dt = new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(bruto) ? bruto : `${bruto}Z`);
      if (Number.isNaN(dt.getTime())) continue;
      const hora = dt.toLocaleTimeString('pt-BR', { timeZone: 'America/Sao_Paulo', hour: '2-digit', minute: '2-digit' });
      const cand = blocos
        .map((b, i) => ({ b, i }))
        .filter(({ b }) => hm(b.horaInicio) <= hora && hora < hm(b.horaFim));
      if (!cand.length) continue;
      let escolhido = cand[0];
      if (cand.length > 1) {
        const palavras = new Set(norm(o.procedimentoTexto).split(/\W+/).filter((w) => w.length > 3));
        let melhor = -1;
        for (const c of cand) {
          const n = norm(c.b.procedimentoNome).split(/\W+/).filter((w) => palavras.has(w)).length;
          if (n > melhor) [melhor, escolhido] = [n, c];
        }
      }
      cont[escolhido.i]++;
    }
    return cont;
  }

  async function montarProposta() {
    const { de, ate, seguinte } = intervalo(st.mes);
    const u = st.unidade.id;
    const cpf = st.prof.cpf;
    progresso.outro = `Lendo a escala de ${st.prof.nome} em ${rotuloMes(st.mes)}…`;
    render();
    try {
      const pag = await api(`/agenda/dias?de=${de}&ate=${ate}&unidadeId=${u}&profissionalCpf=${cpf}&pagina=0&tamanho=100`);
      const dias = pag?.itens ?? [];
      const comVaga = dias.filter((d) => d.vagas > 0);
      const semEscala = dias.filter((d) => d.vagas === 0 && d.agendados > 0);

      // Dias com a mesma "cara" (dia da semana, faixa, vagas, procedimentos) dividem um dia-exemplo.
      const grupos = new Map();
      for (const d of comVaga) {
        const k = [diaSemana(d.data), d.horaInicio, d.horaFim, d.vagas, d.procedimentos].join('|');
        if (!grupos.has(k)) grupos.set(k, []);
        grupos.get(k).push(d);
      }

      const porBloco = new Map();
      let n = 0;
      for (const lista of grupos.values()) {
        progresso.outro = `Lendo os blocos da escala (${++n} de ${grupos.size})…`;
        render();
        const exemplo = lista[0];
        const det = await api(`/agenda/dia?unidadeId=${u}&profissionalCpf=${cpf}&data=${exemplo.data}`);
        const blocos = det?.blocos ?? [];
        const ocup = ocupacaoPorBloco(blocos, det?.ocupantes);
        blocos.forEach((b, i) => {
          const vagas = (b.vagasPrimeiraVez ?? 0) + (b.vagasRetorno ?? 0) + (b.vagasReserva ?? 0);
          const k = [hm(b.horaInicio), hm(b.horaFim), vagas, b.procedimentoCodigo].join('|');
          if (!porBloco.has(k)) {
            porBloco.set(k, {
              hIni: hm(b.horaInicio),
              hFim: hm(b.horaFim),
              vagas,
              minutos: b.minutosPorVagaEstimado ?? null,
              procedimento: b.procedimentoNome,
              dias: new Set(),
              datas: new Set(),
              exemplos: [],
            });
          }
          const c = porBloco.get(k);
          for (const d of lista) {
            c.dias.add(diaSemana(d.data));
            c.datas.add(d.data);
          }
          c.exemplos.push({ data: exemplo.data, ocupadas: ocup[i], vagas });
        });
      }

      const compromissos = [...porBloco.entries()].map(([k, c]) => {
        const datas = [...c.datas].sort();
        const diasSem = [...c.dias].sort((a, b) => a - b);
        const primeira = datas[0];
        const ultima = datas[datas.length - 1];
        // Datas que a repetição semanal criaria entre a primeira e a última e que a escala não tem.
        const faltam = [];
        for (let d = primeira; d <= ultima; d = somaDias(d, 1)) {
          if (diasSem.includes(diaSemana(d)) && !c.datas.has(d)) faltam.push(d);
        }
        // "Encerra em" do Prime não inclui o dia (outubro inteiro = encerra em 01/11). Só encerra
        // antes quando a escala acaba no meio do mês: a próxima data dos mesmos dias da semana
        // depois da última ainda cai no mês e a escala não a tem.
        let proxima = somaDias(ultima, 1);
        while (!diasSem.includes(diaSemana(proxima))) proxima = somaDias(proxima, 1);
        const fim = proxima < seguinte ? somaDias(ultima, 1) : seguinte;
        return {
          chave: `${u}|${st.mes}|${cpf}|${k}|${diasSem.join('')}`,
          hIni: c.hIni,
          hFim: c.hFim,
          vagas: c.vagas,
          qtd: c.vagas,
          minutos: c.minutos,
          procedimento: c.procedimento,
          tipo: tipoPadrao(c.procedimento),
          extras: 0,
          funcao: null,
          dias: diasSem,
          primeira,
          ultima,
          ocorrencias: datas.length,
          faltam,
          fim,
          ocupadas: c.exemplos.reduce((s, e) => s + e.ocupadas, 0),
          vagasExemplo: c.exemplos.reduce((s, e) => s + e.vagas, 0),
          exemplos: c.exemplos.map((e) => brCurto(e.data)),
        };
      });
      compromissos.sort((a, b) => a.dias[0] - b.dias[0] || a.hIni.localeCompare(b.hIni));

      const avisos = [];
      if (!compromissos.length) avisos.push('Nenhuma escala ativa deste profissional nesta unidade no mês.');
      if (semEscala.length) {
        avisos.push(
          `${semEscala.reduce((s, d) => s + d.agendados, 0)} paciente(s) marcado(s) no SISREG em dia SEM escala: ` +
            semEscala.map((d) => `${DIAS[diaSemana(d.data)]} ${brCurto(d.data)}`).join(', ') +
            '. Esses dias não viram compromisso automaticamente — decida no Prime.',
        );
      }
      const ini = compromissos.length ? Math.min(...compromissos.map((c) => minutos(c.hIni))) : 8 * 60;
      const fimMin = compromissos.length ? Math.max(...compromissos.map((c) => minutos(c.hFim))) : 17 * 60;
      st.proposta = {
        versao: VERSAO_PROPOSTA,
        chave: `${u}|${st.mes}|${cpf}`,
        titulo: `${st.prof.nome} - ${rotuloMes(st.mes)}`,
        horaIni: deMinutos(Math.min(Math.floor(ini / 60) * 60, 8 * 60)),
        horaFim: deMinutos(Math.max(Math.ceil(fimMin / 60) * 60, 17 * 60)),
        compromissos,
        avisos,
        em: Date.now(),
      };
      salvar();
    } finally {
      progresso.outro = '';
    }
  }

  // ------------------------------------------------------------------ ações
  async function executar(fn) {
    erroGeral = '';
    try {
      await fn();
    } catch (e) {
      // Sessão do Prime caída ou extensão recarregada por baixo: a página recarrega (o Prime leva
      // à entrada; o painel volta com o código novo). Pedido do Bernardo, 01/10/2026.
      if (e instanceof SessaoPrimeCaiu || !extensaoViva()) return recarregarPagina();
      erroGeral = e?.message ?? String(e);
    } finally {
      render();
    }
  }

  // Recarrega a página do Prime. Com a janela de um compromisso aberta, pergunta antes: o que não
  // foi salvo nela se perde.
  async function recarregarPagina() {
    const t = await pagina('estado', {}, 3000);
    if (!t.janela) {
      location.reload();
      return;
    }
    modal = {
      titulo: 'Agenda SISREG → Prime',
      texto: 'A página do Prime precisa ser recarregada. A janela do compromisso está aberta: o que não foi salvo nela se perde.',
      detalhe: null,
      acoes: [{ rotulo: 'Recarregar agora', acao: 'recarregar', i: '' }],
    };
    render({ forcar: true });
  }

  // SISREG e Prime ao mesmo tempo (servidores diferentes). O que falhar aparece no erro geral.
  function comparar(forcar = false) {
    return executar(async () => {
      await resolverUnidade();
      if (!st.unidade) throw new Error('Esta unidade do Prime não tem escala do SISREG.');
      const r = await Promise.allSettled([lerSisreg(forcar), lerPrime(forcar)]);
      const falha = r.find((x) => x.status === 'rejected');
      if (falha) throw falha.reason;
    });
  }

  // Primeiro dia em que o compromisso vale, a partir de hoje (dia que já passou fica bloqueado no
  // calendário do Prime). Pula os dias em que a escala não vale.
  // Último dia que a repetição cria: o dia anterior ao "Encerra em" que cai nos dias da semana.
  function ultimoDia(c, dias = c.dias) {
    let d = somaDias(c.fim, -1);
    while (d >= c.primeira && !dias.includes(diaSemana(d))) d = somaDias(d, -1);
    return d;
  }

  function inicioUtil(c, dias = c.dias) {
    const hoje = isoDe(new Date());
    let d = c.primeira > hoje ? c.primeira : hoje;
    for (; d < c.fim; d = somaDias(d, 1)) {
      if (dias.includes(diaSemana(d)) && !c.faltam.includes(d)) return d;
    }
    return null;
  }

  // Dias da semana que o "Abrir e preencher" cria: só os que ainda não estão no Prime (os outros
  // duplicariam o que já existe lá). Sem leitura do Prime, todos.
  function diasAPreencher(c) {
    if (!temPrime()) return c.dias;
    const sit = situacaoNoPrime(c, primePorProfissional().get(norm(st.prof.nome)));
    return sit.faltando.length ? sit.faltando : c.dias;
  }

  // ------------------------------------------------ levar a tela até a agenda certa
  const URL_EDICAO = '/Prime/Agendamento/AgendaEdit.aspx';
  const RETOMADA_MS = 3 * 60e3;
  const pausa = (ms) => new Promise((r) => setTimeout(r, ms));

  // Troca de página no Prime: o painel guarda o que estava fazendo e retoma quando a página nova
  // carregar (ver iniciar).
  async function navegar(i, url, texto) {
    st.pendente = { i, chave: st.proposta.chave, em: Date.now() };
    await chrome.storage.local.set({ [CHAVE]: st });
    msgs[i] = { tipo: 'info', texto };
    render({ forcar: true });
    location.href = url;
    return { navegando: true };
  }

  // Deixa a tela do Prime na agenda deste profissional, com o calendário. Se a agenda do mês já
  // existe no Prime, abre ela (não cria outra); se não existe, abre a Nova Agenda, preenche o
  // período do mês, clica Enviar e preenche o título. Nada disso grava — só o Salvar do rodapé.
  async function garantirAgendaNaTela(i, { retomada = false, trocar = false } = {}) {
    const pr = st.proposta;
    let t = await pagina('estado', {}, 8000);
    for (let k = 0; k < 20 && !(t.ok && t.telerik); k++) {
      await pausa(500);
      t = await pagina('estado', {}, 5000);
    }
    if (!t.ok) return { ok: false, erro: 'A página do Prime não respondeu. Recarregue e tente de novo.' };

    const doPrime = temPrime() ? primePorProfissional().get(norm(st.prof.nome)) : null;
    let existente = doPrime?.agendas?.[0] ?? null;
    const tituloDoProf = (tit) =>
      !!tit && (norm(tit) === norm(pr.titulo) || [...(doPrime?.titulos ?? [])].some((x) => norm(x) === norm(tit)));

    if (t.pagina === 'AgendaEdit.aspx' && t.calendario) {
      if ((t.idAgenda && existente?.id === t.idAgenda) || tituloDoProf(t.titulo)) {
        if (!t.titulo) await pagina('titulo', { texto: pr.titulo });
        return { ok: true };
      }
      // Nova Agenda recém-enviada (sem título, sem id) e o profissional não tem agenda no mês.
      if (!t.titulo && !t.idAgenda && !existente) {
        const rt = await pagina('titulo', { texto: pr.titulo });
        return rt.ok ? { ok: true } : rt;
      }
      if (!trocar) {
        return {
          ok: false,
          etapa: 'outra-agenda',
          erro: `Outra agenda aberta no Prime ("${t.titulo || 'sem título'}"). Trocar perde o que não foi salvo nela.`,
        };
      }
    }

    if (t.pagina === 'AgendaEdit.aspx' && t.parametros?.habilitado && !existente) {
      msgs[i] = { tipo: 'info', texto: `Nova Agenda: preenchendo ${rotuloMes(st.mes)} e clicando Enviar…` };
      render();
      const r = await pagina('enviar', { mes: st.mes, horaIni: pr.horaIni, horaFim: pr.horaFim });
      if (!r.ok) return r;
      const rt = await pagina('titulo', { texto: pr.titulo });
      return rt.ok ? { ok: true } : rt;
    }

    if (retomada) return { ok: false, erro: 'Abri a agenda, mas a tela do Prime não ficou como esperado. Clique de novo.' };

    // Ir para a agenda: a do mês que já existe, ou uma com o nosso título (salva há pouco e ainda
    // fora da leitura guardada), ou a Nova Agenda.
    if (existente && !existente.id) existente = { ...existente, id: await acharAgendaPorTitulo(existente.titulo) };
    if (!existente) {
      const id = await acharAgendaPorTitulo(pr.titulo);
      if (id) existente = { titulo: pr.titulo, id };
    }
    if (existente?.id) return navegar(i, `${URL_EDICAO}?id=${existente.id}`, `Abrindo a agenda "${existente.titulo}" no Prime…`);
    return navegar(i, URL_EDICAO, 'Abrindo "Nova Agenda" no Prime…');
  }

  // Falha de um cartão. Se pede decisão do operador (etapa: escolher a função, usar a data da janela,
  // trocar de agenda), fica no cartão com os controles. Erro de verdade vai para um MODAL no meio da
  // tela e sai do cartão (pedido do Bernardo, 30/09: "esse erro deve aparecer em um modal").
  // Confirmações (trocar de agenda, usar a data da janela) também vão ao modal, com os botões;
  // só a escolha da função (um seletor) fica no cartão.
  function falhar(i, texto, extra = {}) {
    if (extra.etapa === 'funcao') {
      msgs[i] = { tipo: 'erro', texto, ...extra };
      render({ forcar: true });
      return;
    }
    delete msgs[i];
    const c = st.proposta?.compromissos[i];
    const acoes = [];
    if (extra.etapa === 'outra-agenda') acoes.push({ rotulo: `Trocar para a agenda de ${st.prof?.nome ?? ''}`, acao: 'preencher-trocar', i });
    if (extra.etapa === 'data') acoes.push({ rotulo: `Usar ${extra.dataEsperada ?? ''} mesmo assim`, acao: 'preencher-mesmo', i });
    modal = {
      titulo: c ? `${c.dias.map((d) => DIAS[d]).join(', ')} · ${c.hIni}–${c.hFim} · ${c.procedimento}` : 'Agenda SISREG → Prime',
      texto,
      detalhe: extra.detalhe ?? null,
      acoes,
    };
    render({ forcar: true });
  }

  // "Abrir e preencher": leva a tela até a agenda certa, vai até o dia, cria o compromisso
  // provisório (Novo Evento), abre a janela dele e preenche. Quem clica Salvar é o operador.
  async function preencherCompromisso(i, { usarDataDoPainel = false, retomada = false, trocar = false } = {}) {
    const c = st.proposta.compromissos[i];
    const dias = diasAPreencher(c);
    const inicio = inicioUtil(c, dias);
    if (!inicio) {
      falhar(i, 'Este compromisso não tem mais nenhum dia neste mês a partir de hoje.');
      return;
    }
    msgs[i] = { tipo: 'info', texto: 'Abrindo…' };
    render();
    const pre = await garantirAgendaNaTela(i, { retomada, trocar });
    if (pre.navegando) return;
    if (!pre.ok) {
      falhar(i, pre.erro, { etapa: pre.etapa });
      return;
    }
    msgs[i] = { tipo: 'info', texto: `Abrindo ${brCurto(inicio)}…` };
    render();
    const aberto = await pagina('abrirCompromisso', { data: br(inicio), hIni: c.hIni });
    if (!aberto.ok) {
      falhar(i, aberto.erro, { detalhe: aberto.detalhe });
      return;
    }
    msgs[i] = { tipo: 'info', texto: 'Preenchendo…' };
    render();
    const r = await pagina('compromisso', {
      dataEsperada: br(inicio),
      usarDataDoPainel,
      hIni: c.hIni,
      hFim: c.hFim,
      qtd: c.vagas, // fiel ao SISREG: vagas e horário não se editam no painel
      tipoQtd: 'U',
      tipo: tipoDe(c),
      extras: c.extras,
      // Nome como o Prime já escreve (outro compromisso dele); o do SISREG vai junto de reserva.
      profissional: comoNoPrime()?.nome ?? st.prof.nome,
      profissionalSisreg: st.prof.nome,
      funcao: c.funcao,
      dias,
      fim: c.fim,
    });
    if (r.ok) {
      // Depois do Salvar do rodapé a página recarrega; o painel reconfere este profissional no Prime
      // nas cargas seguintes (ver iniciar) para trocar o "falta" por "já no Prime" sozinho.
      st.conferirApos = { cpf: st.prof.cpf, em: Date.now() };
      st.preenchido = { cpf: st.prof.cpf, chave: c.chave, em: Date.now() };
      salvar();
      msgs[i] = {
        tipo: r.avisos?.length ? 'aviso' : 'ok',
        // "Encerra em" do Prime não inclui o dia: 01/11 na janela = último atendimento no dia anterior
        // da repetição (30/10). Sem esta linha o operador acha que passou do SISREG (30/09/2026).
        texto:
          `Preenchido — confira, clique Salvar na janela e depois Salvar da agenda (embaixo). "Encerra em ${brCurto(c.fim)}" = último dia ${brCurto(ultimoDia(c, dias))}.` +
          `${r.avisos?.length ? ` ${r.avisos.join(' ')}` : ''}`,
      };
    } else {
      falhar(i, r.erro, r.etapa ? { etapa: r.etapa, opcoes: r.opcoes, dataEsperada: br(inicio) } : { detalhe: r.detalhe });
      return;
    }
    render();
  }

  // ----------------------------------------------------------------- desenho
  let host = null;
  let raiz = null;

  function montarHost() {
    if (host) return;
    host = document.createElement('div');
    host.id = 'smsmais-agenda';
    host.style.cssText = 'all:initial;position:fixed;z-index:2147483646;top:56px;right:14px;bottom:64px;width:430px;';
    raiz = host.attachShadow({ mode: 'open' });
    raiz.addEventListener('click', aoClicar);
    raiz.addEventListener('change', aoMudar);
    raiz.addEventListener('input', aoDigitar);
    raiz.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && modal) {
        modal = null;
        render({ forcar: true });
      }
    });
    // Redesenho adiado (ver render): sai quando o foco deixa o painel. Se o foco foi para outro
    // elemento do painel (ex.: um botão), não redesenha agora — senão o clique cairia num botão
    // que acabou de ser trocado; a própria ação redesenha.
    raiz.addEventListener('focusout', (e) => {
      if (redesenhoPendente && !(e.relatedTarget && raiz.contains(e.relatedTarget))) setTimeout(() => render(), 0);
    });
    document.documentElement.append(host);
  }
  let redesenhoPendente = false;

  const CSS = `
    /* A fonte vai no .p e não no :host: o estilo "all:initial" inline do host vence o :host e o
       painel ficava com a fonte serifada padrão. */
    .p { font: 13px/1.45 system-ui, 'Segoe UI', sans-serif; color: #1f2328; }
    * { box-sizing: border-box; }
    .p { position: absolute; inset: 0; display: flex; flex-direction: column; background: #fff;
      border: 1px solid #d0d7de; border-top: 4px solid #C8102E; border-radius: 12px;
      box-shadow: 0 12px 36px rgba(0,0,0,.22); overflow: hidden; }
    header { display: flex; align-items: center; gap: 8px; padding: 10px 12px; border-bottom: 1px solid #eaeef2; }
    header h1 { font-size: 14px; margin: 0; flex: 1; }
    header h1 .versao { font-size: 10px; font-weight: 400; color: #8c959f; margin-left: 4px; }
    header button { all: unset; cursor: pointer; font-size: 18px; color: #57606a; padding: 0 4px; }
    .corpo { overflow: auto; padding: 10px 12px 16px; flex: 1; }
    .linha { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; margin: 6px 0; }
    h2 { font-size: 12px; text-transform: uppercase; letter-spacing: .4px; color: #57606a; margin: 14px 0 6px; }
    select, input[type=text], input[type=number], input[type=time] { font: inherit; padding: 4px 6px;
      border: 1px solid #d0d7de; border-radius: 6px; background: #fff; }
    input[type=number] { width: 64px; }
    button.b { font: inherit; font-weight: 600; padding: 6px 11px; border-radius: 7px; cursor: pointer;
      border: 1px solid #C8102E; background: #C8102E; color: #fff; }
    button.s { font: inherit; padding: 5px 10px; border-radius: 7px; cursor: pointer;
      border: 1px solid #d0d7de; background: #f6f8fa; color: #1f2328; }
    button.mini { font: inherit; font-size: 12px; font-weight: 600; padding: 3px 9px; border-radius: 6px; cursor: pointer;
      border: 1px solid #C8102E; background: #fff; color: #C8102E; white-space: nowrap; }
    button:disabled { opacity: .5; cursor: default; }
    .dica { color: #57606a; font-size: 12px; }
    header { position: relative; }
    .barra { position: absolute; left: 0; right: 0; bottom: -1px; height: 3px; overflow: hidden; background: #ddf4ff; }
    .barra::after { content: ''; position: absolute; top: 0; bottom: 0; width: 35%; background: #0969da;
      border-radius: 2px; animation: corre 1.1s ease-in-out infinite; }
    @keyframes corre { from { left: -35%; } to { left: 100%; } }
    .status { position: absolute; left: 12px; right: 12px; bottom: 10px; display: flex; gap: 8px; align-items: center;
      background: rgba(31,35,40,.92); color: #fff; font-size: 12px; border-radius: 8px; padding: 7px 10px;
      box-shadow: 0 6px 18px rgba(0,0,0,.25); pointer-events: none; }
    .giro { flex: none; width: 10px; height: 10px; border: 2px solid currentColor; border-right-color: transparent;
      border-radius: 50%; animation: gira .7s linear infinite; }
    .erro { background: #ffebe9; border: 1px solid #ff8182; border-radius: 8px; padding: 7px 9px; }
    .aviso { background: #fff8c5; border: 1px solid #eac54f; border-radius: 8px; padding: 7px 9px; font-size: 12px; margin: 6px 0; }
    .ok { background: #dafbe1; border: 1px solid #4ac26b; border-radius: 8px; padding: 7px 9px; font-size: 12px; }
    .info { background: #ddf4ff; border: 1px solid #54aeff; border-radius: 8px; padding: 7px 9px; font-size: 12px; }
    .resumo { display: grid; grid-template-columns: repeat(3, 1fr); gap: 6px; margin: 8px 0; }
    .resumo div { border: 1px solid #eaeef2; border-radius: 8px; padding: 6px 8px; text-align: center; }
    .resumo b { display: block; font-size: 18px; }
    .resumo .falta b { color: #C8102E; }
    .resumo .tem b { color: #1a7f37; }
    ul.lista { list-style: none; margin: 4px 0 10px; padding: 0; border: 1px solid #eaeef2; border-radius: 8px; }
    ul.lista li { padding: 7px 9px; border-bottom: 1px solid #eaeef2; display: flex; gap: 8px; align-items: center; }
    ul.lista li:last-child { border-bottom: 0; }
    ul.lista .n { flex: 1; min-width: 0; }
    ul.lista .n small { display: block; color: #57606a; font-size: 11px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .marca { font-size: 11px; font-weight: 700; border-radius: 999px; padding: 1px 7px; white-space: nowrap; }
    .marca.falta { background: #ffebe9; color: #C8102E; }
    .marca.tem { background: #dafbe1; color: #1a7f37; }
    .marca.parecido { background: #fff8c5; color: #7d4e00; }
    .cartao { border: 1px solid #d0d7de; border-radius: 10px; padding: 9px 10px; margin: 8px 0; }
    .cartao.noprime { border-color: #4ac26b; background: #f6fff8; }
    .datas { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; margin: 4px 0; }
    .data { font-size: 11px; font-weight: 600; border-radius: 5px; padding: 1px 6px; background: #eaeef2; color: #1f2328; }
    .data.ok { background: #dafbe1; color: #1a7f37; }
    .data.ir { cursor: pointer; }
    .data.ir:hover { text-decoration: underline; }
    /* Selo clicado: gira até o Prime terminar de carregar (a página ou a semana do calendário). */
    .p.carregando, .p.carregando * { cursor: progress !important; }
    .data.carregando { outline: 2px solid #0969da; }
    .data.carregando::before { content: ''; display: inline-block; width: 7px; height: 7px; margin-right: 4px;
      vertical-align: -1px; border: 2px solid currentColor; border-right-color: transparent; border-radius: 50%;
      animation: gira .7s linear infinite; }
    @keyframes gira { to { transform: rotate(360deg); } }
    .fundo { position: fixed; inset: 0; z-index: 2; display: flex; align-items: center; justify-content: center;
      background: rgba(15,18,22,.45); animation: surge .15s ease-out; font: 13px/1.45 system-ui, 'Segoe UI', sans-serif; }
    .modal { width: min(460px, calc(100vw - 32px)); background: #fff; color: #1f2328; border-radius: 12px;
      border-top: 4px solid #C8102E; box-shadow: 0 18px 50px rgba(0,0,0,.35); padding: 16px 18px 14px; }
    .modal-t { font-size: 12px; font-weight: 600; color: #57606a; margin-bottom: 8px; }
    .modal-x { font-size: 14px; font-weight: 600; }
    .modal-d { font-size: 11px; color: #57606a; margin-top: 8px; word-break: break-word; }
    .modal-b { display: flex; justify-content: flex-end; flex-wrap: wrap; gap: 8px; margin-top: 14px; }
    .toast { position: fixed; z-index: 1; pointer-events: none; max-width: 210px; padding: 5px 10px 5px 8px;
      font: 600 12px/1.3 system-ui, 'Segoe UI', sans-serif; color: #fff; background: rgba(31,35,40,.9);
      border-radius: 999px; box-shadow: 0 4px 14px rgba(0,0,0,.22); white-space: nowrap;
      animation: surge .18s ease-out; transition: opacity .35s, transform .35s; }
    .toast::before { content: '✓'; color: #4ac26b; margin-right: 6px; }
    .toast.saindo { opacity: 0; transform: translateY(-6px); }
    @keyframes surge { from { opacity: 0; transform: translateY(4px) scale(.96); } }
    .data.fora { background: #ffebe9; color: #C8102E; }
    .cartao .t { font-weight: 700; }
    .cartao .proc { font-size: 12px; color: #57606a; margin: 2px 0 4px; }
    .zero { color: #b35900; font-weight: 600; }
    .passos { margin: 4px 0 0; padding-left: 18px; font-size: 12px; color: #3d444d; }
    .passos li { margin: 3px 0; }
    label { font-size: 12px; color: #57606a; }
  `;

  // Redesenho de fundo (progresso da leitura) não pode atropelar quem está lendo ou
  // digitando: a rolagem é mantida, e com um campo de cartão em foco o redesenho espera o operador
  // sair do campo. Ações do próprio operador passam { forcar: true }.
  function render({ forcar = false } = {}) {
    if (!st.aberto) {
      host?.remove();
      host = raiz = null;
      return;
    }
    montarHost();
    const foco = raiz.activeElement;
    if (!forcar && foco?.dataset?.campo && foco.dataset.campo !== 'filtro') {
      redesenhoPendente = true;
      return;
    }
    redesenhoPendente = false;
    const rolagem = raiz.querySelector('.corpo')?.scrollTop ?? 0;
    const p = [];
    // Trabalho em curso NÃO entra no fluxo do conteúdo (empurrava os cartões e depois sumia —
    // Bernardo, 30/09): uma barra fina na borda do cabeçalho e, havendo texto, um aviso flutuante
    // no rodapé do painel, por cima.
    const status = ocupado();
    p.push(`<style>${CSS}</style><div class="p ${indo ? 'carregando' : ''}"><header><h1>Agenda SISREG → Prime <span class="versao">v${VERSAO}</span></h1>
      <button data-acao="fechar" title="Fechar">×</button>${status || indo ? '<div class="barra"></div>' : ''}</header><div class="corpo">`);

    if (!extensaoViva()) {
      p.push('<div class="erro">A extensão foi atualizada. Recarregue esta página (F5).</div>');
    } else if (auth && !auth.auth) {
      p.push(`<div class="erro">Sem sessão no SMSMarica. <a href="${PAINEL}" target="_blank" rel="noopener">Entre no painel</a> e volte aqui.</div>`);
    }
    if (erroGeral) p.push(`<div class="erro">${esc(erroGeral)}</div>`);

    // Cadastro só com a unidade casada com a do Prime; se o Prime trocou de unidade, volta à situação.
    if (st.prof && !unidadeDesatualizada() && st.unidade) desenharCadastro(p);
    else desenharSituacao(p);

    p.push('</div>');
    if (status) p.push(`<div class="status"><span class="giro"></span><span>${esc(status)}</span></div>`);
    p.push('</div>');
    if (modal) {
      p.push(`<div class="fundo" data-acao="fechar-modal"><div class="modal" role="alertdialog" aria-modal="true">
        <div class="modal-t">${esc(modal.titulo)}</div>
        <div class="modal-x">${esc(modal.texto)}</div>
        ${modal.detalhe ? `<div class="modal-d">${esc(modal.detalhe)}</div>` : ''}
        <div class="modal-b">${
          modal.acoes?.length
            ? `<button class="s" data-acao="fechar-modal">Cancelar</button>` +
              modal.acoes.map((a) => `<button class="b" data-acao="${a.acao}" data-i="${a.i}">${esc(a.rotulo)}</button>`).join('')
            : '<button class="b" data-acao="fechar-modal">Entendi</button>'
        }</div></div></div>`);
    }
    raiz.innerHTML = p.join('');
    if (toastEl) raiz.append(toastEl);
    if (modal) raiz.querySelector('.modal button.b')?.focus();
    const corpo = raiz.querySelector('.corpo');
    if (corpo) corpo.scrollTop = rolagem;
    const filtroEl = raiz.querySelector('[data-campo=filtro]');
    if (filtroEl && ultimoFiltro) {
      filtroEl.value = ultimoFiltro;
      filtroEl.focus();
      filtroEl.setSelectionRange(ultimoFiltro.length, ultimoFiltro.length);
    }
  }

  // Tela 1: situação do mês (SISREG × Prime).
  function desenharSituacao(p) {
    const cab = cabecalhoPrime();
    p.push(`<div class="dica">Unidade: <b>${esc(cab ? `${cab.cnes} – ${cab.nome}` : 'não identificada')}</b></div>`);
    const meses = [0, 1, 2].map((k) => {
      const h = new Date();
      return isoDe(new Date(h.getFullYear(), h.getMonth() + k, 1)).slice(0, 7);
    });
    p.push(
      `<div class="linha"><label>Mês:</label><select data-campo="mes">` +
        meses.map((m) => `<option value="${m}" ${st.mes === m ? 'selected' : ''}>${rotuloMes(m)}</option>`).join('') +
        '</select></div>',
    );

    if (!st.unidade || (cab && st.unidade.cnes !== cab.cnes)) {
      if (!cab) p.push('<div class="erro">Não achei a unidade no cabeçalho do Prime.</div>');
      else if (semUnidade === cab.cnes) p.push('<div class="erro">Esta unidade do Prime não tem escala do SISREG.</div>');
      return;
    }
    if (!temSisreg() || !temPrime()) {
      p.push(`<button class="b" data-acao="comparar" ${ocupado() ? 'disabled' : ''}>Comparar SISREG × Prime em ${rotuloMes(st.mes)}</button>
        <div class="dica" style="margin-top:6px">Leva ~1 min.</div>`);
      return;
    }

    const doPrime = primePorProfissional();
    const sis = st.cacheProf.lista;
    const nomesSis = new Set(sis.map((x) => norm(x.nome)));
    const faltam = sis.filter((x) => !doPrime.has(norm(x.nome))).sort((a, b) => b.agendados - a.agendados || b.vagas - a.vagas);
    const tem = sis.filter((x) => doPrime.has(norm(x.nome))).sort((a, b) => a.nome.localeCompare(b.nome));
    const soPrime = [...doPrime.values()].filter((x) => !nomesSis.has(norm(x.nome))).sort((a, b) => a.nome.localeCompare(b.nome));
    const faltamComMarcados = faltam.filter((x) => x.agendados > 0).length;
    const filtro = norm(ultimoFiltro);
    const passa = (nome) => !filtro || norm(nome).includes(filtro);

    p.push(`<h2>Situação de ${rotuloMes(st.mes)}</h2>
      <div class="resumo">
        <div><b>${sis.length}</b>com escala no SISREG</div>
        <div class="tem"><b>${tem.length}</b>com agenda no Prime</div>
        <div class="falta"><b>${faltam.length}</b>sem agenda no Prime</div>
      </div>`);
    if (faltamComMarcados) {
      p.push(`<div class="aviso"><b>${faltamComMarcados}</b> sem agenda no Prime já têm paciente marcado no SISREG — comece por eles.</div>`);
    }
    if (st.cachePrime.falhas?.length) {
      p.push(`<div class="aviso">Não consegui ler ${st.cachePrime.falhas.length} agenda(s) do Prime: ${esc(st.cachePrime.falhas.join('; '))}. Atualize a comparação.</div>`);
    }
    p.push(`<div class="linha"><input type="text" data-campo="filtro" placeholder="filtrar por nome" style="flex:1"></div>`);

    p.push(`<h2>Sem agenda no Prime (${faltam.length})</h2>`);
    if (faltam.length) {
      p.push(
        '<ul class="lista">' +
          faltam
            .filter((x) => passa(x.nome))
            .map(
              (x) => `<li><span class="n">${esc(x.nome)}<small>SISREG: ${x.vagas} vagas · ${
                x.agendados ? `<b>${x.agendados} marcados</b>` : '<span class="zero">0 marcados</span>'
              }</small></span><span class="marca falta">falta</span>
              <button class="mini" data-acao="cadastrar" data-cpf="${esc(x.cpf)}">Cadastrar</button></li>`,
            )
            .join('') +
          '</ul>',
      );
    } else p.push('<div class="ok">Todos os profissionais com escala no SISREG têm agenda no Prime neste mês.</div>');

    if (tem.length) {
      p.push(`<h2>Já têm agenda no Prime (${tem.length})</h2><ul class="lista">` +
        tem
          .filter((x) => passa(x.nome))
          .map((x) => {
            const pr = doPrime.get(norm(x.nome));
            return `<li><span class="n">${esc(x.nome)}<small>${esc([...pr.titulos].join(' · '))}</small></span>
              <span class="marca tem">${pr.compromissos.length} compr.</span>
              <button class="mini" data-acao="cadastrar" data-cpf="${esc(x.cpf)}">Conferir</button></li>`;
          })
          .join('') +
        '</ul>');
    }
    if (soPrime.length) {
      p.push(`<h2>Só no Prime, sem escala no SISREG (${soPrime.length})</h2>
        <ul class="lista">` +
        soPrime
          .filter((x) => passa(x.nome))
          .map((x) => `<li><span class="n">${esc(x.nome)}<small>${esc([...x.titulos].join(' · '))}</small></span></li>`)
          .join('') +
        '</ul>');
    }
    p.push(`<div class="dica">SISREG lido em ${agoraCurto(st.cacheProf.em)} · Prime lido em ${agoraCurto(st.cachePrime.em)} ·
      <button class="s" data-acao="atualizar-prime" ${ocupado() ? 'disabled' : ''}>Atualizar</button>
      <button class="s" data-acao="comparar-de-novo" ${ocupado() ? 'disabled' : ''}>Atualizar tudo (SISREG e Prime)</button></div>`);
  }

  // Tela 2: cadastro guiado de um profissional. Objetiva: por compromisso, se está no Prime e em
  // que datas; se não está, o botão. Sem textos de explicação (pedido do Bernardo, 30/09/2026).
  function desenharCadastro(p) {
    const conferido = Math.max(st.cachePrime?.em ?? 0, st.conferidoEm ?? 0);
    p.push(`<div class="linha"><button class="s" data-acao="voltar">←</button>
      <b style="flex:1">${esc(st.prof.nome)} · ${rotuloMes(st.mes)}</b>
      <button class="s" data-acao="conferir-prime" ${ocupado() ? 'disabled' : ''} title="Prime lido em ${conferido ? agoraCurto(conferido) : '—'}">Atualizar</button></div>`);
    const pr = propostaValida(st.prof.cpf) ? st.proposta : null;
    if (!pr) {
      p.push(`<button class="b" data-acao="montar" ${ocupado() ? 'disabled' : ''}>Montar a partir do SISREG</button>`);
      return;
    }
    const doPrime = temPrime() ? primePorProfissional().get(norm(st.prof.nome)) : null;
    const situacoes = pr.compromissos.map((c) => (temPrime() ? situacaoNoPrime(c, doPrime) : null));

    // Título só importa quando a extensão vai criar a agenda (o profissional não tem uma no mês).
    if (!doPrime?.agendas?.length) {
      p.push(`<div class="linha"><label>Título:</label><input type="text" data-campo="titulo" value="${esc(pr.titulo)}" style="flex:1"></div>`);
    }
    if (msgs.geral) p.push(`<div class="${msgs.geral.tipo}">${esc(msgs.geral.texto)}</div>`);
    for (const a of pr.avisos) p.push(`<div class="aviso">${esc(a)}</div>`);
    pr.compromissos.forEach((c, i) => p.push(cartao(c, i, situacoes[i])));
  }

  // Selo que leva à agenda do Prime onde aquilo está (só abre a página; não clica em nada nela).
  const chaveIda = (i, id, titulo, data) => `${i}|${id || titulo}|${data || ''}`;
  const seloAgenda = (i, texto, a, classe, data = '', hIni = '') =>
    `<span class="data ir ${classe} ${indo?.chave === chaveIda(i, a.id, a.titulo, data) ? 'carregando' : ''}" data-acao="ir-agenda"
      data-i="${i}" data-id="${esc(a.id ?? '')}" data-titulo="${esc(a.titulo)}" data-data="${data}" data-hini="${hIni}"
      title="${esc(a.titulo)}">${esc(texto)}</span>`;

  function cartao(c, i, sit) {
    const est = sit?.estado ?? 'falta';
    const faltando = sit?.faltando?.length ? sit.faltando : c.dias;
    const rotDias = (ds) => ds.map((d) => DIAS[d]).join(', ');
    // Salvo na janela do compromisso, mas a agenda ainda não foi salva: está só na tela do Prime.
    const soNaTela = est !== 'coberto' && (st.soNaTela ?? []).includes(c.chave);
    const selo = soNaTela ? '<span class="marca parecido">Falta salvar a agenda</span>' : {
      coberto: '<span class="marca tem">No Prime</span>',
      parcial: `<span class="marca falta">Falta ${rotDias(faltando)}</span>`,
      falta: '<span class="marca falta">Não está no Prime</span>',
    }[est];
    const faixa = faixaSisreg(c, faltando);
    const semEscala = c.faltam.filter((d) => faltando.includes(diaSemana(d)));
    const m = msgs[i];
    const extra = [];
    if (m) {
      extra.push(`<div class="${m.tipo}" style="margin-top:6px">${esc(m.texto)}</div>`);
      // Diagnóstico técnico de falha dentro do Prime (pequeno, para o operador copiar e mandar).
      if (m.detalhe) extra.push(`<div class="dica" style="font-size:11px;margin-top:3px;word-break:break-word">${esc(m.detalhe)}</div>`);
      if (m.etapa === 'funcao' && m.opcoes?.length) {
        extra.push(`<div class="linha"><label>Função:</label><select data-campo="funcao" data-i="${i}">
          <option value="">— escolha —</option>${m.opcoes.map((o) => `<option ${c.funcao === o ? 'selected' : ''}>${esc(o)}</option>`).join('')}</select>
          <button class="s" data-acao="preencher" data-i="${i}">Continuar</button></div>`);
      }
    }
    return `<div class="cartao ${est === 'coberto' ? 'noprime' : ''}">
      <div class="linha" style="margin:0"><span class="t" style="flex:1">${c.dias.map((d) => DIAS[d]).join(', ')} · ${c.hIni}–${c.hFim} · ${c.vagas} vaga(s)</span>${selo}</div>
      <div class="proc">${esc(c.procedimento)}</div>
      ${sit?.datas?.length ? `<div class="datas">${sit.datas.map((x) => seloAgenda(i, brCurto(x.d), x, 'ok', x.d, c.hIni)).join('')}</div>` : ''}
      ${sit?.dentro?.length ? `<div class="datas"><span class="dica">no Prime como</span>${sit.dentro.map((x) => seloAgenda(i, x.rot, x, '')).join('')}</div>` : ''}
      ${est !== 'coberto' && faixa ? `<div class="datas"><span class="dica">SISREG</span><span class="data">${brCurto(faixa[0])}</span>→<span class="data">${brCurto(faixa[1])}</span></div>` : ''}
      ${est !== 'coberto' && semEscala.length ? `<div class="datas"><span class="dica">sem escala:</span>${semEscala.map((d) => `<span class="data fora">${brCurto(d)}</span>`).join('')}</div>` : ''}
      ${est !== 'coberto' && sit?.sobrepoe?.length ? `<div class="datas"><span class="dica">sobrepõe no Prime:</span>${sit.sobrepoe.map((x) => `<span class="data fora">${x}</span>`).join('')}</div>` : ''}
      ${est !== 'coberto' ? `<div class="linha">
        <select data-campo="tipo" data-i="${i}">${TIPOS.map(([v, t]) => `<option value="${v}" ${tipoDe(c) === v ? 'selected' : ''}>${t}</option>`).join('')}</select>
        <label>extras</label><input type="number" min="0" data-campo="extras" data-i="${i}" value="${c.extras}">
        <button class="b" data-acao="preencher" data-i="${i}">Abrir e preencher${est === 'parcial' ? ` (${rotDias(faltando)})` : ''}</button>
      </div>` : ''}
      ${extra.join('')}
    </div>`;
  }

  // ---------------------------------------------------------------- eventos
  function aoClicar(e) {
    const alvo = e.target.closest('[data-acao]');
    if (!alvo) return;
    const acao = alvo.dataset.acao;
    if (acao === 'recarregar') {
      location.reload();
      return;
    }
    // Código velho numa página aberta antes da atualização: não age — recarrega a página.
    if (!extensaoViva() && acao !== 'fechar' && acao !== 'fechar-modal') {
      recarregarPagina();
      return;
    }
    const i = alvo.dataset.i !== undefined ? Number(alvo.dataset.i) : null;

    if (acao === 'fechar-modal') {
      if (e.target.closest('.modal') && !e.target.closest('button')) return;
      modal = null;
      render({ forcar: true });
      return;
    }
    if (acao === 'fechar') {
      st.aberto = false;
      salvar();
      render({ forcar: true });
      return;
    }
    if (acao === 'comparar' || acao === 'comparar-de-novo') {
      comparar(acao === 'comparar-de-novo');
      return;
    }
    if (acao === 'atualizar-prime') {
      executar(() => lerPrime(true));
      return;
    }
    if (acao === 'cadastrar') {
      const x = st.cacheProf.lista.find((y) => y.cpf === alvo.dataset.cpf);
      st.prof = { cpf: x.cpf, nome: x.nome };
      for (const k of Object.keys(msgs)) delete msgs[k];
      salvar();
      if (!propostaValida(x.cpf)) executar(montarProposta);
      else render({ forcar: true });
      return;
    }
    if (acao === 'voltar') {
      st.prof = null;
      salvar();
      render({ forcar: true });
      return;
    }
    if (acao === 'montar') {
      executar(montarProposta);
      return;
    }
    if (acao === 'preencher' || acao === 'preencher-mesmo' || acao === 'preencher-trocar') {
      modal = null;
      preencherCompromisso(i, {
        usarDataDoPainel: acao === 'preencher-mesmo',
        trocar: acao === 'preencher-trocar',
      }).catch((err) => {
        falhar(i, err?.message ?? String(err));
        render({ forcar: true });
      });
      return;
    }
    if (acao === 'conferir-prime') {
      executar(relerPrimeDoProfissional);
      return;
    }
    if (acao === 'ir-agenda') {
      if (indo) return; // uma ida por vez: duas andando as setas do calendário se atropelam
      const { id, titulo, data, hini } = alvo.dataset;
      erroGeral = '';
      indo = { chave: chaveIda(i, id, titulo, data) };
      render({ forcar: true }); // o selo gira já no clique, antes de qualquer espera
      const ponto = { x: e.clientX, y: e.clientY };
      irParaAgenda(id, titulo, data, hini, i)
        .then((r) => {
          if (r?.jaNaTela) mostrarToast(r.jaNaTela, ponto);
          // Trocando de página: continua girando até a página sair (o Prime mostra "carregando" na
          // aba). Se a navegação não acontecer (ex.: o Prime perguntou e o operador ficou), desliga.
          if (r?.navegando) {
            indo.navegando = true;
            setTimeout(() => terminarIda(), 30e3);
          }
        })
        .catch((err) => {
          erroGeral = err?.message ?? String(err);
        })
        .finally(() => {
          if (!indo?.navegando) terminarIda();
        });
    }
  }

  function terminarIda() {
    indo = null;
    render({ forcar: true });
  }

  // Aviso curto junto do ponteiro (ex.: a data clicada já está na tela). Vive fora do conteúdo do
  // painel — o render o recoloca — e some sozinho.
  let toastEl = null;
  function mostrarToast(texto, { x, y }) {
    toastEl?.remove();
    const el = document.createElement('div');
    el.className = 'toast';
    el.textContent = texto;
    el.style.left = `${Math.max(8, Math.min(x + 14, window.innerWidth - 220))}px`;
    el.style.top = `${Math.max(8, y - 34)}px`;
    toastEl = el;
    raiz?.append(el);
    setTimeout(() => el.classList.add('saindo'), 1500);
    setTimeout(() => {
      el.remove();
      if (toastEl === el) toastEl = null;
    }, 1900);
  }

  // Selo de data: abre a agenda do Prime que tem o compromisso e leva o calendário até a semana da
  // data (setas do calendário). Não abre nem cria compromisso. Com a janela de um compromisso
  // aberta, não sai (o que não foi salvo se perderia).
  const IR_PARA_MS = 60e3;
  const agendaNaTela = (id) =>
    location.pathname.toLowerCase() === URL_EDICAO.toLowerCase() &&
    new URLSearchParams(location.search).get('id')?.toLowerCase() === String(id).toLowerCase();

  async function irParaAgenda(id, titulo, data, hIni, i) {
    if (!id) id = await acharAgendaPorTitulo(titulo);
    if (!id) throw new Error(`Não achei a agenda "${titulo}" no Prime.`);
    if (agendaNaTela(id)) {
      if (!data) return { jaNaTela: 'A agenda já está na tela' };
      const r = await irParaDataNaTela(data, hIni);
      return r.jaNaTela ? { jaNaTela: `${brCurto(data)} já está na tela` } : {};
    }
    if (location.pathname.toLowerCase() === URL_EDICAO.toLowerCase()) {
      const t = await pagina('estado', {}, 3000);
      if (t.janela) throw new Error('Feche a janela do compromisso aberta no Prime antes.');
    }
    // A data segue junto: quando a agenda carregar, o painel leva o calendário até ela (ver iniciar).
    st.irPara = data ? { id, data, hIni, i, em: Date.now() } : null;
    await chrome.storage.local.set({ [CHAVE]: st });
    location.href = `${URL_EDICAO}?id=${id}`;
    return { navegando: true };
  }

  // Espera a agenda terminar de carregar e leva o calendário até a data. Quem chama liga/desliga o giro.
  async function irParaDataNaTela(data, hIni) {
    render({ forcar: true });
    let t = await pagina('estado', {}, 5000);
    for (let k = 0; k < 20 && !(t.ok && t.telerik && t.calendario); k++) {
      await pausa(500);
      t = await pagina('estado', {}, 5000);
    }
    const r = await pagina('irParaData', { data: br(data), hIni }, 60000);
    if (!r.ok) throw new Error(r.erro);
    return r;
  }

  function aoMudar(e) {
    const campo = e.target.dataset.campo;
    if (!campo) return;
    const i = e.target.dataset.i !== undefined ? Number(e.target.dataset.i) : null;
    if (campo === 'mes') {
      st.mes = e.target.value;
      st.prof = null;
      salvar();
      render({ forcar: true });
      return;
    }
    if (campo === 'titulo') {
      st.proposta.titulo = e.target.value;
      salvar();
      return;
    }
    // Só o que NÃO vem do SISREG se edita no cartão (tipo, extras, função). Horário e vagas são
    // os do SISREG: se estiverem errados, corrige-se lá (regra do Bernardo, 30/09/2026).
    if (i !== null && st.proposta?.compromissos[i]) {
      const c = st.proposta.compromissos[i];
      if (campo === 'extras') c.extras = Math.max(0, Number(e.target.value) || 0);
      else if (campo === 'tipo' || campo === 'funcao') c[campo] = e.target.value || null;
      salvar();
    }
  }

  function aoDigitar(e) {
    if (e.target.dataset.campo === 'filtro') {
      ultimoFiltro = e.target.value;
      render({ forcar: true });
    }
  }

  // ------------------------------------------------------------------ vida
  chrome.runtime.onMessage.addListener((msg) => {
    // O service worker difunde o estado a cada lote enviado (LED do selo). Aqui só importa se o
    // login do SMSMarica mudou — redesenhar a cada lote fazia o painel voltar ao topo sem parar.
    if (msg.tipo === 'estado') {
      const antes = !!auth?.auth;
      auth = msg.estado;
      if (st.aberto && antes !== !!auth?.auth) render();
    }
  });

  async function iniciar() {
    const guardado = (await chrome.storage.local.get(CHAVE))[CHAVE];
    if (guardado) st = { ...st, ...guardado };
    auth = await chrome.runtime.sendMessage({ tipo: 'estado' }).catch(() => null);
    if (st.aberto) {
      render();
      // Unidade trocada no Prime desde a última página: o painel acompanha (e não retoma nada).
      if (unidadeDesatualizada()) {
        st.pendente = null;
        executar(resolverUnidade);
        return;
      }
    }
    // Selo de data clicado em outra página: a agenda carregou, agora o calendário vai até a data.
    const ir = st.irPara;
    if (ir) {
      st.irPara = null;
      salvar();
      if (Date.now() - ir.em < IR_PARA_MS && agendaNaTela(ir.id)) {
        // O selo segue girando na página nova até o calendário chegar na semana.
        indo = { chave: chaveIda(ir.i, ir.id, null, ir.data) };
        erroGeral = '';
        irParaDataNaTela(ir.data, ir.hIni)
          .catch((err) => {
            erroGeral = err?.message ?? String(err);
          })
          .finally(terminarIda);
        return;
      }
    }
    // Retomada: o "Abrir e preencher" trocou de página no Prime (agenda existente ou Nova Agenda)
    // e continua aqui. Só uma vez e só se for recente — nunca fica pulando de página sozinho.
    const pend = st.pendente;
    if (pend) {
      st.pendente = null;
      salvar();
      if (st.aberto && Date.now() - pend.em < RETOMADA_MS && st.prof && propostaValida(st.prof.cpf) && st.proposta.chave === pend.chave) {
        preencherCompromisso(pend.i, { retomada: true }).catch((err) => {
          falhar(pend.i, err?.message ?? String(err));
          render({ forcar: true });
        });
      }
      return;
    }
    // Reconferência depois de um preenchimento (30 min): a cada carga de página — o Salvar do rodapé
    // recarrega — relê só a agenda deste profissional. Não roda com a janela do compromisso aberta
    // (o operador ainda está conferindo) nem na tela Consultar Agenda (a leitura refaz a pesquisa dela).
    const ca = st.conferirApos;
    if (ca && st.aberto && st.prof?.cpf === ca.cpf && Date.now() - ca.em < 30 * 60e3 && !/AgendaList\.aspx$/i.test(location.pathname)) {
      await pausa(1500);
      const t = await pagina('estado', {}, 5000);
      if (!t.ok || !t.janela) executar(relerPrimeDoProfissional);
    } else if (ca && Date.now() - ca.em >= 30 * 60e3) {
      st.conferirApos = null;
      salvar();
    }
  }

  globalThis.__smsmaisAgenda = {
    abrir() {
      st.aberto = true;
      salvar();
      render();
      if (!st.unidade || unidadeDesatualizada()) executar(resolverUnidade);
    },
  };

  iniciar();
})();
