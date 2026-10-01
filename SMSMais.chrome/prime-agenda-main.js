// Assistente de agenda — lado da PÁGINA do Prime (world: MAIN).
//
// Só no mundo da página existem os objetos Telerik ($find), e o preenchimento precisa passar por
// eles: escrever direto no <input> não atualiza o *_ClientState, e o Prime lê o valor do
// ClientState no postback (medido no cadastro de paciente, 21/09/2026).
//
// Recebe comandos do assistente (prime-agenda.js, mundo isolado) por postMessage e responde do
// mesmo jeito. NUNCA clica em Salvar, Excluir ou Enviar — gravar é do operador. O único botão que
// aciona é o "Adicionar" do profissional DENTRO da janela do compromisso: ele só põe o nome na
// grade da janela, não grava a agenda.
//
// Tela mapeada em 30/09/2026 (Agendamento/AgendaEdit.aspx, build 2024.03.1.63):
//  - Parâmetros: rdpPeriodo (mês), rtpHoraInicio/rtpHoraFim (faixa do calendário), rbtnSubmit
//    ("Enviar"). Depois do Enviar aparecem rtxtTitulo e o calendário (RadScheduler1).
//  - Compromisso novo: botão direito num horário do calendário → "Novo Evento" NÃO abre janela:
//    cria no calendário um compromisso PROVISÓRIO ("Consulta", 1 h) que só vai ao banco no Salvar
//    do rodapé (medido 30/09: a Consultar Agenda seguiu com 2 compromissos). Para editá-lo, duplo
//    clique (ou scheduler.editAppointment) abre o RadDock1 ("Editar Item Agenda") — e por isso o
//    hidAppointmentCommand vem "Edit" também no compromisso novo.
//  - Célula de horário: SÓ os td da grade (table.rsContentTable). O getTimeSlotFromDomElement do
//    Telerik devolve um "horário" para QUALQUER td do calendário (calcula pela linha/coluna do td
//    na tabela em que ele estiver) — ver celulasDaGrade.
//  - Trocar de semana: pelas SETAS do calendário (.rsPrevDay/.rsNextDay). NÃO usar
//    scheduler.set_selectedDate: a tela muda de semana, mas o servidor continua na anterior e o
//    "Novo Evento" cai no mesmo dia da semana antiga (medido 30/09: pedi 08/10, nasceu em 01/10).
//  - Na janela: rtxtStartDate, rtpStartTime/rtpEndTime, ntbQtd + rcboTipoQuantidade
//    (U=Horário(s), M=Minuto(s), H=Hora(s)), rcboTipoAppointment, ntbExtrasPermitidos,
//    rcboProfissional (serviço GetProfissionalGeracaoAgenda — a lista DEPENDE do tipo),
//    rcboFuncaoAtendimento (serviço GetDataGeracaoAgenda — depende do profissional),
//    RadButtonAdicionar → gridAgendaProfissional, e o RadSchedulerRecurrenceEditor1.
//  - A repetição fica presa ao mês da agenda: "Encerra em" aceita no máximo o dia 1º do mês
//    seguinte (é por isso que toda agenda do CDT vence no dia 1º).

