// Reconhecedor de operações do Prime (Eco Sistemas) — o "endpoints.js" deste sistema.
//
// O Prime é ASP.NET WebForms: TODA operação é um POST para a mesma `.aspx`, e o que distingue
// uma da outra são o `__EVENTTARGET`/`__EVENTARGUMENT` e um punhado de campos. Este arquivo
// transforma esse POST num EVENTO DE NEGÓCIO nomeado, com as chaves que o hub precisa —
// `pacienteId`, `agendaId`, `unidadeId` — em vez de deixar o trabalho para quem for consultar
// o acervo depois.
//
// Tudo aqui foi medido em 21/09/2026 (unidade 2b89b351, tela Agenda da recepção).

// ------------------------------------------------------------------ utilidades

// Nome de campo do WebForms vem com o caminho inteiro do controle:
//   ctl00$ctl00$DefaultContent$ChildDefaultContent$UCNovoCadastroPaciente1$txtNome
// O que identifica o campo é o último pedaço.
export function sufixo(nome) {
  const i = String(nome).lastIndexOf('$');
  return i < 0 ? String(nome) : String(nome).slice(i + 1);
}

// Primeiro valor NÃO vazio de um campo, procurando pelo sufixo. A tela de cadastro repete
// `txtCEP`/`rcboMunicipio` em três blocos de endereço; o preenchido é o que interessa.
export function campo(campos, nomeCurto) {
  for (const [k, v] of Object.entries(campos ?? {})) {
    if (sufixo(k) !== nomeCurto) continue;
    const valor = Array.isArray(v) ? v.find((x) => x !== '' && x != null) : v;
    if (valor !== undefined && valor !== null && valor !== '') return String(valor);
  }
  return null;
}

const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;
const ehGuid = (s) => Boolean(s) && GUID.test(s) && s.length === 36;

function colher(campos, nomes) {
  const saida = {};
  for (const n of nomes) {
    const v = campo(campos, n);
    if (v !== null) saida[n] = v;
  }
  return saida;
}

// ------------------------------------------------------- verbos (o que medimos)
//
// `evento`  — nome de negócio; vai para a COLUNA `evento` do hub (indexada).
// `etapa`   — o verbo cru do Prime; vai para a coluna `etapa`, para conferência.
// `esperaId`— o id do registro criado NÃO existe no envio: só aparece na resposta. Quem
//             reconhecer marca a aba como "aguardando id" e o evento sai quando ele chega.

export const VERBOS = {
  PACIENTE_CRIADO: {
    evento: 'paciente-criado',
    etapa: 'rbSalvar',
    escrita: true,
    esperaId: 'pacienteId',
  },
  PACIENTE_AGENDADO: {
    evento: 'paciente-agendado',
    etapa: 'rbSalvarAgendamento',
    escrita: true,
  },
  PACIENTE_ACOLHIDO: {
    evento: 'paciente-acolhido',
    etapa: 'Acolher',
    escrita: true,
  },
  PACIENTE_DESAGENDADO: {
    evento: 'paciente-desagendado',
    etapa: 'Desagendar',
    escrita: true,
  },
};

// Campos de identidade do cadastro. Nomes medidos na tela; o que não existir sai de fora.
const CAMPOS_CADASTRO = [
  'txtNome', 'txtCPF', 'RadTextBoxCNS', 'txtNomeMae', 'txtNomePai',
  'rdpDataNascimento', 'rblSexo', 'txtCelular', 'rcboRaca',
  'rcboUfNascimento', 'rcboMunicipioNascimento', 'txtNumeroProntuario',
  'txtCEP', 'rcbLogradouro', 'txtNumero', 'txtComplemento', 'rcboBairro',
  'rcboMunicipio', 'rcboMunicipioCodigo', 'hidEnderecoId',
];

const CAMPOS_AGENDAMENTO = [
  'hiAgendaId', 'rcboProfissional', 'RadDatePicker1', 'rblTurnoAgendaProfissional',
  'rblProcedencia', 'rblTipoAgendamento', 'rcboFuncao',
];

