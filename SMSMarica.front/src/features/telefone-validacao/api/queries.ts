import { useMutation, useQuery } from '@tanstack/react-query';
import {
  confirmarTelefoneOtp,
  enviarTelefoneOtp,
  obterTelefoneValidado,
} from '@/features/telefone-validacao/api/telefoneValidacaoApi';

/** Só consulta quando há CPF (11 díg.) e número suficiente. */
export function useTelefoneValidado(cpf: string | null, numero: string | null) {
  return useQuery({
    queryKey: ['telefone-validado', cpf, numero],
    queryFn: () => obterTelefoneValidado(cpf as string, numero as string),
    enabled: Boolean(cpf) && Boolean(numero),
    staleTime: 30_000,
  });
}

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
