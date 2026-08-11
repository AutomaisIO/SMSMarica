import { http } from '@/shared/api/httpClient';

export type IncidenteIdentidade = {
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

/**
 * Congela um exame por suspeita de estar no paciente errado. Enquanto estiver congelado, não se
 * lauda, não se assina, o aviso não sai para o paciente e o motor não mexe no vínculo.
 *
 * Acessível a quem só tem Consulta no PACS de propósito: quem percebe a troca é quem está
 * olhando o exame, não o administrador.
 */
export async function abrirIncidente(
  studyInstanceUID: string,
  motivo: string,
): Promise<IncidenteIdentidade> {
  const { data } = await http.post<IncidenteIdentidade>('/exames/incidentes-identidade', {
    studyInstanceUID,
    motivo,
  });
  return data;
}

/** Fila de exames em conferência. `status` 1 = em aberto. */
export async function listarIncidentes(status?: number): Promise<IncidenteIdentidade[]> {
  const { data } = await http.get<IncidenteIdentidade[]>('/exames/incidentes-identidade', {
    params: status ? { status } : undefined,
  });
  return data;
}
