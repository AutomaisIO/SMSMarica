export type ModalidadeDicom = 'CR' | 'DX' | 'MG' | 'US' | 'CT' | 'MR' | 'NM' | 'PT' | 'ECG' | 'OT';

export const MODALIDADES: { id: ModalidadeDicom; rotulo: string }[] = [
  { id: 'US', rotulo: 'US — Ultrassom' },
  { id: 'MG', rotulo: 'MG — Mamografia' },
  { id: 'CR', rotulo: 'CR — Radiografia (placa)' },
  { id: 'DX', rotulo: 'DX — Radiografia digital' },
  { id: 'CT', rotulo: 'CT — Tomografia' },
  { id: 'MR', rotulo: 'MR — Ressonância magnética' },
  { id: 'NM', rotulo: 'NM — Medicina nuclear' },
  { id: 'PT', rotulo: 'PT — PET' },
  { id: 'ECG', rotulo: 'ECG — Cardiologia (Holter, MAPA, ECG)' },
  { id: 'OT', rotulo: 'OT — Outro' },
];

export function rotuloModalidade(m: ModalidadeDicom): string {
  return MODALIDADES.find((x) => x.id === m)?.rotulo ?? m;
}

export type Equipamento = {
  id: string;
  nome: string;
  unidadeId: string;
  unidadeNome: string;
  modalidadeDicom: ModalidadeDicom;
  identificadorDicom: string | null;
  descricaoMaxCaracteres: number;
  ativo: boolean;
  criadoEm: string;
} & CodigoProcedimentoConfig;

export type EquipamentoListItem = {
  id: string;
  nome: string;
  unidadeId: string;
  unidadeNome: string;
  modalidadeDicom: ModalidadeDicom;
  identificadorDicom: string | null;
  descricaoMaxCaracteres: number;
  ativo: boolean;
} & CodigoProcedimentoConfig;

/**
 * Código do procedimento no item de worklist (ADR-0074): o código interno do SISREG vai em
 * (0008,0100), dentro da sequência que o aparelho lê. Desligado por padrão — liga-se aparelho a
 * aparelho, quando ele está cadastrado com esses códigos (ex.: o raio-X do CDT).
 */
export type CodigoProcedimentoConfig = {
  /** Requested Procedure Code Sequence (0032,1064). */
  codigoNoProcedimentoSolicitado: boolean;
  /** Scheduled Protocol Code Sequence (0040,0008), dentro do passo agendado. */
  codigoNoProtocoloAgendado: boolean;
  /** Esquema em (0008,0102). Vazio = o padrão, 99SMS. */
  codigoProcedimentoEsquema: string | null;
};

export type SalvarEquipamentoPayload = {
  nome: string;
  unidadeId: string;
  modalidadeDicom: ModalidadeDicom;
  identificadorDicom?: string | null;
  descricaoMaxCaracteres?: number;
  ativo?: boolean;
} & Partial<CodigoProcedimentoConfig>;

/** Esquema do código quando o campo fica vazio (esquema privado: começa com "99"). */
export const CODIGO_ESQUEMA_PADRAO = '99SMS';

/**
 * Teto do VR `LO` do DICOM — nenhum aparelho lê mais que isto, e é o padrão de todo
 * equipamento novo. Baixar só quando um console concreto falhar: o mamógrafo Fuji
 * FDR-3000AWS não monta a imagem (erro 31027) com descrição longa e fica em 16.
 */
export const DESCRICAO_MAX_PADRAO = 64;
export const DESCRICAO_MAX_MINIMO = 4;
