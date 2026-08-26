export type TipoPendenciaCadastro = 'NumeroErrado';

export type VinculoContato = 'NaoInformado' | 'Parente' | 'Responsavel' | 'SemVinculo';

export type StatusPendenciaCadastro = 'Aberta' | 'Resolvida' | 'Ignorada';

export type PendenciaCadastro = {
  id: string;
  telefoneCanonical: string;
  pacienteId: string | null;
  pacienteNome: string | null;
  pacienteCpf: string | null;
  tipo: TipoPendenciaCadastro;
  vinculo: VinculoContato;
  observacao: string | null;
  status: StatusPendenciaCadastro;
  criadaPeloRobo: boolean;
  criadoEm: string;
  resolvidoEm: string | null;
  resolucaoNota: string | null;
};
