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
  { id: 'TiposExame', rotulo: 'Tipos de exame' },
  { id: 'ProcedimentosSigtap', rotulo: 'Catálogo SIGTAP' },
  { id: 'Pacs', rotulo: 'PACS' },
  { id: 'Laudos', rotulo: 'Laudos' },
  { id: 'LaudosTemplates', rotulo: 'Templates de laudo' },
  { id: 'ConfiguracaoLaudo', rotulo: 'Configuração de laudo (cabeçalho/rodapé)' },
  { id: 'Inteligencia', rotulo: 'IA' },
  { id: 'InteligenciaConfiguracao', rotulo: 'Configuração IA' },
  { id: 'InteligenciaAprendizado', rotulo: 'Aprendizado IA' },
  { id: 'Especialidades', rotulo: 'Especialidades' },
  { id: 'Equipamentos', rotulo: 'Equipamentos' },
  { id: 'Agendamentos', rotulo: 'Agendamentos' },
  { id: 'Sisreg', rotulo: 'SISREG (consulta)' },
  { id: 'SisregConfiguracao', rotulo: 'Configuração SISREG' },
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
  { id: 'Sandbox', rotulo: 'Sandbox de testes (QA)' },
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
};
