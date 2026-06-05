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
  ativo: boolean;
  criadoEm: string;
};

export type EquipamentoListItem = {
  id: string;
  nome: string;
  unidadeId: string;
  unidadeNome: string;
  modalidadeDicom: ModalidadeDicom;
  ativo: boolean;
};

export type SalvarEquipamentoPayload = {
  nome: string;
  unidadeId: string;
  modalidadeDicom: ModalidadeDicom;
  identificadorDicom?: string | null;
  ativo?: boolean;
};
