import {
  Activity,
  BarChart3,
  BellRing,
  CalendarCheck2,
  FilePlus2,
  Bot,
  BrainCircuit,
  Bug,
  Bus,
  Building2,
  Calculator,
  CalendarClock,
  BookOpen,
  ClipboardCheck,
  ClipboardList,
  DownloadCloud,
  DatabaseZap,
  HeartPulse,
  Hourglass,
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
  Landmark,
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
  Target,
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
  badge?: 'meusTickets' | 'ticketsGestao' | 'painelInicio';
  /**
   * Gancho de encadeamento: destino/tela padrão interno do menu. Quando o menu
   * for favoritado na tela Início, o redirect leva a este destino em vez da
   * própria rota do menu. Hoje nenhum menu define — deixe vazio para cair na
   * própria `to`. (Ver Tarefa "Favoritar menu".)
   */
  destinoPadrao?: string;
  /**
   * Sub-itens de um 3º nível (grupo dentro da seção). Quando presente, este item
   * vira um sub-grupo COLAPSÁVEL: o clique no rótulo abre/fecha a lista de
   * `subItens` em vez de (só) navegar. Usado em Regulação → SER/SERNIT → telas de
   * cada fonte. A visibilidade do sub-grupo segue os `subItens` acessíveis.
   */
  subItens?: ItemMenu[];
};

export type SecaoMenu = {
  id: string;
  titulo?: string;
  icone?: LucideIcon;
  itens: ItemMenu[];
  /**
   * Mantém o grupo (título + expansão) na sidebar mesmo com um único item, em vez
   * de achatar o item para o topo. Usado quando a seção deve crescer no futuro —
   * ex.: Indicadores hoje só tem "Conde", mas entrarão outras unidades.
   */
  manterGrupo?: boolean;
};

