import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';

/** O que um canal faz na entrada do paciente (ADR-0078). */
export type CanalEntrada = { consultarEsus: boolean };

/** Um ajuste da configuração: quando, quem e o que mudou (já escrito pelo servidor). */
export type AjusteConfiguracao = { em: string; quem: string | null; mudancas: string[] };

export type ConfiguracaoAppCidadao = {
  pwa: CanalEntrada;
  nativo: CanalEntrada;
  /** A conta da plataforma no e-SUS está em Fontes de dados — sem ela, ligar a consulta não tem efeito. */
  esusConfigurado: boolean;
  atualizadoEm: string | null;
  atualizadoPor: string | null;
  ajustes: AjusteConfiguracao[];
};

const chave = ['app-cidadao', 'configuracao'] as const;

export function useConfiguracaoAppCidadao() {
  return useQuery({
    queryKey: chave,
    queryFn: async () => (await http.get<ConfiguracaoAppCidadao>('/app-cidadao/configuracao')).data,
  });
}

export function useSalvarConfiguracaoAppCidadao() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { pwa: CanalEntrada; nativo: CanalEntrada }) =>
      (await http.put<ConfiguracaoAppCidadao>('/app-cidadao/configuracao', payload)).data,
    onSuccess: (dados) => qc.setQueryData(chave, dados),
  });
}
