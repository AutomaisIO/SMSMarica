import {
  Activity,
  Baby,
  BarChart3,
  BellRing,
  Bot,
  Bug,
  Bus,
  Building2,
  Calculator,
  PieChart,
  CalendarClock,
  CalendarPlus,
  BookOpen,
  ClipboardCheck,
  ClipboardList,
  DownloadCloud,
  DatabaseZap,
  HeartPulse,
  FileCog,
  FileSignature,
  FileText,
  FlaskConical,
  Image as ImageIcon,
  Inbox,
  LifeBuoy,
  Folder,
  KeyRound,
  KeySquare,
  Layers,
  LayoutDashboard,
  Map,
  MessageCircle,
  Route,
  ScanLine,
  ScrollText,
  Settings2,
  Sparkles,
  Star,
  Stethoscope,
  Truck,
  UserCog,
  Users,
  Wrench,
  Zap,
  type LucideIcon,
} from 'lucide-react';
import type { ModuloPermissao } from '@/shared/auth/authStore';

export type ItemMenu = {
  rotulo: string;
  to: string;
  icone: LucideIcon;
  /** Descrição opcional exibida no card da página-hub da seção. */
  descricao?: string;
  modulo?: ModuloPermissao;
  end?: boolean;
  /** Estado passado pra rota — usado p/ ações automáticas (ex.: abrir modal). */
  state?: unknown;
  /**
   * Ação especial no clique da sidebar em vez de navegar: 'chat' abre a Central de
   * Atendimento como janela FLUTUANTE (ChatWidget) — minimizada volta como está;
   * fechada reabre zerada. A rota `to` continua valendo para deep-link/hub.
   */
  acao?: 'chat';
  /**
   * Chave de badge/contador exibido ao lado do rótulo (notificação de tickets, ticket #42).
   * O valor numérico é resolvido no Sidebar via `useTicketsBadges`.
   */
  badge?: 'meusTickets' | 'ticketsGestao';
  /**
   * Gancho de encadeamento: destino/tela padrão interno do menu. Quando o menu
   * for favoritado na tela Início, o redirect leva a este destino em vez da
   * própria rota do menu. Hoje nenhum menu define — deixe vazio para cair na
   * própria `to`. (Ver Tarefa "Favoritar menu".)
   */
  destinoPadrao?: string;
};

export type SecaoMenu = {
  id: string;
  titulo?: string;
  icone?: LucideIcon;
  itens: ItemMenu[];
};

