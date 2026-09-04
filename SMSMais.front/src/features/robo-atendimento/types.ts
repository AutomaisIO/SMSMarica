export type TipoCondicaoRobo = 'PalavraChave' | 'Regex' | 'Frase';

export type TipoTreinoRobo = 'Instrucao' | 'Exemplo' | 'Glossario' | 'Do' | 'Dont';

/** Comandos habilitáveis por assunto (ResponderCidadao é implícito, não entra na tela). */
export type ComandoRobo =
  | 'ConsultarStatusAgendamento'
  | 'ConfirmarPresenca'
  | 'IniciarCancelamento'
  | 'RegistrarNumeroErrado'
  | 'EncaminharParaHumano'
  | 'InformarHorarioAtendimento'
  | 'ConsultarStatusExameRecente'
  | 'ConsultarPosicaoRegulacao';

export type RoboAssuntoCondicao = {
  tipo: TipoCondicaoRobo;
  valor: string;
  ativo: boolean;
  ordem: number;
};

export type RoboAssuntoTreino = {
  tipo: TipoTreinoRobo;
  titulo: string | null;
  conteudo: string;
  ordem: number;
  ativo: boolean;
};

export type RoboAssuntoListItem = {
  id: string;
  nome: string;
  descricao: string | null;
  ativo: boolean;
  modelo: string | null;
  ordem: number;
  /** Assunto usado quando nenhum outro casa com a mensagem (no máximo um). */
  padrao: boolean;
  qtdComandos: number;
};

export type RoboAssunto = {
  id: string;
  nome: string;
  descricao: string | null;
  instrucoesPersona: string;
  modelo: string | null;
  ativo: boolean;
  /** "HH:mm:ss" ou nulo. */
  horarioInicio: string | null;
  horarioFim: string | null;
  /** Bitmask (bit 0 = domingo … bit 6 = sábado); nulo = todos os dias. */
  diasSemana: number | null;
  maxInteracoesSemResolver: number;
  limiarConfianca: number;
  escalonamentoUnidadeId: string | null;
  escalonamentoUnidadeNome: string | null;
  ordem: number;
  padrao: boolean;
  condicoes: RoboAssuntoCondicao[];
  treinos: RoboAssuntoTreino[];
  comandos: ComandoRobo[];
  criadoEm: string;
};

export type SalvarRoboAssuntoPayload = {
  nome: string;
  descricao: string | null;
  instrucoesPersona: string;
  modelo: string | null;
  ativo: boolean;
  horarioInicio: string | null;
  horarioFim: string | null;
  diasSemana: number | null;
  maxInteracoesSemResolver: number;
  limiarConfianca: number;
  escalonamentoUnidadeId: string | null;
  ordem: number;
  padrao: boolean;
  condicoes: RoboAssuntoCondicao[];
  treinos: RoboAssuntoTreino[];
  comandos: ComandoRobo[];
};

export type ComandoRoboCatalogo = {
  comando: ComandoRobo;
  rotulo: string;
  descricao: string;
  escrita: boolean;
};

export type StatusRoboErro = 'Aberto' | 'Revisado' | 'Descartado';

export type RoboErro = {
  id: string;
  conversaId: string;
  mensagemWhatsAppId: string | null;
  assunto: string | null;
  trecho: string | null;
  nota: string | null;
  status: StatusRoboErro;
  criadoEm: string;
  criadoPorNome: string | null;
  revisadoEm: string | null;
  revisaoNota: string | null;
};

export type RoboConfiguracao = {
  ativo: boolean;
  personaGlobal: string;
  modeloPadrao: string;
  nomeExibicao: string;
  mensagemHandOff: string | null;
  mensagemForaHorario: string | null;
  /** Início do expediente dos atendentes humanos (HH:mm[:ss], Brasília). Antes disso o robô assume. */
  horaAtendimentoHumanoInicio: string | null;
  /** Fim do expediente dos atendentes humanos (HH:mm[:ss], Brasília). A partir disso o robô assume. */
  horaAtendimentoHumanoFim: string | null;
  /** Dias da semana COM atendente humano (bitmask, bit 0 = domingo). Nulo = todos os dias. */
  diasSemanaAtendimentoHumano: number | null;
  /** Motor de IA que responde: assinatura (legado) ou Messages API. Trocável sem deploy. */
  motor: MotorRobo;
};

