import { http } from '@/shared/api/httpClient';

/**
 * Dispensa de verificação do contato: o registro de que o paciente NÃO vai validar o WhatsApp,
 * e por quê. É o que destrava a autorização presencial na recepção para quem não tem celular
 * ou não consegue confirmar o código.
 */

export type MotivoDispensaContato =
  | 'SemCelular'
  | 'SemWhatsApp'
  | 'NumeroDeTerceiro'
  | 'NaoConsegueConfirmar'
  | 'SemSinalNoMomento'
  | 'RecusaValidar'
  | 'Outro';

export type MotivoDispensaOpcao = {
  motivo: MotivoDispensaContato;
  rotulo: string;
  /** Depois de dispensar por este motivo, resultado/laudo ainda saem por WhatsApp? */
  permiteEnvio: boolean;
  /** Frase que explica ao operador o efeito da escolha (mostrada no modal). */
  consequencia: string;
  exigeDescricao: boolean;
};

export type DispensaContato = {
  id: string;
  pacienteId: string;
  motivo: MotivoDispensaContato;
  motivoRotulo: string;
  motivoDescricao: string | null;
  /** Texto pronto para a tela (a descrição livre quando o motivo é "Outro"). */
  motivoTexto: string;
  permiteEnvio: boolean;
  criadoEm: string;
  criadoPor: string | null;
  criadoPorNome: string | null;
};

/** Opções de motivo — a régua vive no backend; o front não duplica rótulo nem consequência. */
export async function listarMotivosDispensa(): Promise<MotivoDispensaOpcao[]> {
  const { data } = await http.get<MotivoDispensaOpcao[]>('/telefones/dispensa/motivos');
  return data;
}

/** Dispensa ATIVA do paciente. 204 (sem corpo) → null. */
export async function obterDispensaAtiva(pacienteId: string): Promise<DispensaContato | null> {
  const { data, status } = await http.get<DispensaContato | ''>(`/telefones/dispensa/${pacienteId}`);
  return status === 204 || !data ? null : (data as DispensaContato);
}

export async function registrarDispensa(entrada: {
  pacienteId: string;
  motivo: MotivoDispensaContato;
  motivoDescricao?: string | null;
  pacienteCiente: boolean;
}): Promise<DispensaContato> {
  const { data } = await http.post<DispensaContato>('/telefones/dispensa', entrada);
  return data;
}

export async function revogarDispensa(pacienteId: string, motivo?: string): Promise<void> {
  await http.post(`/telefones/dispensa/${pacienteId}/revogar`, { motivo: motivo ?? null });
}
