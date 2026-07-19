// Página local do Automais.Pabx — teste/operação antes do menu no SMSMarica.
// JS puro, sem build. Toda chamada à API leva o header X-Api-Key.

const $ = (sel) => document.querySelector(sel);

let apiKey = localStorage.getItem('pabx-api-key') || '';
let unidades = [];
let cdrPagina = 1;
let editandoNumero = null;
let timerAuto = null;

// ---------- infra ----------

async function api(caminho, opcoes = {}) {
  const resp = await fetch(caminho, {
    ...opcoes,
    headers: {
      'Content-Type': 'application/json',
      'X-Api-Key': apiKey,
      ...(opcoes.headers || {}),
    },
  });

  if (resp.status === 204) return null;

  const corpo = await resp.json().catch(() => null);
  if (!resp.ok) {
    const detalhe = corpo?.detail || corpo?.title || `HTTP ${resp.status}`;
    const erros = corpo?.errors ? ' — ' + Object.values(corpo.errors).flat().join('; ') : '';
    throw new Error(detalhe + erros);
  }
  return corpo;
}

function toast(msg, erro = false) {
  const el = $('#toast');
  el.textContent = msg;
  el.classList.toggle('erro', erro);
  el.classList.remove('oculto');
  clearTimeout(el._t);
  el._t = setTimeout(() => el.classList.add('oculto'), 4000);
}

function esc(s) {
  const div = document.createElement('div');
  div.textContent = s ?? '';
  return div.innerHTML;
}

// ---------- abas ----------

document.querySelectorAll('nav .aba').forEach((btn) => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('nav .aba').forEach((b) => b.classList.remove('ativa'));
    btn.classList.add('ativa');
    ['ramais', 'cdr', 'unidades'].forEach((aba) =>
      $(`#aba-${aba}`).classList.toggle('oculto', aba !== btn.dataset.aba));
    if (btn.dataset.aba === 'unidades') carregarUnidadesTabela();
  });
});

// ---------- API key ----------

$('#apiKey').value = apiKey;
$('#salvarKey').addEventListener('click', () => {
  apiKey = $('#apiKey').value.trim();
  localStorage.setItem('pabx-api-key', apiKey);
  iniciar();
});

// ---------- unidades ----------

async function carregarUnidades() {
  unidades = await api('/api/unidades');
  const opcoes = unidades.map((u) => `<option value="${u.id}">${u.id} — ${esc(u.nome)}</option>`).join('');
  $('#filtroUnidade').innerHTML = '<option value="">Todas as unidades</option>' + opcoes;
  $('#fUnidade').innerHTML = opcoes;
  $('#aUnidade').innerHTML = opcoes;
}

async function carregarUnidadesTabela() {
  const corpo = $('#tabelaUnidades tbody');
  corpo.innerHTML = unidades.map((u) => `
    <tr>
      <td>${u.id}</td>
      <td>${esc(u.nome)}</td>
      <td>${esc(u.grupo)}</td>
      <td>${esc(u.lan)}</td>
      <td>${esc(u.status)}</td>
      <td>${u.quantidadeRamais}</td>
      <td>${esc(u.gestor ?? '')}</td>
    </tr>`).join('');
}

// ---------- ramais + status ----------