export const SECOES: SecaoMenu[] = [
  {
    id: 'inicio',
    itens: [{ rotulo: 'Início', to: '/app', icone: LayoutDashboard, end: true }],
  },
  {
    id: 'cadastros',
    titulo: 'Cadastros',
    icone: Folder,
    itens: [
      { rotulo: 'Pacientes', to: '/app/pacientes', icone: Users, modulo: 'Pacientes', descricao: 'Cadastre, edite e consulte pacientes.' },
      { rotulo: 'Médicos', to: '/app/medicos', icone: Stethoscope, modulo: 'Medicos', descricao: 'Cadastro de médicos.' },
      { rotulo: 'Profissionais', to: '/app/profissionais', icone: HeartPulse, modulo: 'Medicos', descricao: 'Demais profissionais de saúde.' },
      { rotulo: 'Unidades', to: '/app/unidades', icone: Building2, modulo: 'Unidades', descricao: 'Locais de saúde.' },
      { rotulo: 'Veículos', to: '/app/veiculos', icone: Bus, modulo: 'Veiculos', descricao: 'Frota do município.' },
      { rotulo: 'Motoristas', to: '/app/motoristas', icone: Truck, modulo: 'Motoristas', descricao: 'Agentes de transporte sanitário.' },
      { rotulo: 'Usuários', to: '/app/usuarios', icone: UserCog, modulo: 'Usuarios', descricao: 'Contas com acesso ao sistema.' },
      { rotulo: 'Tipos de tratamento', to: '/app/tipos-tratamento', icone: ClipboardList, modulo: 'TiposTratamento', descricao: 'Catálogo usado pelos tratamentos.' },
      { rotulo: 'Perfis', to: '/app/perfis', icone: KeyRound, modulo: 'Perfis', descricao: 'Conjuntos de permissões reutilizáveis.' },
    ],
  },
  {
    id: 'operacao',
    titulo: 'Transporte Pacientes',
    icone: Wrench,
    itens: [
      { rotulo: 'Tratamentos', to: '/app/tratamentos', icone: CalendarClock, modulo: 'Tratamentos', descricao: 'Periodicidade paciente ↔ unidade.' },
      { rotulo: 'Translados', to: '/app/translados', icone: Route, modulo: 'Translados', descricao: 'Rotas diárias e alocação de assentos.' },
      { rotulo: 'Mapa da frota', to: '/app/rastreamento/mapa', icone: Map, modulo: 'Rastreamento', descricao: 'Posição dos veículos no mapa.' },
      { rotulo: 'Rastreamento', to: '/app/rastreamento', icone: Activity, modulo: 'Rastreamento', end: true, descricao: 'GPS e geofences.' },
      { rotulo: 'Faturamento', to: '/app/faturamento', icone: Calculator, modulo: 'Faturamento', descricao: 'Consolidação de custos.' },
      { rotulo: 'Avaliações', to: '/app/avaliacoes', icone: Star, modulo: 'Avaliacoes', descricao: 'Feedback dos pacientes.' },
    ],
  },
  {
    id: 'agendamento',
    titulo: 'Agendamento',
    icone: CalendarClock,
    itens: [
      { rotulo: 'Marcar consulta', to: '/app/agendamentos/marcar', icone: CalendarPlus, modulo: 'Agendamentos', descricao: 'Nova marcação por especialidade.' },
      { rotulo: 'Agendas', to: '/app/agendas', icone: CalendarClock, modulo: 'Agendamentos', descricao: 'Agendas de profissionais e equipamentos.' },
      { rotulo: 'Especialidades', to: '/app/especialidades', icone: Stethoscope, modulo: 'Especialidades', descricao: 'Catálogo de especialidades.' },
    ],
  },
  {
    id: 'sisreg',
    titulo: 'SISREG',
    icone: ClipboardList,
    itens: [
      { rotulo: 'Consultar SISREG', to: '/app/sisreg', icone: ClipboardList, modulo: 'Sisreg', end: true, descricao: 'Consulta integrada (só leitura).' },
      { rotulo: 'Importação SISREG', to: '/app/importacao-sisreg', icone: DownloadCloud, modulo: 'Sisreg', descricao: 'Preview e importação de agendamentos.' },
      {
        rotulo: 'Configuração SISREG',
        to: '/app/sisreg/configuracao',
        icone: Settings2,
        modulo: 'SisregConfiguracao',
        descricao: 'Credenciais e parâmetros.',
      },
    ],
  },
  {
    id: 'solicitacoes',
    titulo: 'Solicitações',
    icone: ClipboardCheck,
    itens: [
      {
        rotulo: 'Exames',
        to: '/app/solicitacoes-exame',
        icone: ClipboardCheck,
        modulo: 'SolicitacoesExame',
        descricao: 'Pedidos de exame de imagem e worklist.',
      },
      {
        rotulo: 'Consultas',
        to: '/app/consultas',
        icone: Stethoscope,
        modulo: 'Consultas',
        descricao: 'Consultas reguladas do SISREG (sem imagem/laudo).',
      },
      {
        rotulo: 'Mapeamento pendente',
        to: '/app/mapeamento-sigtap',
        icone: Sparkles,
        modulo: 'MapeamentoSigtap',
        descricao: 'Vincular SIGTAP → tipo de exame (exames importados sem tipo).',
      },
      {
        rotulo: 'Tipos de Exame',
        to: '/app/tipos-exame',
        icone: Layers,
        modulo: 'TiposExame',
        descricao: 'Catálogo de tipos de exame.',
      },
      {
        rotulo: 'Catálogo SIGTAP',
        to: '/app/procedimentos-sigtap',
        icone: BookOpen,
        modulo: 'ProcedimentosSigtap',
        descricao: 'Procedimentos do SUS.',
      },
    ],
  },
  {
    id: 'imagens',
    titulo: 'Exames de Imagem',
    icone: ImageIcon,
    itens: [
      {
        rotulo: 'Abrir Exame',
        to: '/app/pacs',
        icone: ScanLine,
        modulo: 'Pacs',
        descricao: 'Visualizador de imagens DICOM.',
      },
      {
        rotulo: 'Laudos',
        to: '/app/laudos',
        icone: FileText,
        modulo: 'Laudos',
        descricao: 'Listagem e edição de laudos.',
      },
      {
        rotulo: 'Templates de Laudo',
        to: '/app/laudo-templates',
        icone: FileCog,
        modulo: 'LaudosTemplates',
        descricao: 'Modelos estruturados de laudo.',
      },
      {
        rotulo: 'Configuração de Laudo',
        to: '/app/laudo-configuracao',
        icone: FileSignature,
        modulo: 'ConfiguracaoLaudo',
        descricao: 'Cabeçalho, rodapé e assinatura.',
      },
      {
        rotulo: 'Equipamentos',
        to: '/app/equipamentos',
        icone: ScanLine,
        modulo: 'Equipamentos',
        descricao: 'Modalidades, unidade e AE Title da worklist.',
      },
    ],
  },
  {
    id: 'pep-sincronizacao',
    titulo: 'Sincronização PEP',
    icone: DatabaseZap,
    itens: [
      {
        rotulo: 'Importar prontuários',
        to: '/app/pep-sincronizacao',
        icone: DatabaseZap,
        modulo: 'SincronizacaoPep',
        descricao: 'Importação de PEPs externos.',
      },
    ],
  },
  {
    id: 'integracoes',
    titulo: 'Integrações',
    icone: KeySquare,
    itens: [
      { rotulo: 'Credenciais', to: '/app/integracoes', icone: Settings2, modulo: 'IntegracoesConfig', descricao: 'Provedores e credenciais.' },
      { rotulo: 'API Tokens', to: '/app/api-tokens', icone: KeySquare, modulo: 'ApiTokens', descricao: 'Tokens de acesso à API.' },
    ],
  },
  {
    id: 'indicadores',
    titulo: 'Indicadores',
    icone: BarChart3,
    itens: [
      {
        rotulo: 'Adulto',
        to: '/app/indicadores/adulto',
        icone: BarChart3,
        modulo: 'Indicadores',
        descricao: 'Urgência e internação de adultos do HMCML.',
      },
      {
        rotulo: 'Pediátrico',
        to: '/app/indicadores/pediatrico',
        icone: Baby,
        modulo: 'Indicadores',
        descricao: 'Urgência e internação do centro pediátrico.',
      },
      {
        rotulo: 'Materno Infantil',
        to: '/app/indicadores/materno-infantil',
        icone: Baby,
        modulo: 'Indicadores',
        descricao: 'Urgência e internação do centro materno infantil.',
      },
      {
        rotulo: 'Perfil Epidemiológico',
        to: '/app/indicadores/perfil-epidemiologico',
        icone: PieChart,
        modulo: 'Indicadores',
        descricao: 'Distribuição dos atendimentos e das internações.',
      },
      {
        rotulo: 'Institucional',
        to: '/app/indicadores/institucional',
        icone: Building2,
        modulo: 'Indicadores',
        descricao: 'Desempenho institucional (RH, custos, faturamento).',
      },
    ],
  },
  {
    id: 'inteligencia',
    titulo: 'Inteligência',
    icone: Sparkles,
    itens: [
      { rotulo: 'IA', to: '/app/ia', icone: Sparkles, modulo: 'Inteligencia', end: true, descricao: 'Consulta em linguagem natural.' },
      {
        rotulo: 'Configuração IA',
        to: '/app/ia/configuracao',
        icone: Settings2,
        modulo: 'InteligenciaConfiguracao',
        descricao: 'Provedores e parâmetros da IA.',
      },
      {
        rotulo: 'Melhorias da IA',
        to: '/app/ia/melhorias',
        icone: Sparkles,
        modulo: 'InteligenciaAprendizado',
        descricao: 'Aprendizado e ajustes.',
      },
      {
        rotulo: 'Agente IA',
        to: '/app/agente-ia',
        icone: Bot,
        modulo: 'AgenteIa',
        descricao: 'Terminal do agente no servidor (administrativo).',
      },
    ],
  },
  {
    id: 'atendimento',
    titulo: 'Atendimento',
    icone: MessageCircle,
    itens: [
      {
        rotulo: 'Central de Atendimento',
        to: '/app/conversas',
        icone: MessageCircle,
        modulo: 'Conversas',
        descricao: 'Chat de WhatsApp com os cidadãos (por unidade).',
        // Abre a janela flutuante do chat (não navega) — ticket #18.
        acao: 'chat',
      },
      {
        rotulo: 'Notificações de Agendamento',
        to: '/app/notificacoes-agendamento',
        icone: BellRing,
        modulo: 'NotificacoesAgendamento',
        descricao: 'Envio e confirmação de exames pelo WhatsApp.',
      },
      {
        rotulo: 'Mensagens Prontas',
        to: '/app/respostas-rapidas',
        icone: Zap,
        modulo: 'RespostasRapidas',
        descricao: 'Respostas rápidas que os atendentes usam no chat.',
      },
      {
        rotulo: 'Estatísticas',
        to: '/app/estatisticas',
        icone: BarChart3,
        modulo: 'Estatistica',
        descricao: 'Retrato do WhatsApp: envios, sessões, entregas e atendentes.',
      },
    ],
  },
  {
    id: 'suporte',
    titulo: 'Suporte',
    icone: LifeBuoy,
    itens: [
      {
        rotulo: 'Meus Tickets',
        to: '/app/tickets',
        icone: LifeBuoy,
        end: true,
        badge: 'meusTickets',
        descricao: 'Reporte bugs, peça mudanças ou tire dúvidas.',
      },
      {
        rotulo: 'Gestão de Tickets',
        to: '/app/tickets/gestao',
        icone: Inbox,
        modulo: 'Ticket',
        badge: 'ticketsGestao',
        descricao: 'Ver, responder e triar todos os tickets.',
      },
    ],
  },
  {
    id: 'sistema',
    titulo: 'Sistema',
    icone: ScrollText,
    itens: [
      {
        rotulo: 'Auditoria',
        to: '/app/auditoria',
        icone: ScrollText,
        modulo: 'Auditoria',
        descricao: 'Trilha de alterações do sistema (somente leitura).',
      },
      {
        rotulo: 'Erros do sistema',
        to: '/app/erros',
        icone: Bug,
        modulo: 'Erros',
        descricao: 'Log de erros (500) para diagnóstico pelo código de referência.',
      },
      {
        rotulo: 'Sandbox (QA)',
        to: '/app/sandbox',
        icone: FlaskConical,
        modulo: 'Sandbox',
        descricao: 'Testar magic link, PWA e confirmação com um paciente real.',
      },
    ],
  },
];

