export const TIPOS_VEICULO = {
  Carro: 1,
  Van: 2,
  MicroOnibus: 3,
  Onibus: 4,
  Ambulancia: 5,
  Outro: 99,
} as const;

export type TipoVeiculo = (typeof TIPOS_VEICULO)[keyof typeof TIPOS_VEICULO];

export const ROTULOS_TIPO_VEICULO: Record<TipoVeiculo, string> = {
  [TIPOS_VEICULO.Carro]: 'Carro',
  [TIPOS_VEICULO.Van]: 'Van',
  [TIPOS_VEICULO.MicroOnibus]: 'Micro-ônibus',
  [TIPOS_VEICULO.Onibus]: 'Ônibus',
  [TIPOS_VEICULO.Ambulancia]: 'Ambulância',
  [TIPOS_VEICULO.Outro]: 'Outro',
};

export const TIPOS_ASSENTO = {
  Motorista: 1,
  Passageiro: 2,
  Acompanhante: 3,
} as const;

export type TipoAssento = (typeof TIPOS_ASSENTO)[keyof typeof TIPOS_ASSENTO];

export const ROTULOS_TIPO_ASSENTO: Record<TipoAssento, string> = {
  [TIPOS_ASSENTO.Motorista]: 'Motorista',
  [TIPOS_ASSENTO.Passageiro]: 'Passageiro',
  [TIPOS_ASSENTO.Acompanhante]: 'Acompanhante',
};

export type VeiculoListItem = {
  id: string;
  placa: string;
  modelo: string;
  fabricante: string;
  cor: string;
  tipo: TipoVeiculo;
  ativo: boolean;
};

export type AssentoDto = {
  id: string;
  numero: number;
  tipo: TipoAssento;
};

export type FileiraDto = {
  id: string;
  ordem: number;
  quantidadeAssentos: number;
  assentos: AssentoDto[];
};

export type Veiculo = {
  id: string;
  placa: string;
  modelo: string;
  fabricante: string;
  cor: string;
  tipo: TipoVeiculo;
  ativo: boolean;
  criadoEm: string;
  fileiras: FileiraDto[];
};

export type AssentoInput = {
  numero: number;
  tipo: TipoAssento;
};

export type FileiraInput = {
  ordem: number;
  assentos: AssentoInput[];
};

export type CadastrarVeiculoPayload = {
  placa: string;
  modelo: string;
  fabricante: string;
  cor: string;
  tipo: TipoVeiculo;
  fileiras: FileiraInput[];
};

export type AtualizarVeiculoPayload = {
  placa: string;
  modelo: string;
  fabricante: string;
  cor: string;
  tipo: TipoVeiculo;
};
