/**
 * Tipos do módulo Regulação → Solicitações (ADR-0052).
 *
 * Enums viajam como STRING no JSON (ver a convenção do backend). Tipar como número faz o
 * `select` "voltar sozinho" na tela, porque o valor do option nunca casa com o do estado.
 */

export type SistemaRegulacao = 'Sisreg' | 'Ser' | 'Sernit' | 'Esus';

export type TipoProcedimentoRegulacao = 'Consulta' | 'Exame' | 'Cirurgia' | 'Outro';

export type RegulacaoOrigem = {
  id: string;
  sistema: SistemaRegulacao;
  rotulo: string;
  /** Só o SER tem ramo: `AE` (ambulatório estadual) ou `NAO_AE` (rede geral). */
  ramo: string | null;
  chaveExterna: string;
};

/** Unidade de Maricá que executa o procedimento, com a oferta que o SISREG mostra hoje. */
export type ExecutanteInterno = {
  unidadeId: string;
  nome: string;
  cnes: string | null;
  vagasTotal: number;
  /** Primeira vigência que ainda vai começar — nulo quando toda a oferta já está valendo. */
  proximaVigencia: string | null;
};

export type ExisteExterno = {
  ser: boolean;
  serAmbulatorioEstadual: boolean;
  sernit: boolean;
};

export type RegulacaoProcedimentoItem = {
  id: string;
  nome: string;
  tipo: TipoProcedimentoRegulacao;
  /** 1 = casou pelo texto; abaixo disso é proximidade semântica. */
  score: number;
  origens: RegulacaoOrigem[];
  executantesInternos: ExecutanteInterno[];
  existeExterno: ExisteExterno;
};

export type BuscaProcedimentoResultado = {
  itens: RegulacaoProcedimentoItem[];
  /** O provedor de embeddings caiu e só a busca por texto respondeu. A tela avisa. */
  degradada: boolean;
};

export type RegulacaoProcedimentoDetalhe = {
  id: string;
  nome: string;
  tipo: TipoProcedimentoRegulacao;
  codigoSigtap: string | null;
  origens: RegulacaoOrigem[];
  executantesInternos: ExecutanteInterno[];
  existeExterno: ExisteExterno;
};

export type SugestaoPareamento = {
  origemId: string;
  sistema: SistemaRegulacao;
  rotulo: string;
  canonicoAtualId: string;
  canonicoAtual: string;
  sugeridoId: string;
  sugeridoNome: string;
  score: number;
};

export type CatalogoSyncResultado = {
  origensNovas: number;
  origensDesativadas: number;
  canonicosNovos: number;
  embeddingsGerados: number;
  semEmbedding: number;
  sugestoes: number;
};

export type NaoSeiViraRegulacao = 'Ressalva' | 'Pendencia';

/** Uma regra de classificação de follow-up, como fica gravada em `regrasFollowup`. */
export type RegraFollowUp = {
  categoria: string;
  ordem: number;
  /** Regex escrita sobre o texto normalizado: sem acento, maiúsculo, espaços colapsados. */
  padrao: string;
  /** `contato`, `documento` ou nulo. Só duas das nove categorias abrem pendência. */
  vira_pendencia: string | null;
};

/** O que a caixa "testar texto" devolve — inclusive qual regra decidiu. */
export type TesteFollowUp = {
  categoria: string;
  viraPendencia: string | null;
  ordemDaRegra: number | null;
  textoNormalizado: string;
};

/** Configuração completa — só para quem tem o módulo de configuração (51). */
export type ConfiguracaoRegulacao = {
  permitirExternoComInterno: boolean;
  pontaPodeEscolherUnidade: boolean;
  pontaPodeVerTodasUnidades: boolean;
  exigirCpf: boolean;
  rotuloFila: string;
  sisregPrazoEdicaoDias: number;
  buscaCorteDistancia: number;
  buscaScoreSugestaoPareamento: number;
  regrasFollowup: unknown;
  anexoLimiteMb: number;
  anexoTiposPermitidos: string[];
  naoSeiPadrao: NaoSeiViraRegulacao;
  atualizadoEm: string | null;
  atualizadoPorNome: string | null;
  /** Trava de concorrência: volta no PUT e o servidor recusa com 409 se alguém salvou antes. */
  rowVersion: number;
};

/** O subconjunto que o wizard precisa — sem os parâmetros de operação. */
export type ConfiguracaoFluxo = {
  permitirExternoComInterno: boolean;
  pontaPodeVerTodasUnidades: boolean;
  exigirCpf: boolean;
  rotuloFila: string;
  anexoLimiteMb: number;
  anexoTiposPermitidos: string[];
};

// ---------------------------------------------------------------- paciente no wizard

export type PacienteResumoRegulacao = {
  id: string;
  nome: string;
  cpf: string | null;
  cns: string | null;
  nascimento: string | null;
  sexo: string | null;
  /** Sem CPF: salva rascunho, mas não sai da fila (ADR-0041 + configuração `exigirCpf`). */
  cpfPendente: boolean;
};

export type PacienteCadsus = {
  cpf: string | null;
  cns: string | null;
  nome: string;
  nascimento: string | null;
  sexo: string | null;
  nomeMae: string | null;
  /** Porta por onde veio: `Sisreg` ou `Ser`. */
  fonte: string;
};
