// Unidade de atendimento = DESTINO do Transporte de Pacientes (clínica, hospital de referência).
// Cadastro manual e próprio do transporte — não é a "Unidade" de saúde do SISREG/CNES.

export type EnderecoUnidadeAtendimento = {
  cep: string;
  logradouro: string;
  numero: string | null;
  complemento: string | null;
  bairro: string;
  cidade: string;
  uf: string;
  pontoReferencia: string | null;
};

export type UnidadeAtendimentoListItem = {
  id: string;
  nome: string;
  logradouro: string | null;
  numero: string | null;
  bairro: string | null;
  cidade: string | null;
  uf: string | null;
  temCoordenada: boolean;
  externa: boolean;
  ativo: boolean;
  tratamentosAtivos: number;
};

export type UnidadeAtendimento = {
  id: string;
  nome: string;
  endereco: EnderecoUnidadeAtendimento | null;
  telefone: string | null;
  observacoes: string | null;
  latitude: number | null;
  longitude: number | null;
  externa: boolean;
  ativo: boolean;
  tratamentosAtivos: number;
  criadoEm: string;
  atualizadoEm: string | null;
};

export type SalvarUnidadeAtendimentoPayload = {
  nome: string;
  endereco: EnderecoUnidadeAtendimento;
  telefone: string | null;
  observacoes: string | null;
  latitude: number;
  longitude: number;
  externa: boolean;
};
