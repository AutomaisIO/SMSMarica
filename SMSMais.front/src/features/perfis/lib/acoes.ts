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

/** As abas da tela de permissões, por assunto. A ordem é a das abas. */
export const GRUPOS_PERMISSAO: { id: GrupoPermissao; rotulo: string }[] = [
  { id: 'cadastros', rotulo: 'Cadastros' },
  { id: 'regulacao', rotulo: 'Regulação' },
  { id: 'sisreg', rotulo: 'SISREG e agenda' },
  { id: 'exames', rotulo: 'Exames e laudos' },
  { id: 'atendimento', rotulo: 'Atendimento' },
  { id: 'transporte', rotulo: 'Transporte' },
  { id: 'ouvidoria', rotulo: 'Ouvidoria' },
  { id: 'estatisticas', rotulo: 'Estatísticas' },
  { id: 'ia', rotulo: 'Inteligência' },
  { id: 'sistema', rotulo: 'Sistema' },
];

export type GrupoPermissao =
  | 'cadastros'
  | 'regulacao'
  | 'sisreg'
  | 'exames'
  | 'atendimento'
  | 'transporte'
  | 'ouvidoria'
  | 'estatisticas'
  | 'ia'
  | 'sistema';

/**
 * Os módulos da matriz de permissões. `rotulo` é o nome curto que aparece na linha; `ajuda` é a
 * explicação, que só abre no "?" — junto com o que cada ação faz (`APELIDOS_ACOES_POR_MODULO`).
 * Módulo do enum que não está aqui não aparece na tela de propósito (reservado ou sem uso).
 */
