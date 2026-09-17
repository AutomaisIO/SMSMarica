import type { AcaoPermissao, ModuloPermissao } from '@/shared/auth/authStore';
import type { MatrizEdicao, PermissaoModuloApi } from '@/features/perfis/types';

const TODAS: AcaoPermissao[] = ['Consulta', 'Inclusao', 'Edicao', 'Exclusao'];

export function parseAcoes(s: string): AcaoPermissao[] {
  if (!s || s === 'Nenhuma') return [];
  if (s === 'Todas') return [...TODAS];
  return s
    .split(/\s*,\s*/)
    .filter(Boolean)
    .filter((x): x is AcaoPermissao => (TODAS as string[]).includes(x));
}

export function serializarAcoes(acoes: AcaoPermissao[]): string {
  if (!acoes.length) return 'Nenhuma';
  if (acoes.length === TODAS.length) return 'Todas';
  return acoes.join(', ');
}

/** Converte as permissões do backend para o formato de edição (matriz). */
export function paraMatriz(permissoes: PermissaoModuloApi[]): MatrizEdicao {
  const r: MatrizEdicao = {};
  for (const p of permissoes) r[p.modulo] = parseAcoes(p.acoes);
  return r;
}

/** Converte a matriz para o formato do backend (lista). Inclui apenas módulos com ações. */
export function deMatriz(matriz: MatrizEdicao): PermissaoModuloApi[] {
  const out: PermissaoModuloApi[] = [];
  for (const modulo of Object.keys(matriz) as ModuloPermissao[]) {
    const acoes = matriz[modulo] ?? [];
    if (acoes.length === 0) continue;
    out.push({ modulo, acoes: serializarAcoes(acoes) });
  }
  return out;
}

/** Lista oficial dos módulos, com rótulo humano. Mantém a mesma ordem do
 *  Sidebar para a edição de Perfis ficar previsível. */
