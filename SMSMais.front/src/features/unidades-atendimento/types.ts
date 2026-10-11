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

/** Uma unidade de atendimento como opção de ligação de um nome externo. */
export type UnidadeAtendimentoOpcao = { id: string; nome: string; temEndereco: boolean };

/**
 * Um local como o SER, o SERNIT ou o ESUS o escrevem, com os agendamentos futuros que o usam. A
 * unidade LIGADA (por uma pessoa) é de onde sai o endereço do aviso ao paciente; a SUGERIDA é só
 * um palpite da máquina, que nunca vale sozinho.
 */
export type LocalAgendamentoExterno = {
  nome: string;
  sistemas: string[];
  agendamentosFuturos: number;
  proximaData: string | null;
  ligada: UnidadeAtendimentoOpcao | null;
  sugerida: UnidadeAtendimentoOpcao | null;
};
