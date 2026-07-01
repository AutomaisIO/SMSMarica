export type StatusLaudo = 'Rascunho' | 'Finalizado';

/** Respostas do checklist enviadas ao salvar/finalizar (o servidor recalcula o sugerido). */
export type ChecklistLaudoInput = {
  respostasJson: string | null;
  contribuicoes: string[] | null;
  biRadsFinal: string | null;
};

export type Laudo = {
  id: string;
  studyInstanceUID: string;
  versao: number;
  laudoAnteriorId: string | null;
  pacienteId: string | null;
  pacienteNome: string | null;
  pacienteCpf: string | null;
  /** Nome cru do DICOM capturado na criação — rótulo temporário enquanto não há vínculo. */
  pacienteNomeDicom: string | null;
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
  biRads: string | null;
  biRadsSugerido: string | null;
  respostasChecklist: string | null;
  finalizadoEm: string | null;
  criadoEm: string;
  atualizadoEm: string | null;
  assinado: boolean;
  /** Elegibilidade da assinatura resolvida no servidor (autor + finalizado + não assinado + tem rubrica). */
  podeAssinar: boolean;
  /** Motivo legível do bloqueio (ex.: rubrica não cadastrada), ou null quando elegível/irrelevante. */
  motivoBloqueioAssinatura: string | null;
  /** O médico AUTOR tem rubrica de assinatura cadastrada. */
  medicoTemRubrica: boolean;
};

export type LaudoListItem = {
  id: string;
  studyInstanceUID: string;
  versao: number;
  pacienteId: string | null;
  pacienteNome: string | null;
  /** Nome cru do DICOM (rótulo temporário) enquanto o exame não tem vínculo. */
  pacienteNomeDicom: string | null;
  medicoId: string;
  medicoNome: string;
  titulo: string;
  status: StatusLaudo;
  biRads: string | null;
  finalizadoEm: string | null;
  criadoEm: string;
  assinado: boolean;
};

export type StatusAssinatura =
  | 'NaoIniciada'
  | 'Iniciada'
  | 'AguardandoAssinatura'
  | 'Concluida'
  | 'Falhou'
  | 'Cancelada';

export type AssinaturaStatus = {
  assinaturaId: string | null;
  status: StatusAssinatura;
  assinadoEm: string | null;
  certificadoTitular: string | null;
  formato: string | null;
};

/** Resposta do "iniciar": a chave de uso único que o front passa ao agente. */
export type IniciarAssinaturaResp = {
  assinaturaId: string;
  chave: string;
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
  assinado: boolean;
};

export type FiltroLaudos = {
  studyInstanceUID?: string;
  pacienteId?: string;
  medicoId?: string;
  status?: StatusLaudo;
  dataInicial?: string;
  dataFinal?: string;
  biRads?: string;
  /** true = só vinculados; false = só não vinculados; undefined = todos. */
  vinculado?: boolean;
  /** true = só assinados; false = só não assinados; undefined = todos. */
  assinado?: boolean;
  limite?: number;
};

export type CadastrarLaudoPayload = {
  studyInstanceUID: string;
  pacienteId: string | null;
  laudoTemplateId: string | null;
  titulo: string;
  conteudoJson: string;
  conteudoHtml: string;
  checklist?: ChecklistLaudoInput | null;
};

export type AtualizarLaudoPayload = {
  pacienteId: string | null;
  titulo: string;
  conteudoJson: string;
  conteudoHtml: string;
  checklist?: ChecklistLaudoInput | null;
};

export type FinalizarLaudoPayload = {
  titulo: string;
  conteudoJson: string;
  conteudoHtml: string;
  checklist?: ChecklistLaudoInput | null;
};