(() => {
  if (window.__smsmaisAgendaMain) return;
  window.__smsmaisAgendaMain = true;

  const C = 'ctl00_ctl00_DefaultContent_ChildDefaultContent_';
  const D = C + 'RadDock1_C_';
  const R = D + 'RadSchedulerRecurrenceEditor1_';
  const DIAS_ID = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

  const achar = (id) => (typeof window.$find === 'function' ? window.$find(id) : null);
  // Passo em curso: erro que estoura DENTRO do Telerik (ex.: "Cannot read properties of undefined
  // (reading 'get_index')", 30/09) volta ao painel dizendo em que passo foi; a pilha vai ao console.
  let passo = '';
  const pausa = (ms) => new Promise((r) => setTimeout(r, ms));
  const telerikPronto = () => typeof window.$find === 'function' && !!window.Telerik;
  const norm = (s) =>
    String(s ?? '')
      .normalize('NFD')
      .replace(/[̀-ͯ]/g, '')
      .toUpperCase()
      .replace(/\s+/g, ' ')
      .trim();
  const visivel = (el) => !!el && el.offsetParent !== null && getComputedStyle(el).visibility !== 'hidden';
  const hidden = (nome) => document.querySelector(`input[name$="$${nome}"]`)?.value ?? '';
  const doRecorrencia = (suf) => document.querySelector(`[id$="RadSchedulerRecurrenceEditor1_${suf}"]`);

  const doisDig = (n) => String(n).padStart(2, '0');
  const hhmm = (dt) => (dt instanceof Date ? `${doisDig(dt.getHours())}:${doisDig(dt.getMinutes())}` : null);
  const ddmmaaaa = (dt) =>
    dt instanceof Date ? `${doisDig(dt.getDate())}/${doisDig(dt.getMonth() + 1)}/${dt.getFullYear()}` : null;
  const partesBr = (s) => {
    const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(String(s ?? '').trim());
    return m ? { d: +m[1], m: +m[2], a: +m[3] } : null;
  };
  const partesIso = (s) => {
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(s ?? ''));
    return m ? { a: +m[1], m: +m[2], d: +m[3] } : null;
  };
  const comHora = (p, hm) => {
    const [h, mi] = String(hm).split(':').map(Number);
    return new Date(p.a, p.m - 1, p.d, h, mi, 0);
  };

  // Espera o postback AJAX em curso (RadAjaxManager usa o PageRequestManager do MS AJAX).
  async function esperarAjax(tetoMs = 30000) {
    const prm = window.Sys?.WebForms?.PageRequestManager?.getInstance?.();
    if (!prm) return;
    const inicio = Date.now();
    await pausa(150);
    while (prm.get_isInAsyncPostBack() && Date.now() - inicio < tetoMs) await pausa(150);
  }
  const assentar = async () => {
    await pausa(250);
    await esperarAjax();
  };

  // Código antigo da tela (handler do "Tipo" da janela, medido 30/09) percorre a pilha com
  // fn.caller/arguments.callee; quando a pilha tem função async/arrow nossa, o V8 lança
  // "'caller', 'callee', and 'arguments' properties may not be accessed on strict mode functions".
  // Toda ação que dispara evento do Telerik roda por aqui: de um setTimeout, numa função comum —
  // a pilha que o Prime vê começa ali, como num clique de verdade. Quem chama passa `function`,
  // nunca arrow.
  function foraDaPilha(acao) {
    return new Promise(function (resolve, reject) {
      setTimeout(function () {
        try {
          resolve(acao());
        } catch (e) {
          reject(e);
        }
      }, 0);
    });
  }

  // Pede a lista de um RadComboBox que carrega por serviço (load on demand) e espera a volta.
  function pedirItens(combo, texto, tetoMs = 20000) {
    return new Promise((resolve, reject) => {
      let feito = false;
      const terminar = (fn) => {
        if (feito) return;
        feito = true;
        combo.remove_itemsRequested(ok);
        combo.remove_itemsRequestFailed(falhou);
        clearTimeout(relogio);
        fn();
      };
      const ok = () => terminar(() => resolve(itensDe(combo)));
      const falhou = () => terminar(() => reject(new Error('o Prime não devolveu a lista')));
      const relogio = setTimeout(() => terminar(() => reject(new Error('tempo esgotado esperando a lista do Prime'))), tetoMs);
      combo.add_itemsRequested(ok);
      combo.add_itemsRequestFailed(falhou);
      // Sem clearItems antes: o requestItems(texto, false) já troca a lista, e limpar por fora deixa
      // o combo com um "selecionado" que não existe mais (suspeita do erro get_index de 30/09).
      combo.requestItems(texto, false);
    });
  }
  function itensDe(combo) {
    const it = combo.get_items();
    return Array.from({ length: it.get_count() }, (_, i) => it.getItem(i));
  }

  // A grade tem uma 1ª coluna OCULTA ("Código", ex. 0517): nome e função são achados pelo cabeçalho.
  function profissionaisNaGrade() {
    const grade = document.getElementById(D + 'gridAgendaProfissional');
    if (!grade) return [];
    const cab = [...grade.querySelectorAll('th')].map((th) => norm(th.textContent));
    const iNome = Math.max(0, cab.indexOf('NOME'));
    const iFuncao = cab.findIndex((t) => t.startsWith('FUNCAO'));
    return [...grade.querySelectorAll('tr.rgRow, tr.rgAltRow')].map((tr) => {
      const tds = tr.querySelectorAll('td');
      return { nome: tds[iNome]?.textContent.trim() ?? '', funcao: tds[iFuncao >= 0 ? iFuncao : iNome + 1]?.textContent.trim() ?? '' };
    });
  }
  const naGrade = (nome) => profissionaisNaGrade().some((p) => norm(p.nome) === norm(nome));

  // O que está na janela agora — devolvido ao painel para o operador conferir.
  function lerJanela() {
    const dias = DIAS_ID.map((d, i) => (doRecorrencia(`WeeklyWeekDay${d}`)?.checked ? i : null)).filter((x) => x !== null);
    return {
      data: achar(D + 'rtxtStartDate')?.get_value?.() ?? null,
      inicio: hhmm(achar(D + 'rtpStartTime')?.get_selectedDate?.()),
      fim: hhmm(achar(D + 'rtpEndTime')?.get_selectedDate?.()),
      quantidade: achar(D + 'ntbQtd')?.get_value?.() ?? null,
      tipoQuantidade: achar(D + 'rcboTipoQuantidade')?.get_text?.() ?? null,
      tipo: achar(D + 'rcboTipoAppointment')?.get_text?.() ?? null,
      extras: achar(D + 'ntbExtrasPermitidos')?.get_value?.() ?? null,
      profissionais: profissionaisNaGrade(),
      recorrente: !!doRecorrencia('RecurrentAppointment')?.checked,
      semanal: !!doRecorrencia('RepeatFrequencyWeekly')?.checked,
      dias,
      encerraEm: ddmmaaaa(achar(R + 'RangeEndDate')?.get_selectedDate?.()),
    };
  }

  function estado() {
    const periodo = achar(C + 'rdpPeriodo');
    const titulo = achar(C + 'rtxtTitulo') ?? document.getElementById(C + 'rtxtTitulo');
    const dock = document.getElementById(C + 'RadDock1');
    return {
      ok: true,
      pagina: location.pathname.split('/').pop(),
      idAgenda: new URLSearchParams(location.search).get('id'),
      calendario: !!achar(C + 'RadScheduler1'),
      telerik: telerikPronto(),
      parametros: periodo
        ? { habilitado: periodo.get_enabled ? periodo.get_enabled() : true, mes: ddmmaaaa(periodo.get_selectedDate()) }
        : null,
      titulo: titulo ? (titulo.get_value ? titulo.get_value() : titulo.value) : null,
      janela: visivel(dock)
        ? { comando: hidden('hidAppointmentCommand'), data: achar(D + 'rtxtStartDate')?.get_value?.() ?? null }
        : null,
    };
  }

  // Parâmetros da Nova Agenda: mês + faixa de horário do calendário. Quem clica "Enviar" é o operador.
  async function preparar(d) {
    const periodo = achar(C + 'rdpPeriodo');
    if (!periodo) return { ok: false, erro: 'Abra Agenda → Consultar Agenda → "Nova Agenda" primeiro.' };
    if (periodo.get_enabled && !periodo.get_enabled()) {
      return { ok: false, erro: 'Os parâmetros desta agenda já foram enviados (estão travados). Siga para o título.' };
    }
    const [a, m] = String(d.mes).split('-').map(Number);
    const p = { a, m, d: 1 };
    await foraDaPilha(function () {
      periodo.set_selectedDate(new Date(a, m - 1, 1));
      achar(C + 'rtpHoraInicio')?.set_selectedDate(comHora(p, d.horaIni));
      achar(C + 'rtpHoraFim')?.set_selectedDate(comHora(p, d.horaFim));
    });
    return {
      ok: true,
      conferir: {
        mes: ddmmaaaa(periodo.get_selectedDate()),
        inicio: hhmm(achar(C + 'rtpHoraInicio')?.get_selectedDate()),
        fim: hhmm(achar(C + 'rtpHoraFim')?.get_selectedDate()),
      },
    };
  }

  // Parâmetros + "Enviar". O Enviar só monta o calendário na tela (AJAX, sem recarregar) — NÃO
  // grava a agenda (medido 30/09: novembro seguiu com 0 agendas na Consultar Agenda). Clicar pelo
  // elemento do botão: o click() do objeto RadButton não dispara o postback.
  async function enviar(d) {
    const r = await preparar(d);
    if (!r.ok) return r;
    const botao = document.getElementById(C + 'rbtnSubmit');
    if (!botao) return { ok: false, erro: 'Não achei o botão "Enviar" da Nova Agenda.' };
    botao.click();
    await assentar();
    for (let k = 0; k < 40 && !achar(C + 'RadScheduler1'); k++) await pausa(250);
    if (!achar(C + 'RadScheduler1')) {
      return { ok: false, erro: 'Cliquei em "Enviar", mas o calendário não apareceu. Veja se o Prime mostrou alguma mensagem.' };
    }
    return { ok: true, conferir: r.conferir };
  }

  async function titulo(d) {
    const rad = achar(C + 'rtxtTitulo');
    const campo = document.getElementById(C + 'rtxtTitulo');
    if (!rad && !campo) return { ok: false, erro: 'O campo Título só aparece depois de clicar em "Enviar" nos parâmetros.' };
    await foraDaPilha(function () {
      if (rad?.set_value) rad.set_value(d.texto);
      else {
        campo.value = d.texto;
        campo.dispatchEvent(new Event('change', { bubbles: true }));
      }
    });
    return { ok: true };
  }

  async function compromisso(d) {
    if (!telerikPronto()) return { ok: false, erro: 'A página do Prime ainda não terminou de carregar.' };
    const dock = document.getElementById(C + 'RadDock1');
    if (!visivel(dock)) {
      return { ok: false, erro: 'Abra a janela do compromisso: no calendário do Prime, botão direito no dia → "Novo Evento".' };
    }
    // Não dá para barrar pelo hidAppointmentCommand: o compromisso novo também abre como "Edit".
    // A proteção contra preencher o compromisso errado é a data conferida logo abaixo.
    passo = 'conferir a data da janela';
    const dataJanela = achar(D + 'rtxtStartDate')?.get_value?.() ?? '';
    if (d.dataEsperada && dataJanela && dataJanela !== d.dataEsperada && !d.usarDataDoPainel) {
      return {
        ok: false,
        etapa: 'data',
        dataJanela,
        erro: `A janela foi aberta no dia ${dataJanela}, mas este compromisso começa em ${d.dataEsperada}.`,
      };
    }
    if (d.usarDataDoPainel && d.dataEsperada) {
      await foraDaPilha(function () {
        achar(D + 'rtxtStartDate')?.set_value(d.dataEsperada);
      });
    }

    const feito = [];

    // 1) Tipo primeiro: a lista de profissionais do Prime depende dele.
    passo = 'escolher o tipo';
    const tipo = achar(D + 'rcboTipoAppointment');
    const itemTipo = tipo?.findItemByValue(String(d.tipo));
    if (!itemTipo) return { ok: false, erro: `O tipo "${d.tipo}" não existe no Prime.` };
    if (tipo.get_value() !== String(d.tipo)) {
      // Trocar o tipo LIMPA o profissional escolhido (a lista depende do tipo): por isso vem antes.
      await foraDaPilha(function () {
        itemTipo.select();
      });
      await assentar();
    }
    feito.push(`tipo: ${itemTipo.get_text()}`);

    // 2) Profissional + função → Adicionar (só se ainda não está na grade da janela).
    if (!naGrade(d.profissional) && !(d.profissionalSisreg && naGrade(d.profissionalSisreg))) {
      passo = 'buscar o profissional';
      const prof = achar(D + 'rcboProfissional');
      // A busca do combo devolve só a primeira página de quem CONTÉM o texto (medido 30/09: "ANA …"
      // trouxe LUCIANA, TATIANA… e não a ANA LUCIA). Tenta o nome inteiro e depois as palavras mais
      // raras (as mais longas), até achar o nome exato — o do Prime ou o do SISREG.
      const nomes = [d.profissional, d.profissionalSisreg].filter(Boolean).map(norm);
      const LIGA = new Set(['DE', 'DA', 'DO', 'DAS', 'DOS', 'E']);
      const palavras = [...new Set(nomes.flatMap((n) => n.split(' ')))].filter((w) => w.length >= 3 && !LIGA.has(w)).sort((a, b) => b.length - a.length);
      const buscas = [...new Set([d.profissional, d.profissionalSisreg].filter(Boolean).concat(palavras))].slice(0, 8);
      let alvo = null;
      for (const termo of buscas) {
        const itens = await pedirItens(prof, termo);
        alvo = itens.find((x) => nomes.includes(norm(x.get_text())));
        if (alvo) break;
      }
      if (!alvo) {
        // Erro de cadastro no Prime, não da extensão: o combo só oferece quem está habilitado para o
        // tipo (30/09: a Dra. Ana Lucia só aparece em "Consulta"). Sem sugerir nomes parecidos.
        return {
          ok: false,
          erro:
            `O Prime não oferece "${d.profissional}" para o tipo "${itemTipo.get_text()}" nesta unidade. ` +
            'O cadastro do profissional no Prime precisa liberar esse tipo.',
        };
      }
      // A busca deixou o combo filtrado pelo último termo: o item achado continua valendo.
      passo = 'escolher o profissional';
      await foraDaPilha(function () {
        alvo.select();
      });
      await assentar();

      passo = 'buscar a função';
      const func = achar(D + 'rcboFuncaoAtendimento');
      const funcoes = await pedirItens(func, '');
      let itemFunc = null;
      if (d.funcao) itemFunc = funcoes.find((x) => norm(x.get_text()) === norm(d.funcao));
      else if (funcoes.length === 1) itemFunc = funcoes[0];
      if (!itemFunc) {
        return {
          ok: false,
          etapa: 'funcao',
          opcoes: funcoes.map((x) => x.get_text()),
          erro: funcoes.length
            ? 'Escolha a função de atendimento deste compromisso.'
            : 'O Prime não devolveu nenhuma função de atendimento para este profissional neste tipo.',
        };
      }
      passo = 'escolher a função';
      await foraDaPilha(function () {
        itemFunc.select();
      });
      await assentar();

      // O Adicionar sem profissional abre um alert nativo ("Informe o profissional") que trava a
      // página até alguém clicar OK (medido 30/09). Só clica com os dois escolhidos.
      if (!achar(D + 'rcboProfissional')?.get_value() || !achar(D + 'rcboFuncaoAtendimento')?.get_value()) {
        return { ok: false, erro: 'O profissional ou a função não ficaram escolhidos na janela. Tente de novo.' };
      }
      passo = 'adicionar o profissional';
      await foraDaPilha(function () {
        achar(D + 'RadButtonAdicionar').click();
      });
      await assentar();
      await pausa(300);
      if (!naGrade(alvo.get_text())) {
        return {
          ok: false,
          erro: 'Cliquei em "Adicionar", mas o profissional não apareceu na grade da janela. Veja se o Prime mostrou alguma mensagem.',
        };
      }
      feito.push(`profissional: ${alvo.get_text()} (${itemFunc.get_text()})`);
    } else {
      feito.push('profissional já estava na grade');
    }

    // 3) Horário, quantidade e extras — DEPOIS do Adicionar, que refaz a janela.
    passo = 'preencher horário e vagas';
    const base = partesBr(achar(D + 'rtxtStartDate')?.get_value?.()) ?? partesBr(d.dataEsperada);
    if (!base) return { ok: false, erro: 'Não consegui ler a data da janela.' };
    const tq = achar(D + 'rcboTipoQuantidade');
    const itemTq = tq?.findItemByValue(d.tipoQtd || 'U');
    await foraDaPilha(function () {
      achar(D + 'rtpStartTime')?.set_selectedDate(comHora(base, d.hIni));
      achar(D + 'rtpEndTime')?.set_selectedDate(comHora(base, d.hFim));
      achar(D + 'ntbQtd')?.set_value(Number(d.qtd));
      if (itemTq && tq.get_value() !== itemTq.get_value()) itemTq.select();
      achar(D + 'ntbExtrasPermitidos')?.set_value(Number(d.extras || 0));
    });
    await assentar();
    feito.push(`${d.hIni}–${d.hFim}, ${d.qtd} ${itemTq?.get_text() ?? ''}, extras ${d.extras || 0}`);

    // 4) Repetição semanal nos dias da escala, até o fim da vigência (no máximo o dia 1º seguinte).
    passo = 'marcar a repetição';
    const rec = doRecorrencia('RecurrentAppointment');
    if (rec && !rec.checked) {
      rec.click();
      await pausa(200);
    }
    const semanal = doRecorrencia('RepeatFrequencyWeekly');
    if (semanal && !semanal.checked) {
      semanal.click();
      await pausa(200);
    }
    await foraDaPilha(function () {
      achar(R + 'WeeklyRepeatInterval')?.set_value(1);
    });
    DIAS_ID.forEach((nome, i) => {
      const cx = doRecorrencia(`WeeklyWeekDay${nome}`);
      const quer = (d.dias || []).includes(i);
      if (cx && cx.checked !== quer) cx.click();
    });
    const ate = doRecorrencia('RepeatUntilGivenDate');
    if (ate && !ate.checked) {
      ate.click();
      await pausa(150);
    }
    const fim = partesIso(d.fim);
    if (fim) {
      await foraDaPilha(function () {
        achar(R + 'RangeEndDate')?.set_selectedDate(new Date(fim.a, fim.m - 1, fim.d));
      });
    }
    feito.push('repetição semanal');

    const conferir = lerJanela();
    const avisos = [];
    if (fim && conferir.encerraEm !== `${doisDig(fim.d)}/${doisDig(fim.m)}/${fim.a}`) {
      avisos.push(`O Prime não aceitou encerrar em ${doisDig(fim.d)}/${doisDig(fim.m)}/${fim.a} (ficou ${conferir.encerraEm ?? 'vazio'}).`);
    }
    if (conferir.inicio !== d.hIni || conferir.fim !== d.hFim) {
      avisos.push(`Horário na janela ficou ${conferir.inicio}–${conferir.fim}; o esperado era ${d.hIni}–${d.hFim}.`);
    }
    return { ok: true, feito, conferir, avisos };
  }

  // ------------------------------------------- abrir um compromisso novo no dia certo
  // Faz o que o operador faria: vai até a semana, botão direito no horário → "Novo Evento" (o Prime
  // cria o compromisso provisório) e abre esse compromisso para editar. Nada disso grava: o
  // provisório só vai ao banco no Salvar do rodapé.
  const agenda = () => achar(C + 'RadScheduler1');
  const minutosDe = (hm) => {
    const [h, m] = String(hm).split(':').map(Number);
    return h * 60 + m;
  };
  // Só as células da GRADE de horários. O getTimeSlotFromDomElement do Telerik devolve um "horário"
  // para QUALQUER td do calendário: calcula pela posição (linha/coluna) do td na tabela em que ele
  // estiver. Medido no Prime em 30/09: os 179 td do calendário viram "horário", mas a grade da
  // semana tem 126 (18 linhas × 7 dias); o resto é o mini-calendário ESCONDIDO do cabeçalho (o
  // seletor de data), a linha "dia inteiro" e td de layout. O cabeçalho vem antes no HTML, então a
  // busca pegava um dia do mini-calendário sempre que o horário caía nas linhas que ele tem (08:30
  // a 11:00): o botão direito ia numa célula 31×27 escondida em .rsHeader, o menu não abria e o
  // "Novo Evento" quebrava com "reading 'get_index'" (COLPOSCOPIA sex 08:30 — quatro versões
  // tentando afastar o que estava "por cima" até o diagnóstico do cartão mostrar a célula). Qui
  // 08:00 e 13:00 funcionavam: o mini-calendário não tem td na linha 0 nem da linha 7 em diante.
  function celulasDaGrade(s) {
    const raiz = s.get_element();
    // A grade do Telerik é table.rsContentTable ("dia inteiro" é rsAllDayTable, fora dela).
    const daGrade = [...raiz.querySelectorAll('.rsContentTable td')].filter((td) => td.closest('table').classList.contains('rsContentTable'));
    if (daGrade.length) return daGrade;
    // Sem essa classe: a maior tabela do calendário que não é cabeçalho, mini-calendário nem "dia
    // inteiro" — e sem tabela dentro (td de layout).
    const porTabela = new Map();
    for (const td of raiz.querySelectorAll('td')) {
      if (td.closest('.rsHeader, .rsDatePickerWrapper, .RadCalendar, .rsAllDayTable') || td.querySelector('table')) continue;
      const tabela = td.closest('table');
      if (!porTabela.has(tabela)) porTabela.set(tabela, []);
      porTabela.get(tabela).push(td);
    }
    return [...porTabela.values()].sort((a, b) => b.length - a.length)[0] ?? [];
  }
  function slots(s) {
    const modelo = s.get_activeModel();
    const lista = [];
    for (const td of celulasDaGrade(s)) {
      try {
        const sl = modelo.getTimeSlotFromDomElement(td);
        if (!sl?.get_startTime || sl.get_isAllDay?.()) continue;
        lista.push({ td, sl, ini: sl.get_startTime(), fim: sl.get_endTime?.() ?? null });
      } catch {
        /* célula que não é horário */
      }
    }
    // Conferência pelo próprio Telerik: o horário sabe qual é a célula dele (o Prime usa
    // get_domElement no handler do botão direito). Fica só o td que É a célula do seu horário; se a
    // conferência não deixar nenhum, ela não se aplica a esta versão do Telerik e vale a lista.
    const conferidos = lista.filter((x) => {
      try {
        const el = x.sl.get_domElement?.();
        return !el || el === x.td;
      } catch {
        return true;
      }
    });
    return conferidos.length ? conferidos : lista;
  }
  const mesmoDia = (a, b) => a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
  function acharSlot(s, dia, hm) {
    const doDia = slots(s).filter((x) => mesmoDia(x.ini, dia));
    if (!doDia.length) return null;
    const alvo = minutosDe(hm);
    const min = (x) => x.ini.getHours() * 60 + x.ini.getMinutes();
    // o horário que contém o início; senão o último antes dele; senão o primeiro do dia
    return (
      doDia.find((x) => min(x) <= alvo && (!x.fim || alvo < x.fim.getHours() * 60 + x.fim.getMinutes())) ||
      [...doDia].reverse().find((x) => min(x) <= alvo) ||
      doDia[0]
    );
  }
  function compromissosDaAgenda(s) {
    const apps = s.get_appointments();
    return Array.from({ length: apps.get_count() }, (_, i) => apps.getAppointment(i));
  }
  const chaveApp = (a) => `${a.get_id?.() ?? ''}|${a.get_start().getTime()}|${a.get_end().getTime()}`;

  // Semana certa, pelas setas do calendário (o servidor precisa acompanhar — ver cabeçalho).
  // O calendário abre na semana de HOJE, não no mês da agenda: até ~3 meses de setas.
  async function irParaSemana(dia, hm) {
    let s = agenda();
    let slot = s && acharSlot(s, dia, hm);
    let setas = 0;
    for (let k = 0; s && !slot && k < 14; k++, setas++) {
      const vis = slots(s).map((x) => x.ini);
      if (!vis.length) break;
      const seta = s.get_element().querySelector(dia > vis[vis.length - 1] ? '.rsNextDay' : '.rsPrevDay');
      if (!seta) break;
      seta.click();
      await assentar();
      s = agenda();
      slot = s && acharSlot(s, dia, hm);
    }
    return { s, slot, setas };
  }

  // Selo de data do painel: só leva o calendário até a semana do dia e destaca o horário. Não abre
  // nem cria compromisso.
  async function irParaData(d) {
    if (visivel(document.getElementById(C + 'RadDock1'))) {
      return { ok: false, erro: 'Feche a janela do compromisso aberta no Prime antes.' };
    }
    if (!agenda()) return { ok: false, erro: 'O calendário da agenda ainda não apareceu.' };
    const p = partesBr(d.data);
    if (!p) return { ok: false, erro: `Data inválida: ${d.data}` };
    const { slot, setas } = await irParaSemana(new Date(p.a, p.m - 1, p.d), d.hIni || '08:00');
    if (!slot) return { ok: false, erro: `Não achei ${d.data} no calendário (fora do mês da agenda?).` };
    slot.td.scrollIntoView({ block: 'center' });
    const antes = slot.td.style.outline;
    slot.td.style.outline = '3px solid #0969da';
    setTimeout(() => {
      slot.td.style.outline = antes;
    }, 2500);
    return { ok: true, jaNaTela: setas === 0 };
  }

  async function abrirCompromisso(d) {
    const dock = document.getElementById(C + 'RadDock1');
    if (visivel(dock)) return { ok: true, jaAberta: true };
    let s = agenda();
    if (!s) return { ok: false, erro: 'O calendário só aparece depois de clicar "Enviar" nos parâmetros da agenda.' };
    const p = partesBr(d.data);
    if (!p) return { ok: false, erro: `Data inválida: ${d.data}` };
    const dia = new Date(p.a, p.m - 1, p.d);

    // 1) semana certa
    passo = 'ir até a semana';
    let slot;
    ({ s, slot } = await irParaSemana(dia, d.hIni));
    if (!slot) return { ok: false, erro: `Não achei ${d.data} no calendário (fora do mês da agenda?).` };
    if (slot.td.classList.contains('Disabled')) {
      return { ok: false, erro: `O dia ${d.data} está bloqueado no calendário do Prime (já passou?).` };
    }

    // 2) botão direito → "Novo Evento"
    // Validado no Prime real em 30/09 (qui 01/10 e 08/10): o evento "contextmenu" despachado na
    // CÉLULA DA GRADE faz o calendário guardar o horário, e o item "Novo Evento" cria o provisório
    // nele. Numa célula que não é da grade o menu não abre e o item quebra lá dentro com "reading
    // 'get_index'" (_handleServerSideTimeSlotContextMenuItemClick) — ver celulasDaGrade. O
    // try/catch fica: se o Prime não guardar o horário, o cartão recebe o caminho manual e o
    // diagnóstico da célula usada.
    passo = 'criar o "Novo Evento"';
    const antes = new Set(compromissosDaAgenda(s).map(chaveApp));
    slot.td.scrollIntoView({ block: 'center', inline: 'nearest' });
    const r = slot.td.getBoundingClientRect();
    slot.td.dispatchEvent(
      new MouseEvent('contextmenu', {
        bubbles: true,
        cancelable: true,
        view: window,
        button: 2,
        buttons: 2,
        clientX: Math.round(r.left + Math.min(r.width / 2, 40)),
        clientY: Math.round(r.top + r.height / 2),
      }),
    );
    await pausa(300);
    const item = s.get_timeSlotContextMenus?.()[0]?.get_items().getItem(0);
    if (!item || !item.get_enabled()) return { ok: false, erro: 'O Prime não deixou criar compromisso nesse horário.' };
    try {
      await foraDaPilha(function () {
        item.click();
      });
    } catch (err) {
      // Diagnóstico (vai no cartão): que célula foi usada, de que tabela, e onde o Prime quebrou.
      const nome = (el) => String(el?.id || (typeof el?.className === 'string' && el.className) || el?.tagName || '-').trim().split(/\s+/)[0].slice(0, 28);
      const onde = String(err?.stack ?? '').split('\n').slice(1, 4).map((l) => l.trim().replace(/^at /, '').replace(/\(.*\)$/, '').trim()).join(' ← ');
      const detalhe =
        `grade=${celulasDaGrade(s).length} células; tabela=${nome(slot.td.closest('table'))}; ` +
        `célula=${slot.td.className || '-'} ${Math.round(r.left)},${Math.round(r.top)} ${Math.round(r.width)}x${Math.round(r.height)} ${hhmm(slot.ini)}; ` +
        `agenda=${new URLSearchParams(location.search).get('id') ? 'salva' : 'nova'}; em=${onde || '-'}`;
      console.warn('[SMSMais agenda] "Novo Evento" falhou', detalhe, err);
      return {
        ok: false,
        erro:
          `O Prime não registrou o horário no botão direito. Faça à mão: botão direito no horário ${d.hIni} de ${d.data} → ` +
          '"Novo Evento", duplo clique no compromisso criado, e clique em "Abrir e preencher" de novo.',
        detalhe,
      };
    }
    await assentar();
    await pausa(300);

    // 3) o compromisso provisório que apareceu → abrir para editar
    passo = 'abrir o compromisso';
    s = agenda();
    const novo = s && compromissosDaAgenda(s).find((a) => !antes.has(chaveApp(a)));
    if (!novo) return { ok: false, erro: 'Cliquei em "Novo Evento", mas nenhum compromisso novo apareceu no calendário.' };
    await foraDaPilha(function () {
      s.editAppointment(novo);
    });
    await assentar();
    for (let i = 0; i < 20 && !visivel(document.getElementById(C + 'RadDock1')); i++) await pausa(250);
    if (!visivel(document.getElementById(C + 'RadDock1'))) {
      return { ok: false, erro: 'Criei o compromisso provisório, mas a janela não abriu. Dê duplo clique nele e tente Preencher.' };
    }
    return { ok: true, data: achar(D + 'rtxtStartDate')?.get_value?.() ?? null };
  }

  // Espera o Prime terminar o postback em curso (ex.: depois de um "Salvar"). Postback COMPLETO
  // recarrega a página e esta resposta nunca chega — quem continua é a carga seguinte do painel.
  async function aguardarPrime() {
    await pausa(300);
    await esperarAjax(60000);
    return { ok: true, janelaAberta: visivel(document.getElementById(C + 'RadDock1')) };
  }

  const COMANDOS = { estado, preparar, enviar, titulo, compromisso, abrirCompromisso, irParaData, aguardarPrime };

  window.addEventListener('message', async (e) => {
    if (e.source !== window || e.data?.__smsmaisAgendaCmd !== true) return;
    const { id, cmd, dados } = e.data;
    let resp;
    passo = '';
    try {
      resp = COMANDOS[cmd] ? await COMANDOS[cmd](dados ?? {}) : { ok: false, erro: `comando desconhecido: ${cmd}` };
    } catch (err) {
      console.warn('[SMSMais agenda]', cmd, passo || '-', err);
      resp = { ok: false, erro: `Erro do Prime${passo ? ` ao ${passo}` : ''}: ${String(err?.message ?? err)}` };
    }
    window.postMessage({ __smsmaisAgendaResp: true, id, ...resp }, '*');
  });
})();