/** Rota base das páginas-hub de cada seção. */
export const ROTA_HUB = '/app/menu';

/** Caminho da página-hub de uma seção. */
export function caminhoHub(secaoId: string): string {
  return `${ROTA_HUB}/${secaoId}`;
}

export function encontrarSecaoPorId(secaoId: string | undefined): SecaoMenu | undefined {
  return SECOES.find((s) => s.id === secaoId);
}

/** Localiza um item de menu pela sua rota (`to`). */
export function encontrarItemPorTo(to: string): ItemMenu | undefined {
  for (const secao of SECOES) {
    const item = secao.itens.find((i) => i.to === to);
    if (item) return item;
  }
  return undefined;
}

/**
 * Resolve o destino final de um menu favoritado — encadeamento do favorito:
 * o favorito do Início aponta para a rota do menu; o menu, por sua vez, pode
 * ter uma tela padrão interna (`destinoPadrao`). Se não houver, cai na própria
 * rota do menu.
 */
export function resolverDestinoMenu(to: string): string {
  const item = encontrarItemPorTo(to);
  return item?.destinoPadrao ?? to;
}

function itemCasaPath(item: ItemMenu, path: string): boolean {
  if (item.end) return path === item.to;
  return path === item.to || path.startsWith(`${item.to}/`);
}

/**
 * Dada uma rota atual, encontra a seção (com título) à qual ela pertence —
 * escolhendo o item de maior prefixo casado para evitar ambiguidade.
 */
export function encontrarSecaoPorPath(path: string): SecaoMenu | undefined {
  let melhor: { secao: SecaoMenu; tamanho: number } | undefined;
  for (const secao of SECOES) {
    if (!secao.titulo) continue;
    for (const item of secao.itens) {
      if (itemCasaPath(item, path) && (!melhor || item.to.length > melhor.tamanho)) {
        melhor = { secao, tamanho: item.to.length };
      }
    }
  }
  return melhor?.secao;
}
