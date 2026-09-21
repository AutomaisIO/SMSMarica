export type StatusNotificacao =
  | 'Pendente'
  | 'Enviada'
  | 'Entregue'
  | 'Lida'
  | 'Falha'
  | 'SemTelefoneValido'
  /** Retida: resultado/laudo só vai para contato verificado. Sai sozinha quando verificarem. */
  | 'AguardandoTelefoneVerificado'
  /** Confirmação para número não verificado: pediu-se o início do CPF antes de mandar os dados. */
  | 'AguardandoVerificacaoCadastral'
  /** Número marcado como inválido (quem atende disse que não conhece o paciente). */
  | 'AguardandoCorrecaoContato'
  /** Terminal: uma atendente entrou no circuito (menu Confirmações) antes de a mensagem sair. */
  | 'SubstituidaPorAtendente';

export type StatusConfirmacao = 'Pendente' | 'Confirmada' | 'Cancelada';

export type FinalidadeComunicacao = 'ConfirmacaoAgendamento' | 'ExameLiberado' | 'LaudoPronto';

export type NotificacaoFiltro = {
  status?: string;
  finalidade?: string;
  confirmacao?: string;
  texto?: string;
  de?: string;
  ate?: string;
  pagina?: number;
  tamanho?: number;
};

export type NotificacaoResumo = {
  id: string;
  finalidade: FinalidadeComunicacao;
  solicitacaoExameId: string | null;
  accessionNumber: string | null;
  codigoSolicitacao: string | null;
  pacienteId: string;
  pacienteNome: string | null;
  tipoExameNome: string | null;
  unidadeNome: string | null;
  dataAgendada: string | null;
  telefone: string | null;
  status: StatusNotificacao;
  motivoFalha: string | null;
  tentativas: number;
  enviadoEm: string | null;
  entregueEm: string | null;
  lidoEm: string | null;
  visualizadoEm: string | null;
  statusConfirmacao: StatusConfirmacao;
  confirmadoEm: string | null;
  confirmadoCanal: string | null;
  motivoCancelamentoPaciente: string | null;
  criadoEm: string;
};

