export type ModoSincronizacao = 'Completo' | 'Incremental';
export type EscopoSincronizacao = 'Limitado' | 'Tudo';

/** Uma base de PEP candidata à importação (vem do cadastro de fontes da IA). */
export type BasePep = {
  id: string;
  nome: string;
  tipo: string; // Salux, Mv, Eco...
  ambiente: string; // Producao, Treinamento
  suportada: boolean;
  ultimaSincronizacaoEm: string | null;
};

export type IniciarImportacaoPayload = {
  fonteId: string;
  modo: ModoSincronizacao;
  escopo: EscopoSincronizacao;
  maxMedicos: number | null;
  maxPacientes: number | null;
  apagarAntes: boolean;
};

export type ContadoresImportacao = {
  medicos: number;
  pacientes: number;
  encounters: number;
  conditions: number;
  medicationRequests: number;
  documentReferences: number;
  observations: number;
  falhas: number;
};

export type StatusImportacao = {
  execucaoId: string | null;
  emExecucao: boolean;
  fonteId: string | null;
  fonteNome: string | null;
  modo: string | null;
  escopo: string | null;
  status: string; // Pendente, EmExecucao, Concluido, Erro, Nenhuma
  faseAtual: string | null;
  iniciadoEm: string | null;
  finalizadoEm: string | null;
  decorridoSegundos: number | null;
  contadores: ContadoresImportacao;
  mensagemErro: string | null;
};

export type ExecucaoImportacao = {
  id: string;
  fonteId: string;
  fonteNome: string;
  modo: string;
  escopo: string;
  status: string;
  iniciadoEm: string;
  finalizadoEm: string | null;
  duracaoSegundos: number | null;
  contadores: ContadoresImportacao;
  temposJson: string | null;
  mensagemErro: string | null;
};
