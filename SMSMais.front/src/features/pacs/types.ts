import type { ModalidadeDicom } from '@/features/tipos-exame/types';

/** Um elemento de tag no formato DICOM-JSON (QIDO-RS / WADO-RS metadata). */
export type ElementoDicom = {
  vr: string;
  Value?: unknown[];
};

/** Um dataset DICOM-JSON: mapa de tag (ex.: "00100010") para o elemento. */
export type DatasetDicom = Record<string, ElementoDicom>;

export type Estudo = {
  studyInstanceUID: string;
  accessionNumber: string;
  patientName: string;
  patientId: string;
  patientSex: string;
  patientAge: string;
  studyDate: string;
  studyDateFormatado: string;
  studyTime: string;
  modalidade: string;
  studyDescription: string;
  numeroSeries: string;
  numeroInstancias: string;
};

export type Serie = {
  seriesInstanceUID: string;
  seriesNumber: string;
  seriesDescription: string;
  modalidade: string;
  numeroInstancias: number;
};

/**
 * Vínculo de um estudo do PACS a uma solicitação/paciente. `explicita` = associação
 * manual/automática persistida (pode desassociar); senão é o casamento implícito do
 * exame de worklist. `origem` só vem nas explícitas.
 */
export type AssociacaoExame = {
  studyInstanceUID: string;
  solicitacaoExameId: string;
  accessionNumber: string;
  pacienteId: string;
  pacienteNome: string | null;
  explicita: boolean;
  origem: 'Manual' | 'Automatica' | null;
  prioridade: 'Eletiva' | 'Prioritaria' | 'Urgente';
  temAnamnese: boolean;
  /**
   * Nome do procedimento como o SISREG o informa ("ULTRASONOGRAFIA TRANSVAGINAL"). É o que a
   * coluna Descrição mostra: a tag DICOM StudyDescription é escrita pelo EQUIPAMENTO e vem
   * genérica ("ULTRASSONOGRAFIA", "Mamografia"), sem saber qual procedimento foi pedido.
   */
  tipoExameNome: string | null;
  modalidade: ModalidadeDicom | null;
  unidadeExecutanteNome: string | null;
  unidadeSolicitanteNome: string | null;
  /** Nº da solicitação no SISREG, quando veio de lá. */
  codigoSolicitacao: string | null;
};

/**
 * De onde as imagens vieram, pelo AE Title de origem (tag privada dcm4chee 7777,1037). Só é
 * consultado para o estudo ÓRFÃO — o associado já traz a unidade do próprio pedido.
 */
export type OrigemEstudo = {
  studyInstanceUID: string;
  aeTitle: string;
  /** Null quando o AE não é de nenhum equipamento cadastrado (ex.: acervo legado importado). */
  equipamentoNome: string | null;
  unidadeNome: string | null;
};

export type TipoBuscaNome = 'inicio' | 'qualquer';

export type FiltroBusca = {
  nome: string;
  tipoBuscaNome: TipoBuscaNome;
  dataInicial: string;
  dataFinal: string;
  limite: number;
  /**
   * Modalidades DICOM marcadas. Vira `ModalitiesInStudy` no QIDO — o dcm4chee faz a UNIÃO dos
   * valores separados por vírgula, então a paginação continua correta (ao contrário de filtrar
   * a página depois de recebida). Vazio/ausente = todas.
   */
  modalidades?: ModalidadeDicom[];
  /**
   * Tipos de exame marcados (ids). Diferente da modalidade, este filtro NÃO existe no DICOM —
   * quem sabe o procedimento é o pedido, do nosso lado. Por isso a lista passou a ser paginada
   * pelo servidor. Com tipo marcado o ÓRFÃO sai por definição (não tem pedido, não tem tipo) —
   * daí `orfaosOcultos` na resposta.
   */
  tipoExameIds?: string[];
  /** Offset para paginação (default 0). */
  offset?: number;
};

/** Página de estudos já recortada pelo servidor (unidade + tipo). */
export type PaginaEstudos = {
  estudos: Estudo[];
  /** Órfãos descartados pelo filtro de tipo nesta varredura — a tela avisa. */
  orfaosOcultos: number;
  /** A varredura bateu no teto antes de completar a página. */
  truncado: boolean;
};

/**
 * Snapshot opaco do estado de annotations do Cornerstone (output de
 * `annotationManager.state.getAllAnnotations()`). Tratamos como JSON arbitrário
 * — quem entende o formato é o próprio Cornerstone.
 */
export type PayloadAnotacoes = unknown;

export type EstudoAnotacaoVersao = {
  id: string;
  studyInstanceUID: string;
  versao: number;
  payload: PayloadAnotacoes;
  usuarioId: string;
  usuarioNome: string;
  criadoEm: string;
  comentario: string | null;
};

export type EstudoAnotacaoVersaoResumo = {
  id: string;
  versao: number;
  usuarioId: string;
  usuarioNome: string;
  criadoEm: string;
  comentario: string | null;
};
