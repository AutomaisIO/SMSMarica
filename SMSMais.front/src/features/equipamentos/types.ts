export type ModalidadeDicom = 'CR' | 'DX' | 'MG' | 'US' | 'CT' | 'MR' | 'NM' | 'PT' | 'OT';

export const MODALIDADES: { id: ModalidadeDicom; rotulo: string }[] = [
  { id: 'US', rotulo: 'US — Ultrassom' },
  { id: 'MG', rotulo: 'MG — Mamografia' },
  { id: 'CR', rotulo: 'CR — Radiografia (placa)' },
  { id: 'DX', rotulo: 'DX — Radiografia digital' },
  { id: 'CT', rotulo: 'CT — Tomografia' },
  { id: 'MR', rotulo: 'MR — Ressonância magnética' },
  { id: 'NM', rotulo: 'NM — Medicina nuclear' },
  { id: 'PT', rotulo: 'PT — PET' },
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
};

export type EquipamentoListItem = {
  id: string;
  nome: string;
  unidadeId: string;
  unidadeNome: string;
  modalidadeDicom: ModalidadeDicom;
  identificadorDicom: string | null;
  descricaoMaxCaracteres: number;
  ativo: boolean;
};

export type SalvarEquipamentoPayload = {
  nome: string;
  unidadeId: string;
  modalidadeDicom: ModalidadeDicom;
  identificadorDicom?: string | null;
  descricaoMaxCaracteres?: number;
  ativo?: boolean;
};

/**
 * Teto do VR `LO` do DICOM — nenhum aparelho lê mais que isto, e é o padrão de todo
 * equipamento novo. Baixar só quando um console concreto falhar: o mamógrafo Fuji
 * FDR-3000AWS não monta a imagem (erro 31027) com descrição longa e fica em 16.
 */
export const DESCRICAO_MAX_PADRAO = 64;
export const DESCRICAO_MAX_MINIMO = 4;
