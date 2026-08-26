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

export type RoboConfiguracao = {
  ativo: boolean;
  personaGlobal: string;
  modeloPadrao: string;
  nomeExibicao: string;
  mensagemHandOff: string | null;
  mensagemForaHorario: string | null;
};