export const MODULOS: { id: ModuloPermissao; rotulo: string }[] = [
  { id: 'Pacientes', rotulo: 'Pacientes' },
  { id: 'Medicos', rotulo: 'Médicos' },
  { id: 'Unidades', rotulo: 'Unidades' },
  { id: 'Veiculos', rotulo: 'Veículos' },
  { id: 'Motoristas', rotulo: 'Motoristas' },
  { id: 'Usuarios', rotulo: 'Usuários' },
  { id: 'TiposTratamento', rotulo: 'Tipos de tratamento' },
  { id: 'Perfis', rotulo: 'Perfis' },
  { id: 'Tratamentos', rotulo: 'Tratamentos' },
  { id: 'Translados', rotulo: 'Translados' },
  { id: 'Rastreamento', rotulo: 'Rastreamento' },
  { id: 'Avaliacoes', rotulo: 'Avaliações' },
  { id: 'SolicitacoesExame', rotulo: 'Solicitações de exame' },
  { id: 'SolicitacaoExameManual', rotulo: 'Solicitação manual de exame (botão "Nova solicitação")' },
  { id: 'TiposExame', rotulo: 'Tipos de exame' },
  { id: 'ProcedimentosSigtap', rotulo: 'Catálogo SIGTAP' },
  { id: 'Pacs', rotulo: 'PACS' },
  { id: 'Laudos', rotulo: 'Laudos' },
  { id: 'LaudosTemplates', rotulo: 'Templates de laudo' },
  { id: 'ConfiguracaoLaudo', rotulo: 'Configuração de laudo (cabeçalho/rodapé)' },
  { id: 'Inteligencia', rotulo: 'Consulta Inteligente' },
  { id: 'InteligenciaConsultaDev', rotulo: 'Consulta Inteligente — modo desenvolvedor (mostra o raciocínio)' },
  {
    id: 'InteligenciaAtendimento',
    rotulo: 'Consulta Inteligente — base Atendimento (lê as conversas com pacientes da rede toda)',
  },
  { id: 'InteligenciaConfiguracao', rotulo: 'Configuração IA' },
  { id: 'Confirmacoes', rotulo: 'Confirmações de agendamento (fila, respostas dos pacientes e regras de envio)' },
  { id: 'InteligenciaAprendizado', rotulo: 'Aprendizado IA' },
  { id: 'AgenteIa', rotulo: 'Agente IA — terminal no servidor (administrativo)' },
  { id: 'Indicadores', rotulo: 'Indicadores contratuais do HMCML' },
  { id: 'Equipamentos', rotulo: 'Equipamentos' },
  { id: 'Sisreg', rotulo: 'SISREG (consulta)' },
  { id: 'SisregConfiguracao', rotulo: 'Configuração SISREG' },
  { id: 'SisregMapeamento', rotulo: 'Mapeamento SISREG (profissionais e procedimentos da unidade)' },
  { id: 'SincronizacaoPep', rotulo: 'Sincronização PEP (Salux)' },
  { id: 'ApiTokens', rotulo: 'API Tokens' },
  { id: 'IntegracoesConfig', rotulo: 'Integrações (credenciais)' },
  { id: 'Faturamento', rotulo: 'Faturamento (TFD/SUS)' },
  { id: 'Auditoria', rotulo: 'Auditoria (trilha do sistema)' },
  { id: 'Erros', rotulo: 'Erros do sistema (diagnóstico)' },
  { id: 'Conversas', rotulo: 'Central de Atendimento (chat)' },
  { id: 'ConversasSupervisao', rotulo: 'Atendimento — supervisão (ver todas as unidades)' },
  { id: 'Ticket', rotulo: 'Suporte — gestão de tickets (ver/responder todos)' },
  { id: 'NotificacoesAgendamento', rotulo: 'Notificações de agendamento (WhatsApp)' },
  { id: 'RespostasRapidas', rotulo: 'Mensagens prontas do chat (cadastro)' },
  { id: 'Estatistica', rotulo: 'Estatísticas de atendimento (retrato WhatsApp)' },
  { id: 'Sandbox', rotulo: 'Sandbox de testes (QA)' },
  { id: 'Consultas', rotulo: 'Consultas reguladas (SISREG)' },
  { id: 'MapeamentoSigtap', rotulo: 'Mapeamento SIGTAP → tipo de exame' },
  // Do Processo Regulatório, entram na matriz só os que hoje concedem alguma coisa de fato: os
  // TRÊS de visão global (lente "Município" do painel de início, ADR-0033 §5) e, desde o
  // ADR-0042, SER e Configuração — que passaram a ter endpoint e tela.
  // `Regulacao` (47) entrou em 06/09/2026 (ADR-0052): ganhou endpoints e a tela de abertura de
  // solicitação, então deixou de ser um enum sem uso. É a permissão da UNIDADE SOLICITANTE —
  // quem abre o pedido; ver o SER/SERNIT é outra coisa e continua em módulo próprio.
  { id: 'Regulacao', rotulo: 'Regulação — Solicitações (unidade solicitante: abre, anexa e envia à pré-regulação)' },
  { id: 'RegulacaoTriagem', rotulo: 'Regulação — Agente regulador (vê todas as unidades, assume, ajusta e envia aos sistemas)' },
  { id: 'RegulacaoMedica', rotulo: 'Regulação — médico regulador (visão do município no painel)' },
  { id: 'RegulacaoAgendamento', rotulo: 'Regulação — agendamento (visão do município no painel)' },
  { id: 'RegulacaoSer', rotulo: 'Regulação — SER: fila do Estado (Edição = registrar FollowUP no SER)' },
  { id: 'RegulacaoSernit', rotulo: 'Regulação — SERNIT: fila de Niterói (Edição = FollowUP/telefones no SERNIT)' },
  { id: 'RegulacaoConfiguracao', rotulo: 'Regulação — Configuração (credenciais, motor do SER/SERNIT, catálogo e regras)' },
  { id: 'CorrecaoIdentidadeExame', rotulo: 'Correção de identidade de exame (reescreve o DICOM no PACS)' },
  { id: 'PesquisaSatisfacao', rotulo: 'Pesquisa de satisfação — enviar ao paciente pelo histórico' },
  { id: 'Instituicao', rotulo: 'Instituição — identidade, marca e contatos legais desta instância' },
  { id: 'RoboAtendimento', rotulo: 'Robô de atendimento — cadastro de assuntos e comandos do bot' },
  { id: 'AjusteCadastro', rotulo: 'Pendências de cadastro (números errados) — ver e resolver' },
  { id: 'Agenda', rotulo: 'Agenda — oferta de vagas do SISREG, ocupação e análise' },
  {
    id: 'AlteracoesAgenda',
    rotulo: 'Alterações de agenda do SISREG — ver a fila, tratar e avisar o paciente',
  },
  {
    id: 'RevelarChaveSisreg',
    rotulo: 'Revelar chave de confirmação do SISREG (seção SISREG do exame/consulta)',
  },
];