async function carregarRamais() {
  const status = await api('/api/ramais/status');
  $('#avisoAmi').classList.toggle('oculto', status.amiDisponivel);
  if (!status.amiDisponivel)
    $('#avisoAmi').textContent = 'AMI indisponível — exibindo inventário sem estado de registro em tempo real.';

  const filtro = $('#filtroUnidade').value;
  const linhas = status.ramais
    .filter((r) => !filtro || r.unidadeId === Number(filtro))
    .map((r) => {
      const led = r.emChamada ? 'chamada' : (r.online ? 'on' : 'off');
      const titulo = r.emChamada ? 'em chamada' : (r.online ? 'registrado' : (r.statusBruto || 'offline'));
      const divergente = r.unidadeDetectadaId !== null && r.unidadeDetectadaId !== r.unidadeId;
      return `
        <tr>
          <td><span class="led ${led}" title="${esc(titulo)}"></span></td>
          <td><strong>${esc(r.numero)}</strong></td>
          <td>${esc(r.descricao ?? '')}</td>
          <td>${esc(r.unidadeNome ?? r.unidadeId)}</td>
          <td>${esc(r.ip ?? '—')}${divergente ? ' ⚠️ unidade ' + r.unidadeDetectadaId : ''}</td>
          <td>${r.latenciaMs !== null ? r.latenciaMs + ' ms' : '—'}</td>
          <td data-tel="${esc(r.numero)}"></td>
          <td>${r.origem === 'Adotado' ? '<span class="badge adotado">adotado</span>' : '<span class="badge">gerenciado</span>'}</td>
          <td>
            <button class="mini" data-editar="${esc(r.numero)}">Editar</button>
            <button class="mini sec" data-reset="${esc(r.numero)}">Reset secret</button>
            <button class="mini sec" data-excluir="${esc(r.numero)}">Excluir</button>
          </td>
        </tr>`;
    });

  $('#tabelaRamais tbody').innerHTML = linhas.join('') ||
    '<tr><td colspan="9">Nenhum ramal no inventário — crie um novo ou adote os existentes.</td></tr>';

  // Coluna "Telefone" precisa do detalhe (marca/modelo/MAC) que não vem no status.
  const ramais = await api('/api/ramais' + (filtro ? `?unidadeId=${filtro}` : ''));
  for (const r of ramais) {
    const celula = document.querySelector(`[data-tel="${r.numero}"]`);
    if (celula)
      celula.textContent = r.marca ? `${r.marca} ${r.modelo ?? ''} (${r.mac ?? 'sem MAC'})` : '—';
  }
}

$('#atualizarRamais').addEventListener('click', () => carregarRamais().catch((e) => toast(e.message, true)));
$('#filtroUnidade').addEventListener('change', () => carregarRamais().catch((e) => toast(e.message, true)));

$('#autoRefresh').addEventListener('change', (ev) => {
  clearInterval(timerAuto);
  if (ev.target.checked)
    timerAuto = setInterval(() => carregarRamais().catch(() => {}), 10000);
});

$('#reaplicar').addEventListener('click', async () => {
  if (!confirm('Regenerar sip_smsmarica.conf e executar sip reload?')) return;
  try {
    const r = await api('/api/ramais/aplicar', { method: 'POST' });
    toast(`Config aplicada: ${r.ramaisEscritos} ramais${r.reloadExecutado ? ' + sip reload' : ' (sem reload — AMI off)'}`);
  } catch (e) { toast(e.message, true); }
});

// ---------- novo/editar ----------

$('#abrirNovo').addEventListener('click', () => {
  editandoNumero = null;
  $('#dlgTitulo').textContent = 'Novo ramal';
  $('#formRamal').reset();
  $('#fNumero').disabled = false;
  $('#rotAtivo').classList.add('oculto');
  $('#dlgRamal').showModal();
});

document.addEventListener('click', async (ev) => {
  const numeroEditar = ev.target.dataset?.editar;
  const numeroReset = ev.target.dataset?.reset;
  const numeroExcluir = ev.target.dataset?.excluir;

  try {
    if (numeroEditar) {
      const r = await api(`/api/ramais/${numeroEditar}`);
      editandoNumero = r.numero;
      $('#dlgTitulo').textContent = `Editar ramal ${r.numero}`;
      $('#fNumero').value = r.numero;
      $('#fNumero').disabled = true;
      $('#fUnidade').value = r.unidadeId;
      $('#fDescricao').value = r.descricao ?? '';
      $('#fMac').value = r.mac ?? '';
      $('#fMarca').value = r.marca ?? '';
      $('#fModelo').value = r.modelo ?? '';
      $('#fCallerId').value = r.callerId ?? '';
      $('#fAtivo').checked = r.ativo;
      $('#rotAtivo').classList.remove('oculto');
      $('#dlgRamal').showModal();
    }

    if (numeroReset) {
      if (!confirm(`Gerar novo secret para o ramal ${numeroReset}? O aparelho atual perde o registro até reprovisionar.`)) return;
      const r = await api(`/api/ramais/${numeroReset}/reset-secret`, { method: 'POST' });
      $('#secretValor').textContent = r.secret;
      $('#dlgSecret').showModal();
      await carregarRamais();
    }

    if (numeroExcluir) {
      if (!confirm(`Excluir o ramal ${numeroExcluir} do inventário (e do sip_smsmarica.conf, se gerenciado)?`)) return;
      await api(`/api/ramais/${numeroExcluir}`, { method: 'DELETE' });
      toast(`Ramal ${numeroExcluir} excluído.`);
      await carregarRamais();
    }
  } catch (e) { toast(e.message, true); }
});

