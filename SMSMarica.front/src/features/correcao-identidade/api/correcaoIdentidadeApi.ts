import { http } from '@/shared/api/httpClient';

/** Como o exame que perdeu o estudo deve ser liberado. */
export const DestinoDaOrigem = {
  /** O paciente ainda não fez o exame — o item volta para a estação. */
  DevolverAWorklist: 1,
  /** Já foi feito (vai ser resolvido por associação) — não recria o item. */
  JaFoiFeito: 2,
} as const;

export type PreviaCorrecao = {
  studyInstanceUID: string;
  pacienteAtualNome: string | null;
  exameAtualId: string | null;
  accessionAtual: string | null;
  destinoPacienteNome: string;
  destinoExameId: string;
  destinoAccession: string;
  destinoProcedimento: string | null;
  rascunhosQueSeraoDescartados: number;
  temLaudoAssinado: boolean;
  comunicacaoJaEnviada: boolean;
  /** Estudo que o destino já possui — quando vem preenchido, o caso é de TROCA. */
  studyDoDestinoSugerido: string | null;
};

export async function obterPrevia(
  studyInstanceUID: string,
  accessionDestino?: string,
): Promise<PreviaCorrecao> {
  const { data } = await http.get<PreviaCorrecao>(
    `/correcao-identidade/previa/${encodeURIComponent(studyInstanceUID)}`,
    { params: accessionDestino ? { accessionDestino } : undefined },
  );
  return data;
}

export async function descartarEstudo(studyInstanceUID: string, motivo: string): Promise<void> {
  await http.post('/correcao-identidade/descartar', { studyInstanceUID, motivo });
}

export async function alterarDestino(
  studyInstanceUID: string,
  accessionDestino: string,
  destinoDaOrigem: number,
  motivo: string,
): Promise<void> {
  await http.post('/correcao-identidade/alterar-destino', {
    studyInstanceUID,
    accessionDestino,
    destinoDaOrigem,
    motivo,
  });
}

export async function trocarEstudos(
  studyInstanceUID: string,
  accessionDestino: string,
  studyInstanceUIDDoDestino: string,
  motivo: string,
): Promise<void> {
  await http.post('/correcao-identidade/trocar', {
    studyInstanceUID,
    accessionDestino,
    studyInstanceUIDDoDestino,
    motivo,
  });
}

export type IncidenteAberto = {
  id: string;
  studyInstanceUID: string;
  exameImagemId: string | null;
  pacienteSuspeitoNome: string | null;
  motivo: string;
  status: number;
  automatico: boolean;
  criadoEm: string;
  resolucaoNota: string | null;
};

/** Fila de exames que alguém colocou em conferência (status 1 = em aberto). */
export async function listarIncidentesAbertos(): Promise<IncidenteAberto[]> {
  const { data } = await http.get<IncidenteAberto[]>('/exames/incidentes-identidade', {
    params: { status: 1 },
  });
  return data;
}