export const ACOES: { id: AcaoPermissao; rotulo: string }[] = [
  { id: 'Consulta', rotulo: 'Consulta' },
  { id: 'Inclusao', rotulo: 'Inclusão' },
  { id: 'Edicao', rotulo: 'Edição' },
  { id: 'Exclusao', rotulo: 'Exclusão' },
];

/**
 * Apelidos contextuais por ação para um módulo específico. Quando as ações
 * genéricas (Consulta/Inclusão/Edição/Exclusão) têm um significado bem
 * particular no módulo, registrar aqui — usado nos tooltips dos checkboxes
 * e como subtítulo na linha do módulo.
 */
export const APELIDOS_ACOES_POR_MODULO: Partial<
  Record<ModuloPermissao, Partial<Record<AcaoPermissao, string>>>
> = {
  Pacs: {
    Consulta: 'Abrir exame',
    Edicao: 'Salvar anotações',
    Exclusao: 'Excluir exame',
  },
  // Só a Inclusão vale aqui: é a permissão que libera o botão "Nova solicitação" (ticket #89).
  SolicitacaoExameManual: {
    Inclusao: 'Abrir solicitação manual',
  },
  // Só a Edição vale aqui: é ela que mostra o card no pedido e libera a correção, que apaga e
  // reescreve o objeto no PACS. Sem apelido, "Edição" pareceria inofensivo.
  CorrecaoIdentidadeExame: {
    Edicao: 'Corrigir identidade de exame (reescreve o DICOM)',
  },
  // Só a Edição: é ela que mostra o botão no atendimento. "Edição" sem apelido pareceria
  // inofensivo — o que ela libera é MANDAR MENSAGEM para o cidadão.
  PesquisaSatisfacao: {
    Edicao: 'Enviar pesquisa de satisfação ao paciente (WhatsApp)',
  },
  // O que se edita aqui aparece na tela de login, no PDF de laudo e na página pública de
  // verificação — inclusive para quem NÃO está autenticado. Não é configuração operacional.
  // Só a Consulta: é ela que mostra o botão "Mostrar chave". A chave é a prova de comparecimento
  // no SISREG — "Consulta" sem apelido pareceria só ver o pedido.
  RevelarChaveSisreg: {
    Consulta: 'Mostrar a chave de confirmação (lida no SISREG, fica na auditoria)',
  },
  // Só a Consulta: é ela que faz a base Atendimento aparecer na Consulta Inteligente. "Consulta"
  // sem apelido pareceria inofensivo — o que ela libera é LER CONVERSAS de cidadão, sem recorte
  // de unidade. Toda pergunta fica na auditoria da IA.
  InteligenciaAtendimento: {
    Consulta: 'Perguntar sobre as conversas com pacientes (rede toda; fica na auditoria)',
  },
  Confirmacoes: {
    Consulta: 'Ver a fila, as respostas dos pacientes e as regras',
    Edicao: 'Alterar horário/vazão do envio, ligar unidades e recolocar mensagens na fila',
  },
  Instituicao: {
    Consulta: 'Ver a identidade da instituição',
    Edicao: 'Alterar nome, marca, domínios e contatos legais (afeta telas públicas)',
  },
};
