import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';

/** Os destinos que recebem anexo da plataforma (envio automático). */
export type DestinoComAnexo = 'ser' | 'sernit';

/** A regra de anexos do destino: o tamanho máximo por arquivo, em MB como o Windows mostra. */
export type RegrasAnexoDestino = {
  tamanhoMaximoMb: number;
  /** `false` = ninguém configurou ainda; vale o padrão. */
  configurado: boolean;
};

export async function obterRegrasAnexo(destino: DestinoComAnexo): Promise<RegrasAnexoDestino> {
  const { data } = await http.get<RegrasAnexoDestino>(`/regulacao/${destino}/configuracao/anexos`);
  return data;
}

export async function salvarRegrasAnexo(
  destino: DestinoComAnexo,
  tamanhoMaximoMb: number,
): Promise<RegrasAnexoDestino> {
  const { data } = await http.put<RegrasAnexoDestino>(`/regulacao/${destino}/configuracao/anexos`, {
    tamanhoMaximoMb,
  });
  return data;
}

export function useRegrasAnexo(destino: DestinoComAnexo) {
  return useQuery({
    queryKey: ['regulacao', 'regras-anexo', destino],
    queryFn: () => obterRegrasAnexo(destino),
  });
}

export function useSalvarRegrasAnexo(destino: DestinoComAnexo) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (tamanhoMaximoMb: number) => salvarRegrasAnexo(destino, tamanhoMaximoMb),
    onSuccess: (dados) => qc.setQueryData(['regulacao', 'regras-anexo', destino], dados),
  });
}