export type PaginaNotificacoes = {
  itens: NotificacaoResumo[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type NotificacaoDetalhe = {
  resumo: NotificacaoResumo;
  ultimaTentativaEm: string | null;
  proximaTentativaEm: string | null;
  mensagemConteudo: string | null;
  mensagemStatus: string | null;
  mensagemErroMeta: string | null;
  linkExpiraEm: string | null;
  linkUsadoEm: string | null;
  linkUsadoIp: string | null;
};

// ------------------------------------------------------------------ Resumo diário

export type DiaMensageria = {
  dia: string;
  enfileiradas: number;
  enviadasNoDia: number;
  enviadas: number;
  entregues: number;
  lidas: number;
  visualizadas: number;
  falhas: number;
  naFila: number;
  aguardandoIdentificacao: number;
  numeroNegado: number;
  semTelefone: number;
  aguardandoVerificado: number;
  substituidasPorAtendente: number;
  confirmadas: number;
  canceladas: number;
  semResposta: number;
};

export type ContagemRotulo = { erro: string; total: number };

export type TotaisMensageria = {
  enfileiradas: number;
  enviadas: number;
  entregues: number;
  lidas: number;
  visualizadas: number;
  falhas: number;
  retidas: number;
  substituidasPorAtendente: number;
  confirmadas: number;
  canceladas: number;
  semResposta: number;
  taxaEntrega: number;
  taxaLeitura: number;
  taxaResposta: number;
};

export type ResumoDiarioMensageria = {
  de: string;
  ate: string;
  totais: TotaisMensageria;
  dias: DiaMensageria[];
  falhasPorErro: ContagemRotulo[];
  porFinalidade: ContagemRotulo[];
  porUnidade: ContagemRotulo[];
};

// ------------------------------------------------------------------ Regras (confirmação)

export type ConfirmacaoConfiguracao = {
  /** "HH:mm" — Brasília. */
  horaInicioEnvio: string;
  horaFimEnvio: string;
  maximoPorPassagem: number;
  somenteSisreg: boolean;
  janelaAbertaAgora: boolean;
  atualizadoEm: string | null;
  /** Dias antes do agendamento em que o lembrete sai — configuração GLOBAL. */
  lembreteDiasAntes: number;
  lembreteHabilitado: boolean;
  /** Motor que traz os cancelamentos feitos no SISREG por outra pessoa (executante, solicitante, regulação). */
  conciliacaoCancelamentoHabilitada: boolean;
  /** Aviso ao paciente quando o agendamento é cancelado. O motivo NUNCA vai na mensagem. */
  avisoCancelamentoHabilitado: boolean;
};

export type SalvarConfirmacaoConfiguracao = Pick<
  ConfirmacaoConfiguracao,
  | 'horaInicioEnvio'
  | 'horaFimEnvio'
  | 'maximoPorPassagem'
  | 'somenteSisreg'
  | 'lembreteDiasAntes'
  | 'lembreteHabilitado'
  | 'conciliacaoCancelamentoHabilitada'
  | 'avisoCancelamentoHabilitado'
>;

/** Modelo aprovado na Meta, como a tela de teste precisa dele. */
export type ModeloWhatsApp = {
  nome: string;
  idioma: string;
  categoria: string;
  corpo: string | null;
  parametros: number;
  exemplos: string[];
  /** Nomes das variáveis na ordem: ["1","2"] (numerado) ou ["nome","data"] (nomeado). */
  variaveis: string[];
  nomeadas: boolean;
  /** Valores que o sistema usaria de verdade neste modelo. */
  sugestao: string[];
  /** Formato do cabeçalho aprovado: IMAGE, VIDEO, DOCUMENT, TEXT — null se não tem cabeçalho. */
  cabecalhoFormato: string | null;
  /** Tem mídia no topo: a arte vai em TODO envio (a do modelo aprovado é só exemplo). */
  cabecalhoExigeArte: boolean;
  cabecalhoTexto: string | null;
  /** A arte em vigor — a mesma que o envio usa. Gerida no Automais.Zap. */
  arteUrl: string | null;
  /** Veio do Automais.Zap (e não do padrão de configuração desta instância). */
  arteNaPlataforma: boolean;
  /** Exige arte e não tem: o envio é recusado antes de chegar na Meta. */
  artePendente: boolean;
};

export type ResultadoTesteModelo = {
  ok: boolean;
  erro: string | null;
  waMessageId: string | null;
};

export type RegraUnidade = {
  unidadeId: string;
  unidadeNome: string;
  enviarConfirmacao: boolean;
  procedimentosComAviso: number;
  procedimentosTotal: number;
};

/** Tarifas Meta (USD) e mapa template → categoria para a estimativa de custo. */
export type MensageriaConfiguracao = {
  tarifaUtilityUsd: number | null;
  tarifaMarketingUsd: number | null;
  tarifaAuthenticationUsd: number | null;
  templatesCategorias: Record<string, string>;
  atualizadoEm: string | null;
};

export type SalvarMensageriaConfiguracao = Omit<MensageriaConfiguracao, 'atualizadoEm'>;

// ------------------------------------------------------------------ Respostas

export type RespostaConfirmacao = {
  solicitacaoId: string;
  exameId: string | null;
  codigoSolicitacao: string | null;
  pacienteId: string;
  pacienteNome: string | null;
  categoria: string;
  procedimento: string | null;
  unidadeExecutante: string | null;
  dataAgendada: string | null;
  statusConfirmacao: StatusConfirmacao;
  canal: string | null;
  respondidoEm: string | null;
  motivo: string | null;
  statusSolicitacao: string;
};

export type FiltroRespostas = {
  resposta?: string;
  de?: string;
  ate?: string;
  texto?: string;
  pagina?: number;
  tamanho?: number;
};

export type PaginaRespostas = {
  itens: RespostaConfirmacao[];
  total: number;
  pagina: number;
  tamanho: number;
};

// ------------------------------------------------------------------ Lote

export type FiltroLote = {
  unidadeId?: string;
  de?: string;
  ate?: string;
  forcar?: boolean;
  incluirJaAvisados?: boolean;
  incluirJaConfirmados?: boolean;
};

export type DisparoLote = FiltroLote & { ignorarJanela?: boolean };

export type PreviaLote = {
  elegiveis: number;
  candidatos: number;
  foraProcedimentoDesligado: number;
  foraJaAvisado: number;
  foraNaoSisreg: number;
  porDia: { dia: string; total: number; consultas: number; exames: number }[];
  /** Só no disparo: quantos foram enfileirados. */
  enfileiradas: number | null;
  aviso: string | null;
  reenviosAvisados: number;
  reenviosConfirmados: number;
};
