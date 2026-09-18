/**
 * Espelho dos DTOs de `SMSMais.Core/EstrategiasFila/Dtos` (camelCase pela serialização MVC).
 *
 * Nada aqui escreve no SISREG: a estratégia é planejamento guardado no nosso banco.
 */

export type ProcedimentoComFila = {
  codigo: string | null;
  nome: string;
  nomeCanonico: string | null;
  ehGrupo: boolean;
  naFila: number;
  esperaMedianaDias: number | null;
  vagasRegulacaoSemana: number;
  unidades: number;
  profissionais: number;
  temEstrategia: boolean;
};

export type ParametroNumero = {
  valor: number;
  travado: boolean;
  min?: number | null;
  max?: number | null;
};

export type Mutirao = { semana: number; vagas: number; descricao?: string | null };

export type Objetivo = 'zerar_em_semanas' | 'equilibrio' | 'minimo_recursos';

export type ParametrosEstrategia = {
  objetivo: Objetivo;
  prazoAlvoSemanas: number | null;
  unidades: ParametroNumero;
  profissionais: ParametroNumero;
  turnosPorProfissionalSemana: ParametroNumero;
  atendimentosPorTurno: ParametroNumero;
  aproveitamento: ParametroNumero;
  entradaSemanal: ParametroNumero;
  mutiroes: Mutirao[];
  mutiroesTravados: boolean;
  horizonteSemanas: number;
};

export type ChaveNumerica =
  | 'unidades'
  | 'profissionais'
  | 'turnosPorProfissionalSemana'
  | 'atendimentosPorTurno'
  | 'aproveitamento'
  | 'entradaSemanal';

export type FaixaEspera = { ordem: number; rotulo: string; volume: number };

export type FilaCenario = {
  total: number;
  porRisco: Record<string, number>;
  esperaMedianaDias: number | null;
  esperaP90Dias: number | null;
  esperaMaxDias: number | null;
  maisAntigoEm: string | null;
  faixas: FaixaEspera[];
};

export type Semana = { semana: string; quantidade: number };

export type Ritmo = {
  mediaSemanal12: number;
  mediaSemanal26: number;
  tendencia: number | null;
  serie: Semana[];
};

export type UnidadeOferta = {
  unidadeId: string;
  nome: string;
  cnes: string | null;
  agendaLocal: boolean;
  profissionais: number;
  vagasRegulacaoSemana: number;
  vagasTotalSemana: number;
};

export type ProfissionalOferta = {
  nome: string;
  cbo: string | null;
  unidade: string;
  dias: number[];
  vagasRegulacaoSemana: number;
};

export type OfertaCenario = {
  unidades: UnidadeOferta[];
  profissionais: ProfissionalOferta[];
  diasSemana: number[];
  horaInicioTipica: string | null;
  horaFimTipica: string | null;
  blocos: number;
  vagasPrimeiraVezSemana: number;
  vagasRetornoSemana: number;
  vagasReservaSemana: number;
  vagasRegulacaoSemana: number;
  vagasTotalSemana: number;
  turnosSemana: number;
  turnosPorProfissionalSemana: number;
  atendimentosPorTurno: number;
  horasDeclaradasSemana: number;
  vagasAgendaLocalSemana: number;
};

export type OcupacaoCenario = {
  semanasMedidas: number;
  vagasRegulacaoOfertadas: number;
  agendados: number;
  aproveitamento: number | null;
};

export type ProcedimentoCenario = {
  codigo: string | null;
  nome: string;
  nomeCanonico: string | null;
  regulacaoProcedimentoId: string | null;
  ehGrupo: boolean;
  familia: string[];
};

export type Cobertura = {
  primeiroDiaAgendado: string | null;
  ultimoDiaAgendado: string | null;
  ultimoDiaDeEscala: string | null;
  agendamentos: number;
};

export type CenarioFila = {
  procedimento: ProcedimentoCenario;
  fila: FilaCenario;
  entrada: Ritmo;
  vazao: Ritmo;
  saidaSemAgendarFracao: number | null;
  oferta: OfertaCenario;
  ocupacao: OcupacaoCenario;
  cobertura: Cobertura;
  parametrosIniciais: ParametrosEstrategia;
  geradoEm: string;
};

export type PontoProjecao = {
  semana: number;
  fila: number;
  capacidade: number;
  atendidos: number;
  entrada: number;
};

export type Projecao = {
  filaInicial: number;
  capacidadeSemanal: number;
  vagasSemanais: number;
  entradaSemanal: number;
  zera: boolean;
  semanaZera: number | null;
  filaFinal: number;
  crescimentoSemanal: number;
  capacidadeEquilibrio: number;
  capacidadeParaZerarNoPrazo: number | null;
  atendidosAteZerar: number;
  picoFila: number;
  horizonteSemanas: number;
  serie: PontoProjecao[];
};

export type AcaoProposta = {
  tipo: string;
  descricao: string;
  unidade: string | null;
  impactoVagasSemana: number | null;
};

export type PropostaAgente = {
  resumo: string;
  acoes: AcaoProposta[];
  riscos: string[];
  confianca: number;
  simulacoes: number;
};

export type StatusEstrategia = 'Rascunho' | 'Pronta' | 'Aplicada' | 'Arquivada';
export type ModoRodada = 'Manual' | 'Agente';

export type RodadaResumo = {
  id: string;
  numero: number;
  modo: ModoRodada;
  zera: boolean | null;
  semanaZera: number | null;
  capacidadeSemanal: number;
  falha: string | null;
  modelo: string | null;
  custoUsd: number | null;
  duracaoMs: number;
  criadoEm: string;
  criadoPor: string | null;
};

export type Rodada = {
  id: string;
  estrategiaId: string;
  numero: number;
  modo: ModoRodada;
  parametrosEntrada: ParametrosEstrategia;
  cenario: CenarioFila;
  parametrosResultado: ParametrosEstrategia;
  projecao: Projecao;
  proposta: PropostaAgente | null;
  modelo: string | null;
  tokensEntrada: number;
  tokensSaida: number;
  custoUsd: number | null;
  duracaoMs: number;
  falha: string | null;
  criadoEm: string;
  criadoPor: string | null;
};

export type EstrategiaResumo = {
  id: string;
  nome: string;
  procedimentoCodigo: string | null;
  procedimentoNome: string;
  nomeCanonico: string | null;
  status: StatusEstrategia;
  rodadas: number;
  rodadaAtual: RodadaResumo | null;
  aplicadaEm: string | null;
  criadoEm: string;
  atualizadoEm: string | null;
};

export type Estrategia = {
  id: string;
  nome: string;
  procedimentoCodigo: string | null;
  procedimentoNome: string;
  nomeCanonico: string | null;
  status: StatusEstrategia;
  parametros: ParametrosEstrategia;
  rodadaAtual: Rodada | null;
  rodadas: RodadaResumo[];
  aplicadaEm: string | null;
  aplicadaPor: string | null;
  aplicacaoNota: string | null;
  criadoEm: string;
  criadoPor: string | null;
  atualizadoEm: string | null;
};

export type SimularResposta = { cenario: CenarioFila; projecao: Projecao };