/**
 * Reconhece a operação a partir do ENVIO já decodificado.
 * @returns {{verbo, evento, etapa, escrita, esperaId?, chaves, dados}|null}
 */
export function reconhecer(caminho, campos) {
  if (!caminho || !campos) return null;
  const alvo = sufixo(campo(campos, '__EVENTTARGET') ?? '');
  const argumento = campo(campos, '__EVENTARGUMENT') ?? '';
  const unidadeId = campo(campos, 'hidUnidadeId');

  // --- Acolher / Desagendar: o verbo e o id viajam juntos no __EVENTARGUMENT.
  //     Medido: "Acolher|<agendaId>", "Desagendar|<agendaId>", via RadAjaxManager1.
  const barra = argumento.indexOf('|');
  if (barra > 0) {
    const acao = argumento.slice(0, barra);
    const id = argumento.slice(barra + 1).trim();
    const verbo =
      acao === 'Acolher' ? VERBOS.PACIENTE_ACOLHIDO :
      acao === 'Desagendar' ? VERBOS.PACIENTE_DESAGENDADO : null;
    if (verbo && ehGuid(id)) {
      return {
        verbo: acao,
        ...verbo,
        // O paciente NÃO é carimbado aqui de propósito: "Acolher" age sobre a LINHA da grade,
        // não sobre o paciente que estiver selecionado na tela. Deduzir um seria inventar.
        chaves: { agendaId: id, unidadeId },
        dados: {},
      };
    }
    return null;
  }

  // --- Agendar: não passa por __doPostBack (é submit do formulário da janela). O que o
  //     identifica é o botão `rbSalvarAgendamento` estar presente no corpo.
  if (/AgendaRecepcao/i.test(caminho) && campo(campos, 'rbSalvarAgendamento')) {
    const agendaId = campo(campos, 'hiAgendaId');
    if (!ehGuid(agendaId)) return null;
    return {
      verbo: 'rbSalvarAgendamento',
      ...VERBOS.PACIENTE_AGENDADO,
      chaves: { agendaId, unidadeId },
      dados: colher(campos, CAMPOS_AGENDAMENTO),
    };
  }

  // --- Cadastrar paciente: __EVENTTARGET termina em `rbSalvar`, na tela de cadastro.
  if (/CadastroPaciente/i.test(caminho) && alvo === 'rbSalvar') {
    return {
      verbo: 'rbSalvar',
      ...VERBOS.PACIENTE_CRIADO,
      chaves: { unidadeId },
      dados: colher(campos, CAMPOS_CADASTRO),
    };
  }

  return null;
}

/**
 * A SELEÇÃO de paciente não é operação de negócio, mas é o que amarra o agendamento ao
 * paciente: "SELECIONAR_PACIENTE|<pacienteId>". Quem chamar guarda por aba e carimba o
 * agendamento seguinte.
 */
export function pacienteSelecionado(campos) {
  const argumento = campo(campos ?? {}, '__EVENTARGUMENT') ?? '';
  if (!argumento.startsWith('SELECIONAR_PACIENTE|')) return null;
  const id = argumento.slice('SELECIONAR_PACIENTE|'.length).trim();
  return ehGuid(id) ? id : null;
}

/**
 * O id do paciente recém-criado só existe na RESPOSTA do salvar — medido no corpo do
 * `__ASYNCPOST` do Telerik, no hidden `hidPacienteId`, e na `action` da página recarregada
 * (`CadastroPaciente.aspx?id=<guid>`).
 */
export function idDoPacienteNaResposta(texto) {
  if (!texto) return null;
  const m =
    texto.match(/hidPacienteId"[^>]{0,80}?value="([0-9a-f-]{36})"/i) ||
    texto.match(/hidPacienteId[^>]{0,80}?value=([0-9a-f-]{36})/i) ||
    texto.match(/CadastroPaciente\.aspx\?id=([0-9a-f-]{36})/i);
  return m && ehGuid(m[1]) ? m[1] : null;
}
