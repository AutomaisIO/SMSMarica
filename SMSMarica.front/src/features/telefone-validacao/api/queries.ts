import { useMutation, useQuery } from '@tanstack/react-query';
import {
  confirmarTelefoneOtp,
  enviarTelefoneOtp,
  obterTelefoneValidado,
} from '@/features/telefone-validacao/api/telefoneValidacaoApi';

/** Só consulta quando há dígitos suficientes (DDD + número). */
export function useTelefoneValidado(numero: string | null) {
  return useQuery({
    queryKey: ['telefone-validado', numero],
    queryFn: () => obterTelefoneValidado(numero as string),
    enabled: Boolean(numero),
    staleTime: 30_000,
  });
}

export function useEnviarTelefoneOtp() {
  return useMutation({ mutationFn: (numero: string) => enviarTelefoneOtp(numero) });
}

export function useConfirmarTelefoneOtp() {
  return useMutation({
    mutationFn: ({ numero, codigo }: { numero: string; codigo: string }) =>
      confirmarTelefoneOtp(numero, codigo),
  });
}