export type MotorRobo = 'Assinatura' | 'Api';

/** Um comando que o robô chamaria no ensaio. `simulado` = escrita, não executada de verdade. */
export type RoboSimulacaoChamada = {
  comando: string;
  entradaJson: string | null;
  resultado: string;
  sucesso: boolean;
  simulado: boolean;
};

export type RoboSimulacao = {
  assunto: string | null;
  modelo: string;
  texto: string;
  handOff: boolean;
  motivoHandOff: string | null;
  confianca: number | null;
  chamadas: RoboSimulacaoChamada[];
  tokensEntrada: number | null;
  tokensSaida: number | null;
  custoUsd: number | null;
  duracaoMs: number;
  dentroDoHorario: boolean;
};

export type SimularRoboPayload = {
  mensagem: string;
  assuntoId?: string | null;
  historico?: { papel: string; texto: string }[];
};

// ---- Treinamento (crítica do atendente → análise adversarial → correção) ----

export type StatusTreinamento =
  | 'Aberto'
  | 'Analisando'
  | 'AguardandoHumano'
  | 'SimulacaoPendente'
  | 'Concluido'
  | 'Descartado'
  | 'Falhou';

export type VereditoSimulacaoTreinamento = 'Passou' | 'Falhou' | 'Duvidoso';

export interface TreinamentoContextoTurno {
  papel: string;
  texto: string;
  em: string | null;
}

export interface TreinamentoPendencia {
  id: string;
  tipo: 'RegraNegocio' | 'AlteracaoCodigo';
  pergunta: string;
  contexto: string | null;
  opcoes: string[];
  status: 'Aberta' | 'Respondida' | 'Dispensada';
  resposta: string | null;
  autorizado: boolean | null;
  criadoEm: string;
  respondidoEm: string | null;
  respondidoPorNome: string | null;
}

export interface TreinamentoAlteracao {
  id: string;
  alvo: 'TreinoAssunto' | 'CondicaoAssunto';
  operacao: 'Criar' | 'Atualizar' | 'Desativar';
  roboAssuntoId: string;
  assuntoNome: string | null;
  alvoId: string;
  antes: string | null;
  depois: string | null;
  justificativa: string | null;
  aplicadoEm: string;
  desfeitoEm: string | null;
  desfeitoPorNome: string | null;
}

export interface TreinamentoSimulacao {
  id: string;
  mensagem: string;
  assuntoNome: string | null;
  resposta: string | null;
  chamadas: RoboSimulacaoChamada[];
  veredito: VereditoSimulacaoTreinamento | null;
  analise: string | null;
  custoUsd: number | null;
  duracaoMs: number;
  automatica: boolean;
  erroMensagem: string | null;
  criadoEm: string;
  criadoPorNome: string | null;
}

export interface TreinamentoItemResumo {
  id: string;
  conversaId: string | null;
  mensagemWhatsAppId: string | null;
  assuntoNome: string | null;
  critica: string;
  trecho: string | null;
  status: StatusTreinamento;
  pendenciasAbertas: number;
  alteracoesAplicadas: number;
  ultimoVeredito: VereditoSimulacaoTreinamento | null;
  criadoEm: string;
  criadoPorNome: string | null;
}

export interface TreinamentoItem {
  id: string;
  conversaId: string | null;
  mensagemWhatsAppId: string | null;
  roboAssuntoId: string | null;
  assuntoNome: string | null;
  critica: string;
  observacao: string | null;
  trecho: string | null;
  contexto: TreinamentoContextoTurno[];
  status: StatusTreinamento;
  analise: string | null;
  modelo: string | null;
  custoUsd: number | null;
  analisadoEm: string | null;
  erroMensagem: string | null;
  pendencias: TreinamentoPendencia[];
  alteracoes: TreinamentoAlteracao[];
  simulacoes: TreinamentoSimulacao[];
  criadoEm: string;
  criadoPorNome: string | null;
}
