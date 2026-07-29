import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  confirmarTelefoneOtp,
  enviarTelefoneOtp,
} from '@/features/telefone-validacao/api/telefoneValidacaoApi';
import {
  listarMotivosDispensa,
  obterDispensaAtiva,
  registrarDispensa,
  revogarDispensa,
} from '@/features/telefone-validacao/api/dispensaContatoApi';

// O "situação do validado" não tem mais consulta própria: o telefoneVerificado
// vem dentro do objeto do paciente (marcador no telecom FHIR).

export function useEnviarTelefoneOtp() {
  return useMutation({
    mutationFn: ({ cpf, numero }: { cpf: string; numero: string }) => enviarTelefoneOtp(cpf, numero),
  });
}

export function useConfirmarTelefoneOtp() {
  return useMutation({
    mutationFn: ({ cpf, numero, codigo }: { cpf: string; numero: string; codigo: string }) =>
      confirmarTelefoneOtp(cpf, numero, codigo),
  });
}

// ------------------------------------------------------------------------------------------
// Dispensa de verificação (o paciente consentiu em não validar o WhatsApp, com motivo).
// ------------------------------------------------------------------------------------------

export const dispensaKeys = {
  raiz: ['telefone-dispensa'] as const,
  motivos: ['telefone-dispensa', 'motivos'] as const,
  doPaciente: (pacienteId: string) => ['telefone-dispensa', 'paciente', pacienteId] as const,
};

/** Opções de motivo. Régua estática do backend — cacheia à vontade. */
export function useMotivosDispensa(habilitado = true) {
  return useQuery({
    queryKey: dispensaKeys.motivos,
    queryFn: listarMotivosDispensa,
    enabled: habilitado,
    staleTime: 60 * 60 * 1000,
  });
}

export function useDispensaAtiva(pacienteId?: string | null, habilitado = true) {
  return useQuery({
    queryKey: pacienteId ? dispensaKeys.doPaciente(pacienteId) : [...dispensaKeys.raiz, 'nenhum'],
    queryFn: () => obterDispensaAtiva(pacienteId!),
    enabled: Boolean(pacienteId) && habilitado,
  });
}

/**
 * Registrar/revogar mexe no gate da recepção: a solicitação precisa ser reconsultada para o
 * card de autorização trocar de estado sem F5, e o paciente para o selo do resumo acompanhar.
 */
function invalidarDependentes(client: ReturnType<typeof useQueryClient>, pacienteId: string) {
  client.invalidateQueries({ queryKey: dispensaKeys.doPaciente(pacienteId) });
  client.invalidateQueries({ queryKey: ['solicitacoes-exame'] });
  client.invalidateQueries({ queryKey: ['pacientes'] });
}

export function useRegistrarDispensa() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: registrarDispensa,
    onSuccess: (_d, v) => invalidarDependentes(client, v.pacienteId),
  });
}

export function useRevogarDispensa() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ pacienteId, motivo }: { pacienteId: string; motivo?: string }) =>
      revogarDispensa(pacienteId, motivo),
    onSuccess: (_d, v) => invalidarDependentes(client, v.pacienteId),
  });
}