$('#formRamal').addEventListener('submit', async (ev) => {
  if (ev.submitter?.value !== 'ok') return;
  ev.preventDefault();

  const corpo = {
    numero: $('#fNumero').value.trim(),
    unidadeId: Number($('#fUnidade').value),
    descricao: $('#fDescricao').value.trim() || null,
    mac: $('#fMac').value.replaceAll(':', '').replaceAll('-', '').trim() || null,
    marca: $('#fMarca').value || null,
    modelo: $('#fModelo').value.trim() || null,
    callerId: $('#fCallerId').value.trim() || null,
  };

  try {
    if (editandoNumero) {
      corpo.ativo = $('#fAtivo').checked;
      await api(`/api/ramais/${editandoNumero}`, { method: 'PUT', body: JSON.stringify(corpo) });
      toast(`Ramal ${editandoNumero} atualizado.`);
    } else {
      const criado = await api('/api/ramais', { method: 'POST', body: JSON.stringify(corpo) });
      $('#secretValor').textContent = criado.secret;
      $('#dlgSecret').showModal();
    }
    $('#dlgRamal').close();
    await carregarRamais();
  } catch (e) { toast(e.message, true); }
});

// ---------- adoção ----------

$('#abrirAdotar').addEventListener('click', () => {
  $('#formAdotar').reset();
  $('#dlgAdotar').showModal();
});

$('#formAdotar').addEventListener('submit', async (ev) => {
  if (ev.submitter?.value !== 'ok') return;
  ev.preventDefault();

  const corpo = {
    numeros: $('#aNumeros').value.split(',').map((n) => n.trim()).filter(Boolean),
    unidadeId: Number($('#aUnidade').value),
  };

  try {
    const r = await api('/api/ramais/adotar', { method: 'POST', body: JSON.stringify(corpo) });
    toast(`Adotados: ${r.adotados.length}; já inventariados: ${r.jaInventariados.length}; não encontrados: ${r.naoEncontrados.join(', ') || 'nenhum'}`);
    $('#dlgAdotar').close();
    await carregarRamais();
  } catch (e) { toast(e.message, true); }
});

// ---------- CDR ----------

async function buscarCdr() {
  const parametros = new URLSearchParams();
  if ($('#cdrRamal').value.trim()) parametros.set('ramal', $('#cdrRamal').value.trim());
  if ($('#cdrNumero').value.trim()) parametros.set('numero', $('#cdrNumero').value.trim());
  if ($('#cdrDe').value) parametros.set('de', $('#cdrDe').value);
  if ($('#cdrAte').value) parametros.set('ate', $('#cdrAte').value);
  parametros.set('pagina', cdrPagina);

  const r = await api('/api/cdr?' + parametros);
  $('#cdrTotal').textContent = `${r.total} chamadas`;
  $('#cdrPagina').textContent = cdrPagina;
  $('#tabelaCdr tbody').innerHTML = r.itens.map((c) => `
    <tr>
      <td>${c.dataHora.replace('T', ' ').slice(0, 19)}</td>
      <td>${esc(c.origem)}</td>
      <td>${esc(c.destino)}</td>
      <td>${formatarDuracao(c.duracaoSegundos)}</td>
      <td>${formatarDuracao(c.faturadoSegundos)}</td>
      <td>${esc(c.disposicao)}</td>
    </tr>`).join('') || '<tr><td colspan="6">Nenhuma chamada com esses filtros.</td></tr>';
}

function formatarDuracao(seg) {
  const m = Math.floor(seg / 60), s = seg % 60;
  return `${m}:${String(s).padStart(2, '0')}`;
}

$('#buscarCdr').addEventListener('click', () => { cdrPagina = 1; buscarCdr().catch((e) => toast(e.message, true)); });
$('#cdrAnterior').addEventListener('click', () => { if (cdrPagina > 1) { cdrPagina--; buscarCdr().catch((e) => toast(e.message, true)); } });
$('#cdrProxima').addEventListener('click', () => { cdrPagina++; buscarCdr().catch((e) => toast(e.message, true)); });

// ---------- boot ----------

async function iniciar() {
  if (!apiKey) { toast('Informe a API key para começar.', true); return; }
  try {
    await carregarUnidades();
    await carregarRamais();
    toast('Conectado.');
  } catch (e) { toast(e.message, true); }
}

iniciar();