export const MODULOS: { id: ModuloPermissao; rotulo: string; grupo: GrupoPermissao; ajuda?: string }[] = [
  // ---- Cadastros
  { id: 'Pacientes', grupo: 'cadastros', rotulo: 'Pacientes', ajuda: 'Cadastro do cidadão: busca, ficha, contatos e documentos.' },
  { id: 'Medicos', grupo: 'cadastros', rotulo: 'Médicos' },
  { id: 'Unidades', grupo: 'cadastros', rotulo: 'Unidades', ajuda: 'Unidades de saúde do município.' },
  { id: 'Usuarios', grupo: 'cadastros', rotulo: 'Usuários', ajuda: 'Contas de acesso ao painel.' },
  { id: 'Perfis', grupo: 'cadastros', rotulo: 'Perfis', ajuda: 'Perfis de acesso e as permissões de cada um.' },
  {
    id: 'Instituicao',
    grupo: 'cadastros',
    rotulo: 'Instituição',
    ajuda: 'Identidade, marca e contatos legais desta instância. Aparece na tela de login, no PDF do laudo e na página pública de verificação.',
  },
  { id: 'AjusteCadastro', grupo: 'cadastros', rotulo: 'Pendências de cadastro', ajuda: 'Números de telefone errados: ver e resolver.' },

  // ---- Regulação
  { id: 'Regulacao', grupo: 'regulacao', rotulo: 'Solicitações', ajuda: 'Lado da unidade solicitante: abre o pedido, anexa e envia à pré-regulação.' },
  {
    id: 'RegulacaoTriagem',
    grupo: 'regulacao',
    rotulo: 'Gestão de fila',
    ajuda: 'Lado do regulador: assume, aceita, devolve e recusa. Os pedidos que aparecem dependem das filas (NAR, Interna, Intermunicipal).',
  },
  { id: 'RegulacaoFilaNar', grupo: 'regulacao', rotulo: 'Fila NAR', ajuda: 'Pedidos NAR — agendamento indireto, em nome de outra unidade. Vale junto com a Gestão de fila.' },
  { id: 'RegulacaoFilaInterna', grupo: 'regulacao', rotulo: 'Fila Interna', ajuda: 'Pedidos internos, para o SISREG do município. Vale junto com a Gestão de fila.' },
  {
    id: 'RegulacaoFilaIntermunicipal',
    grupo: 'regulacao',
    rotulo: 'Fila Intermunicipal',
    ajuda: 'Pedidos para SER, SERNIT e ESUS de São Gonçalo. Vale junto com a Gestão de fila.',
  },
  { id: 'RegulacaoSer', grupo: 'regulacao', rotulo: 'SER', ajuda: 'Fila do Estado (SES-RJ).' },
  { id: 'RegulacaoSernit', grupo: 'regulacao', rotulo: 'SERNIT', ajuda: 'Fila de Niterói.' },
  { id: 'RegulacaoEsusSg', grupo: 'regulacao', rotulo: 'ESUS São Gonçalo', ajuda: 'Fila e agendados da PPI. A integração é só leitura.' },
  { id: 'RegulacaoMedica', grupo: 'regulacao', rotulo: 'Médico regulador', ajuda: 'Visão do município no painel de início.' },
  { id: 'RegulacaoAgendamento', grupo: 'regulacao', rotulo: 'Agendamento', ajuda: 'Visão do município no painel de início.' },
  { id: 'IndicadoresRegulacao', grupo: 'regulacao', rotulo: 'Indicadores de regulação', ajuda: 'SISREG, SER, SERNIT e ESUS SG, com exportação em PDF.' },
  {
    id: 'RegulacaoConfiguracao',
    grupo: 'regulacao',
    rotulo: 'Configuração da regulação',
    ajuda: 'Credenciais, motor do SER/SERNIT/ESUS São Gonçalo, catálogo de procedimentos e regras.',
  },

  // ---- SISREG e agenda
  { id: 'Sisreg', grupo: 'sisreg', rotulo: 'SISREG', ajuda: 'Consulta das solicitações e agendamentos do SISREG.' },
  {
    id: 'RevelarChaveSisreg',
    grupo: 'sisreg',
    rotulo: 'Chave de confirmação',
    ajuda: 'Mostra a chave de confirmação do SISREG — a prova de comparecimento. Cada revelação fica na auditoria.',
  },
  { id: 'Agenda', grupo: 'sisreg', rotulo: 'Agenda', ajuda: 'Oferta de vagas do SISREG, ocupação e análise.' },
  { id: 'AlteracoesAgenda', grupo: 'sisreg', rotulo: 'Alterações de agenda', ajuda: 'Fila das alterações de agenda do SISREG: tratar e avisar o paciente.' },
  {
    id: 'EstrategiasFila',
    grupo: 'sisreg',
    rotulo: 'Estratégias de fila',
    ajuda: 'Simular mudanças na oferta e pedir estratégia ao agente. Só planejamento: nada escreve no SISREG.',
  },
  { id: 'SisregMapeamento', grupo: 'sisreg', rotulo: 'Mapeamento do SISREG', ajuda: 'Profissionais e procedimentos da unidade.' },
  { id: 'SisregConfiguracao', grupo: 'sisreg', rotulo: 'Configuração do SISREG' },

  // ---- Exames e laudos
  { id: 'Solicitacoes', grupo: 'exames', rotulo: 'Exames e consultas', ajuda: 'As solicitações de exame e consulta.' },
  { id: 'SolicitacaoExameManual', grupo: 'exames', rotulo: 'Solicitação manual', ajuda: 'O botão "Nova solicitação" de exame.' },
  { id: 'Pacs', grupo: 'exames', rotulo: 'PACS', ajuda: 'Servidor de imagens.' },
  { id: 'Laudos', grupo: 'exames', rotulo: 'Laudos' },
  { id: 'LaudosTemplates', grupo: 'exames', rotulo: 'Modelos de laudo' },
  { id: 'ConfiguracaoLaudo', grupo: 'exames', rotulo: 'Configuração de laudo', ajuda: 'Cabeçalho e rodapé do laudo.' },
  { id: 'Equipamentos', grupo: 'exames', rotulo: 'Equipamentos' },
  { id: 'TiposExame', grupo: 'exames', rotulo: 'Tipos de exame' },
  { id: 'ProcedimentosSigtap', grupo: 'exames', rotulo: 'Catálogo SIGTAP' },
  { id: 'MapeamentoSigtap', grupo: 'exames', rotulo: 'SIGTAP → tipo de exame', ajuda: 'Mapeamento dos procedimentos SIGTAP para os tipos de exame.' },
  { id: 'CorrecaoIdentidadeExame', grupo: 'exames', rotulo: 'Correção de identidade', ajuda: 'Corrige o paciente de um exame reescrevendo o DICOM no PACS.' },

  // ---- Atendimento
  { id: 'Conversas', grupo: 'atendimento', rotulo: 'Central de Atendimento', ajuda: 'O chat com os pacientes.' },
  { id: 'ConversasSupervisao', grupo: 'atendimento', rotulo: 'Supervisão', ajuda: 'Ver as conversas de todas as unidades.' },
  { id: 'Confirmacoes', grupo: 'atendimento', rotulo: 'Confirmações', ajuda: 'Atendimento das confirmações de agendamento — as filas das atendentes.' },
  { id: 'ConfirmacoesEquipe', grupo: 'atendimento', rotulo: 'Confirmações — equipe', ajuda: 'Produção de cada atendente. Exige Confirmações.' },
  { id: 'NotificacoesAgendamento', grupo: 'atendimento', rotulo: 'Mensageria', ajuda: 'Gestão dos envios de WhatsApp: resumo diário, falhas, lote e regras.' },
  { id: 'RespostasRapidas', grupo: 'atendimento', rotulo: 'Mensagens prontas', ajuda: 'Cadastro das mensagens prontas do chat.' },
  { id: 'RoboAtendimento', grupo: 'atendimento', rotulo: 'Robô de atendimento', ajuda: 'Assuntos e comandos do robô.' },
  { id: 'PesquisaSatisfacao', grupo: 'atendimento', rotulo: 'Pesquisa de satisfação', ajuda: 'Enviar a pesquisa ao paciente pelo histórico.' },

  // ---- Transporte
  { id: 'Tratamentos', grupo: 'transporte', rotulo: 'Atendimentos', ajuda: 'Atendimentos do transporte, com os acompanhantes do paciente.' },
  { id: 'UnidadesAtendimento', grupo: 'transporte', rotulo: 'Destinos', ajuda: 'Unidades de atendimento: endereço e ponto no mapa.' },
  { id: 'TiposTratamento', grupo: 'transporte', rotulo: 'Tipos de tratamento', ajuda: 'E o tempo médio de cada um.' },
  { id: 'Translados', grupo: 'transporte', rotulo: 'Translados' },
  { id: 'Rastreamento', grupo: 'transporte', rotulo: 'Rastreamento' },
  { id: 'Veiculos', grupo: 'transporte', rotulo: 'Veículos' },
  { id: 'Motoristas', grupo: 'transporte', rotulo: 'Motoristas' },
  { id: 'Avaliacoes', grupo: 'transporte', rotulo: 'Avaliações' },
  { id: 'Faturamento', grupo: 'transporte', rotulo: 'Faturamento', ajuda: 'Faturamento do TFD/SUS.' },

  // ---- Ouvidoria (sigilo e ponto de resposta são eixos à parte do trabalho da ouvidoria)
  { id: 'Ouvidoria', grupo: 'ouvidoria', rotulo: 'Ouvidoria', ajuda: 'Fila e tratamento das manifestações.' },
  { id: 'OuvidoriaGestao', grupo: 'ouvidoria', rotulo: 'Gestão', ajuda: 'Painel, configuração, pontos de resposta, assuntos e escalonamento.' },
  { id: 'OuvidoriaSigilo', grupo: 'ouvidoria', rotulo: 'Sigilo', ajuda: 'Denúncias e identidade dos manifestantes sigilosos.' },
  { id: 'OuvidoriaPontoResposta', grupo: 'ouvidoria', rotulo: 'Ponto de resposta', ajuda: 'Responder pela unidade ou área, sem ver o manifestante.' },

  // ---- Estatísticas (um módulo por sistema: ver a equipe de um não dá a do outro)
  { id: 'Estatistica', grupo: 'estatisticas', rotulo: 'Atendimento (WhatsApp)' },
  { id: 'EstatisticaCustos', grupo: 'estatisticas', rotulo: 'Custos', ajuda: 'Custo do robô e estimativa da Meta. Exige Estatísticas.' },
  { id: 'EstatisticaSisreg', grupo: 'estatisticas', rotulo: 'Operadores do SISREG' },
  { id: 'EstatisticaSer', grupo: 'estatisticas', rotulo: 'Operadores do SER' },
  { id: 'EstatisticaSernit', grupo: 'estatisticas', rotulo: 'Operadores do SERNIT' },
  { id: 'EstatisticaEsusSg', grupo: 'estatisticas', rotulo: 'Operadores do ESUS SG' },
  { id: 'Indicadores', grupo: 'estatisticas', rotulo: 'Indicadores do HMCML', ajuda: 'Indicadores contratuais do hospital.' },

  // ---- Inteligência
  { id: 'Inteligencia', grupo: 'ia', rotulo: 'Consulta Inteligente' },
  { id: 'InteligenciaAtendimento', grupo: 'ia', rotulo: 'Base Atendimento', ajuda: 'A Consulta Inteligente lê as conversas com pacientes da rede toda.' },
  { id: 'InteligenciaConsultaDev', grupo: 'ia', rotulo: 'Modo desenvolvedor', ajuda: 'Mostra o raciocínio da Consulta Inteligente.' },
  { id: 'InteligenciaConfiguracao', grupo: 'ia', rotulo: 'Configuração da IA' },
  { id: 'InteligenciaAprendizado', grupo: 'ia', rotulo: 'Aprendizado da IA' },
  { id: 'AgenteIa', grupo: 'ia', rotulo: 'Agente IA', ajuda: 'Terminal no servidor (administrativo).' },

  // ---- Sistema
  { id: 'Ticket', grupo: 'sistema', rotulo: 'Suporte', ajuda: 'Gestão de tickets: ver e responder todos.' },
  { id: 'Auditoria', grupo: 'sistema', rotulo: 'Auditoria', ajuda: 'Trilha do sistema.' },
  { id: 'Erros', grupo: 'sistema', rotulo: 'Erros do sistema' },
  // Fontes de dados (ADR-0077) mostra conexões de produção — dar com critério.
  { id: 'FontesDados', grupo: 'sistema', rotulo: 'Fontes de dados', ajuda: 'Bancos, agentes e logins dos sistemas de onde a plataforma lê dado. Mostra conexões de produção.' },
  { id: 'IntegracoesConfig', grupo: 'sistema', rotulo: 'Integrações', ajuda: 'Credenciais das integrações.' },
  { id: 'ApiTokens', grupo: 'sistema', rotulo: 'Tokens de API' },
  { id: 'SincronizacaoPep', grupo: 'sistema', rotulo: 'Sincronização PEP', ajuda: 'Importação do Salux.' },
  // Extensão do Chrome (ADR-0064): só a administração pede módulo; baixar e autorizar o próprio PC, não.
  { id: 'ExtensaoNavegador', grupo: 'sistema', rotulo: 'Extensão Chrome', ajuda: 'Computadores autorizados e versões publicadas.' },
  { id: 'Sandbox', grupo: 'sistema', rotulo: 'Sandbox', ajuda: 'Testes (QA).' },
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
  // Nada aqui escreve no SISREG: "Inclusão" cria a estratégia no nosso banco e gasta uma chamada
  // de IA por rodada do agente.
  EstrategiasFila: {
    Consulta: 'Ver procedimentos com fila, o cenário e simular sem salvar',
    Inclusao: 'Salvar estratégia e pedir ao agente (gasta chamada de IA)',
    Edicao: 'Editar, rerodar e marcar como aplicada',
    Exclusao: 'Arquivar e excluir estratégias',
  },
  Confirmacoes: {
    Consulta: 'Ver as filas (não confirmados, confirmados, contato errado, pendentes, cancelamento, telefone comprometido)',
    Edicao: 'Atender, assumir, transferir, confirmar, enviar para pendente e marcar contato errado',
    Exclusao: 'Cancelar agendamento — cancela no SISREG primeiro, com o login da própria atendente, e só então aqui',
  },
  // As filas valem junto com a Gestão de fila: dizem quais pedidos o regulador vê e em quais age.
  RegulacaoFilaNar: { Consulta: 'Ver e regular os pedidos NAR' },
  RegulacaoFilaInterna: { Consulta: 'Ver e regular os pedidos internos (SISREG)' },
  RegulacaoFilaIntermunicipal: { Consulta: 'Ver e regular os pedidos intermunicipais' },
  ConfirmacoesEquipe: {
    Consulta: 'Ver a aba Equipe: o que cada atendente fez, tempos e ritmo',
  },
  // Quem só cadastra tratamento NÃO precisa deste módulo: o seletor de destino vem da permissão de
  // Tratamentos. Aqui é quem mantém o cadastro dos destinos.
  UnidadesAtendimento: {
    Consulta: 'Ver as unidades de atendimento, o mapa e os tratamentos de cada uma',
    Inclusao: 'Cadastrar unidade de atendimento',
    Edicao: 'Editar endereço e ponto no mapa; reativar',
    Exclusao: 'Desativar (recusado enquanto houver tratamento ativo indo para lá)',
  },
  NotificacoesAgendamento: {
    Consulta: 'Ver resumo diário, envios, respostas e regras da mensageria',
    Edicao: 'Reenviar, disparar lote, alterar regras e tarifas',
  },
  EstatisticaCustos: {
    Consulta: 'Ver tokens e custo do robô e a estimativa de custo Meta',
  },
  // Só a Consulta vale nos três: a tela é leitura. Quem entra nas estatísticas se escolhe na
  // configuração de cada sistema (Configuração SISREG / Regulação — Configuração).
  EstatisticaSisreg: {
    Consulta: 'Ver as estatísticas dos operadores do SISREG',
  },
  EstatisticaSer: {
    Consulta: 'Ver as estatísticas dos operadores do SER',
  },
  EstatisticaSernit: {
    Consulta: 'Ver as estatísticas dos operadores do SERNIT',
  },
  EstatisticaEsusSg: {
    Consulta: 'Ver as estatísticas dos operadores do ESUS São Gonçalo',
  },
  // Só a Consulta vale: a tela é leitura, e o PDF é a mesma leitura em outro formato.
  IndicadoresRegulacao: {
    Consulta: 'Ver os indicadores e exportar o PDF',
  },
  RegulacaoSer: {
    Consulta: 'Ver a fila do Estado',
    Edicao: 'Registrar FollowUP no SER',
  },
  RegulacaoSernit: {
    Consulta: 'Ver a fila de Niterói',
    Edicao: 'Registrar FollowUP e telefones no SERNIT',
  },
  // Só a Consulta vale: a integração com o ESUS é só leitura. "Visto" nas notificações é marca
  // nossa e também usa a Consulta.
  RegulacaoEsusSg: {
    Consulta: 'Ver fila, agendados, detalhe e notificações do ESUS São Gonçalo (e marcar como visto)',
    Inclusao: '— (sem efeito: integração só leitura)',
    Edicao: '— (sem efeito: reservado para quando a escrita no ESUS for autorizada)',
    Exclusao: '— (sem efeito)',
  },
  Instituicao: {
    Consulta: 'Ver a identidade da instituição',
    Edicao: 'Alterar nome, marca, domínios e contatos legais (afeta telas públicas)',
  },
  // Extensão do Chrome (ADR-0064). "Edição" é o que faz uma versão chegar a todos os computadores.
  ExtensaoNavegador: {
    Consulta: 'Ver os computadores autorizados e as versões publicadas',
    Inclusao: 'Publicar versão (entra no canal de teste)',
    Edicao: 'Pôr versão em produção, trocar o canal de um computador e ligar/desligar a publicação por API',
    Exclusao: 'Retirar versão e revogar computador',
  },
  // Fontes de dados (ADR-0077). A senha nunca é exibida; "Edição" troca a que os robôs usam.
  FontesDados: {
    Consulta: 'Ver as fontes, a situação e quem usa cada uma',
    Inclusao: 'Cadastrar base de dados',
    Edicao: 'Editar conexão, trocar login/senha, testar e gerar token do agente',
    Exclusao: 'Remover base ou limpar o login de um sistema',
  },
  // Ouvidoria (ADR-0060, plano §1.5). "Exclusão" aqui é ARQUIVAR — não apaga nada (trilha append-only).
  Ouvidoria: {
    Consulta: 'Ver fila e detalhe (manifestante mascarado se sigilosa; denúncias só com Sigilo)',
    Inclusao: 'Registrar manifestação',
    Edicao: 'Triar, encaminhar, pedir complementação, validar, responder ao cidadão, prorrogar, cobrar, recurso, concluir',
    Exclusao: 'Arquivar manifestação (com motivo)',
  },
  OuvidoriaGestao: {
    Consulta: 'Ver painel/indicadores e configuração',
    Inclusao: 'Criar pontos de resposta, assuntos e marcadores',
    Edicao: 'Editar pontos, assuntos, marcadores e configuração; escalonar',
    Exclusao: 'Desativar pontos, assuntos e marcadores',
  },
  // Só Consulta/Inclusão/Edição valem: não há "exclusão" no eixo do sigilo.
  OuvidoriaSigilo: {
    Consulta: 'Ver denúncias e revelar identidade em sigilosas (cada revelação exige justificativa e é logada)',
    Inclusao: 'Habilitar denúncia (análise de admissibilidade)',
    Edicao: 'Editar o teor pseudonimizado que vai à apuração',
    Exclusao: '— (sem efeito)',
  },
  // Só Consulta e Edição valem.
  OuvidoriaPontoResposta: {
    Consulta: 'Ver manifestações encaminhadas aos pontos de que é membro, SEM dados do manifestante',
    Inclusao: '— (sem efeito)',
    Edicao: 'Responder pela área (com anexos)',
    Exclusao: '— (sem efeito)',
  },
};