export const SECOES: SecaoMenu[] = [
  {
    id: 'inicio',
    // O badge é condição de existência do painel, não enfeite: quem tem menu favorito é
    // redirecionado ao entrar e NUNCA vê a home. Sem o contador aqui, o painel nasceria invisível
    // justamente para os usuários mais frequentes (ADR-0033 §8).
    itens: [{ rotulo: 'Início', to: '/app', icone: LayoutDashboard, end: true, badge: 'painelInicio' }],
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
    // Era "Agendamento", com a agenda propria do municipio — removida em 05/09/2026 por nunca ter
    // saido de 3 linhas de teste e por assumir "1 paciente por slot", incompativel com o bloco de N
    // vagas que o SISREG publica. O que fica e a leitura da agenda REGULADA.
    id: 'agenda',
    titulo: 'Agenda',
    icone: CalendarClock,
    itens: [
      {
        rotulo: 'Consultar agenda',
        to: '/app/agenda',
        icone: CalendarClock,
        modulo: 'Agenda',
        descricao: 'Oferta de vagas do SISREG x ocupacao: quem atende, quando, e quanto esta livre.',
      },
      {
        rotulo: 'Analise de vagas',
        to: '/app/agenda/analise',
        icone: BarChart3,
        modulo: 'Agenda',
        descricao: 'Ocupacao por unidade, especialidade e profissional; ociosidade e sobrecarga.',
      },
      {
        rotulo: 'Demanda regulada',
        to: '/app/agenda/demanda',
        icone: Hourglass,
        modulo: 'Agenda',
        descricao: 'Top procedimentos, tempo de espera e origem da demanda.',
      },
      {
        // Estratégias de fila (ADR-0058): simulador determinístico + agente que só escolhe
        // parâmetros livres. Só planejamento — nada escreve no SISREG; a estratégia fica no
        // nosso banco para consulta.
        rotulo: 'Estratégias de fila',
        to: '/app/agenda/estrategias',
        icone: Target,
        modulo: 'EstrategiasFila',
        descricao: 'Simule mudanças na oferta e peça ao agente uma estratégia para zerar a fila de um procedimento.',
      },
      {
        rotulo: 'Ofertas',
        to: '/app/ofertas',
        icone: Sparkles,
        modulo: 'AlteracoesAgenda',
        descricao: 'O que abriu no SISREG: agenda nova e horario que vagou por cancelamento.',
      },
      {
        rotulo: 'Alteracoes de Agenda',
        to: '/app/alteracoes-agenda',
        icone: CalendarClock,
        modulo: 'AlteracoesAgenda',
        descricao: 'O que o SISREG remarcou, trocou ou cancelou em agendamentos ja importados.',
      },
    ],
  },
  {
    id: 'regulacao',
    titulo: 'Regulação',
    icone: ClipboardCheck,
    itens: [
      {
        // Módulo Solicitações (ADR-0052): a unidade abre aqui, e o pedido cai na
        // pré-regulação. Permissão 47 — quem só consulta SER/SERNIT não enxerga.
        // Fica em primeiro no grupo porque é o ponto de entrada; SER e SERNIT abaixo são as
        // filas espelhadas dos sistemas de terceiro.
        rotulo: 'Solicitações',
        to: '/app/regulacao/solicitacoes',
        icone: ClipboardList,
        modulo: 'Regulacao',
        descricao:
          'Abertura, fila de pré-regulação e acompanhamento das solicitações (SISREG, SER, SERNIT).',
        subItens: [
          {
            rotulo: 'Minha fila',
            to: '/app/regulacao/solicitacoes',
            icone: ClipboardList,
            modulo: 'Regulacao',
            end: true,
            descricao: 'O que as suas unidades abriram e em que pé está cada pedido.',
          },
          {
            rotulo: 'Nova solicitação',
            to: '/app/regulacao/solicitacoes/nova',
            icone: FilePlus2,
            modulo: 'Regulacao',
            descricao: 'Abre uma solicitação: procedimento, destino, paciente, formulário e anexos.',
          },
          {
            rotulo: 'Fila da regulação',
            to: '/app/regulacao/solicitacoes/regulacao',
            icone: ClipboardCheck,
            modulo: 'RegulacaoTriagem',
            descricao: 'Todas as unidades do município — a visão do agente regulador.',
          },
          {
            rotulo: 'Notificações',
            to: '/app/regulacao/solicitacoes/notificacoes',
            icone: BellRing,
            modulo: 'Regulacao',
            descricao: 'Movimentações das solicitações que ainda não foram vistas.',
          },
          {
            rotulo: 'Regras de elegibilidade',
            to: '/app/regulacao/regras',
            icone: BookOpen,
            modulo: 'RegulacaoConfiguracao',
            descricao: 'O que o manual exige por procedimento — curadoria e importação.',
          },
        ],
      },
      {
        // 2º nível: SER (Estado / SES-RJ). O clique expande os submenus (3º nível).
        rotulo: 'SER',
        to: '/app/regulacao/ser',
        icone: ClipboardList,
        modulo: 'RegulacaoSer',
        descricao: 'Fila do Estado (SES-RJ): fila, nova solicitação, notificações e configuração.',
        subItens: [
          {
            rotulo: 'Fila',
            to: '/app/regulacao/ser',
            icone: ClipboardList,
            modulo: 'RegulacaoSer',
            end: true,
            descricao: 'Fila do Estado (SES-RJ) espelhada — consulta e histórico.',
          },
          {
            rotulo: 'Nova solicitação',
            to: '/app/regulacao/nova-solicitacao',
            icone: FilePlus2,
            modulo: 'RegulacaoSer',
            descricao: 'Monta um pedido no formato do SER — campos variam por recurso.',
          },
          {
            rotulo: 'Notificações',
            to: '/app/regulacao/notificacoes',
            icone: BellRing,
            modulo: 'RegulacaoSer',
            descricao: 'Movimentações do SER que ainda não foram vistas.',
          },
          {
            rotulo: 'Configuração',
            to: '/app/regulacao/configuracao',
            icone: Settings2,
            modulo: 'RegulacaoConfiguracao',
            descricao: 'Credenciais e motor de atualização do SER (Estado).',
          },
        ],
      },
      {
        // 2º nível: SERNIT (SER de Niterói). Mesmos submenus, sob /regulacao/sernit.
        rotulo: 'SERNIT',
        to: '/app/regulacao/sernit',
        icone: ClipboardList,
        modulo: 'RegulacaoSernit',
        descricao: 'Fila de Niterói (SERNIT): fila, nova solicitação, notificações e configuração.',
        subItens: [
          {
            rotulo: 'Fila',
            to: '/app/regulacao/sernit',
            icone: ClipboardList,
            modulo: 'RegulacaoSernit',
            end: true,
            descricao: 'Fila de Niterói (SERNIT) espelhada — consulta e histórico.',
          },
          {
            rotulo: 'Nova solicitação',
            to: '/app/regulacao/sernit/nova-solicitacao',
            icone: FilePlus2,
            modulo: 'RegulacaoSernit',
            descricao: 'Monta um pedido no formato do SERNIT — campos variam por recurso.',
          },
          {
            rotulo: 'Notificações',
            to: '/app/regulacao/sernit/notificacoes',
            icone: BellRing,
            modulo: 'RegulacaoSernit',
            descricao: 'Movimentações do SERNIT que ainda não foram vistas.',
          },
          {
            rotulo: 'Configuração',
            to: '/app/regulacao/sernit/configuracao',
            icone: Settings2,
            modulo: 'RegulacaoConfiguracao',
            descricao: 'Credenciais e motor de atualização do SERNIT (Niterói).',
          },
        ],
      },
      {
        // 2º nível: SISREG III (regulação federal). Trazido para dentro de Regulação.
        rotulo: 'SISREG',
        to: '/app/sisreg',
        icone: ClipboardList,
        modulo: 'Sisreg',
        descricao: 'SISREG III: consulta, importação de agendamentos e configuração.',
        subItens: [
          {
            rotulo: 'Consultar',
            to: '/app/sisreg',
            icone: ClipboardList,
            modulo: 'Sisreg',
            end: true,
            descricao: 'Consulta integrada (só leitura).',
          },
          {
            rotulo: 'Importação',
            to: '/app/importacao-sisreg',
            icone: DownloadCloud,
            modulo: 'Sisreg',
            descricao: 'Preview e importação de agendamentos.',
          },
          // "Mapeamento" saiu daqui: virou a aba SISREG do detalhe da unidade
          // (/app/unidades/{id}), junto com a credencial e o sincronismo diário. Manter os dois
          // caminhos duplicaria manutenção e deixaria duas verdades sobre a mesma unidade.
          {
            rotulo: 'Estatísticas',
            to: '/app/sisreg/estatisticas',
            icone: BarChart3,
            modulo: 'Sisreg',
            descricao: 'Trabalho dos operadores da regulação: equipe, individual e rankings.',
          },
          {
            rotulo: 'Configuração',
            to: '/app/sisreg/configuracao',
            icone: Settings2,
            modulo: 'SisregConfiguracao',
            descricao: 'Credenciais e parâmetros do SISREG.',
          },
        ],
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
      {
        rotulo: 'Relatórios e Estatísticas',
        to: '/app/relatorios-imagem',
        icone: BarChart3,
        modulo: 'Estatistica',
        descricao: 'Dashboard de exames, laudos, tempos médios e exportação analítica.',
      },
    ],
  },
  {
    id: 'pep-sincronizacao',
    titulo: 'Importar Prontuários',
    icone: DatabaseZap,
    itens: [
      {
        rotulo: 'Painel',
        to: '/app/pep-sincronizacao',
        icone: DatabaseZap,
        modulo: 'SincronizacaoPep',
        descricao: 'Status dos motores e divergências de importação.',
      },
      {
        rotulo: 'Fontes / Conectores',
        to: '/app/pep-sincronizacao/fontes',
        icone: Layers,
        modulo: 'SincronizacaoPep',
        descricao: 'Bases (banco/agente) e conectores web (Klinikos) de prontuário.',
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
    manterGrupo: true,
    itens: [
      {
        rotulo: 'Conde',
        to: '/app/indicadores/conde',
        icone: BarChart3,
        modulo: 'Indicadores',
        descricao: 'Indicadores contratuais do Hospital Municipal Conde Modesto Leal.',
      },
    ],
  },
  {
    id: 'consulta-inteligente',
    itens: [
      {
        rotulo: 'Consulta Inteligente',
        to: '/app/consulta-inteligente',
        icone: BrainCircuit,
        modulo: 'Inteligencia',
        end: true,
        descricao: 'Pergunte sobre os dados em linguagem natural (chat com gráficos).',
      },
    ],
  },
  {
    id: 'inteligencia',
    titulo: 'Inteligência',
    icone: Sparkles,
    itens: [
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
        rotulo: 'Confirmações',
        to: '/app/confirmacoes',
        icone: CalendarCheck2,
        modulo: 'Confirmacoes',
        descricao: 'Aviso de agendamento do SISREG pelo WhatsApp: fila, respostas dos pacientes e regras.',
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
        rotulo: 'Robô de Atendimento',
        to: '/app/robo-atendimento',
        icone: Bot,
        modulo: 'RoboAtendimento',
        descricao: 'Assuntos, treinos e comandos do robô que responde no WhatsApp.',
      },
      {
        rotulo: 'Pendências de Cadastro',
        to: '/app/pendencias-cadastro',
        icone: Inbox,
        modulo: 'AjusteCadastro',
        descricao: 'Números errados sinalizados pelo cidadão, para a recepção ajustar o cadastro.',
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
        rotulo: 'Instituição',
        to: '/app/instituicao',
        icone: Landmark,
        modulo: 'Instituicao',
        descricao: 'Nome, marca, domínios e contatos legais desta instância.',
      },
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
        rotulo: 'Avisos no celular',
        to: '/app/avisos-celular',
        icone: BellRing,
        modulo: 'Erros',
        descricao: 'Quem recebe no WhatsApp os erros da plataforma, o que é reportado e o que já saiu.',
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

/** Itens da seção achatados: cada item e, quando houver, seus `subItens` (3º nível). */
function itensAchatados(secao: SecaoMenu): ItemMenu[] {
  return secao.itens.flatMap((i) => (i.subItens ? [i, ...i.subItens] : [i]));
}

/** Localiza um item de menu pela sua rota (`to`) — inclui sub-itens de 3º nível. */
export function encontrarItemPorTo(to: string): ItemMenu | undefined {
  for (const secao of SECOES) {
    const item = itensAchatados(secao).find((i) => i.to === to);
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
    for (const item of itensAchatados(secao)) {
      if (itemCasaPath(item, path) && (!melhor || item.to.length > melhor.tamanho)) {
        melhor = { secao, tamanho: item.to.length };
      }
    }
  }
  return melhor?.secao;
}
