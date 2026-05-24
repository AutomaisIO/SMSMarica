export type StatusLaudo = 'Rascunho' | 'Finalizado';

export type Laudo = {
  id: string;
  studyInstanceUID: string;
  versao: number;
  laudoAnteriorId: string | null;
  pacienteId: string | null;
  pacienteNome: string | null;
  pacienteCpf: string | null;
  medicoId: string;
  medicoNome: string;
  medicoCrm: string;
  medicoUfCrm: string;
  medicoRqe: string | null;
  laudoTemplateId: string | null;
  laudoTemplateNome: string | null;
  titulo: string;
  conteudoJson: string;
  conteudoHtml: string;
  status: StatusLaudo;
  finalizadoEm: string | null;
  criadoEm: string;
  atualizadoEm: string | null;
};

export type LaudoListItem = {
  id: string;
  studyInstanceUID: string;
  versao: number;
  pacienteId: string | null;
  pacienteNome: string | null;
  medicoId: string;
  medicoNome: string;
  titulo: string;
  status: StatusLaudo;
  finalizadoEm: string | null;
  criadoEm: string;
};

export type LaudoHistoricoItem = {
  id: string;
  versao: number;
  status: StatusLaudo;
  medicoId: string;
  medicoNome: string;
  criadoEm: string;
  finalizadoEm: string | null;
};

export type LaudoPorStudy = {
  studyInstanceUID: string;
  laudoId: string;
  versao: number;
  status: StatusLaudo;
};

export type FiltroLaudos = {
  studyInstanceUID?: string;
  pacienteId?: string;
  medicoId?: string;
  status?: StatusLaudo;
  dataInicial?: string;
  dataFinal?: string;
  limite?: number;
};

export type CadastrarLaudoPayload = {
  studyInstanceUID: string;
  pacienteId: string | null;
  laudoTemplateId: string | null;
  titulo: string;
  conteudoJson: string;
  conteudoHtml: string;
};

export type AtualizarLaudoPayload = {
  pacienteId: string | null;
  titulo: string;
  conteudoJson: string;
  conteudoHtml: string;
};

export type FinalizarLaudoPayload = {
  titulo: string;
  conteudoJson: string;
  conteudoHtml: string;
};
