import { useMutation } from '@tanstack/react-query';
import {
  confirmarTelefoneOtp,
  enviarTelefoneOtp,
} from '@/features/telefone-validacao/api/telefoneValidacaoApi';

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
