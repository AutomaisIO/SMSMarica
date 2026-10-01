import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  autorizarAtivacao,
  definirCanal,
  gerarChavePublicacao,
  listarDispositivos,
  listarPacotes,
  obterAtivacao,
  obterChavePublicacao,
  obterSituacao,
  promoverPacote,
  publicarPacote,
  retirarPacote,
  revogarChavePublicacao,
  revogarDispositivo,
} from '@/features/extensao-navegador/api/extensaoApi';
import type { Canal, PublicarPacote } from '@/features/extensao-navegador/types';

export const extensaoKeys = {
  raiz: ['extensao-navegador'] as const,
  situacao: () => ['extensao-navegador', 'situacao'] as const,
  ativacao: (codigo: string) => ['extensao-navegador', 'ativacao', codigo] as const,
  dispositivos: () => ['extensao-navegador', 'dispositivos'] as const,
  pacotes: () => ['extensao-navegador', 'pacotes'] as const,
  chave: () => ['extensao-navegador', 'chave'] as const,
};

export function useSituacaoDistribuicao() {
  return useQuery({ queryKey: extensaoKeys.situacao(), queryFn: obterSituacao });
}

/**
 * O pedido de autorização. Depois de autorizado, o computador busca o token sozinho em poucos
 * segundos — a página acompanha até ver que ele já está ligado.
 */
export function useAtivacao(codigo: string) {
  return useQuery({
    queryKey: extensaoKeys.ativacao(codigo),
    queryFn: () => obterAtivacao(codigo),
    enabled: codigo.length > 0,
    retry: false,
    refetchInterval: (consulta) => (consulta.state.data?.situacao === 'Autorizada' ? 3_000 : false),
  });
}

export function useAutorizarAtivacao(codigo: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => autorizarAtivacao(codigo),
    onSuccess: (dados) => client.setQueryData(extensaoKeys.ativacao(codigo), dados),
  });
}

/** Os computadores informam o próprio estado a cada 10 minutos; a lista se renova a cada minuto. */
export function useDispositivos() {
  return useQuery({ queryKey: extensaoKeys.dispositivos(), queryFn: listarDispositivos, refetchInterval: 60_000 });
}

export function useDefinirCanal() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, canal }: { id: string; canal: Canal }) => definirCanal(id, canal),
    onSuccess: () => client.invalidateQueries({ queryKey: extensaoKeys.dispositivos() }),
  });
}

export function useRevogarDispositivo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => revogarDispositivo(id),
    onSuccess: () => client.invalidateQueries({ queryKey: extensaoKeys.dispositivos() }),
  });
}

export function usePacotes() {
  return useQuery({ queryKey: extensaoKeys.pacotes(), queryFn: listarPacotes });
}

function useMutacaoDePacote<T, R>(mutationFn: (entrada: T) => Promise<R>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => {
      // A versão em vigor muda o que a tela de "Extensão Chrome" oferece para baixar.
      void client.invalidateQueries({ queryKey: extensaoKeys.pacotes() });
      void client.invalidateQueries({ queryKey: extensaoKeys.situacao() });
    },
  });
}

export function usePublicarPacote() {
  return useMutacaoDePacote((pedido: PublicarPacote) => publicarPacote(pedido));
}

export function usePromoverPacote() {
  return useMutacaoDePacote((id: string) => promoverPacote(id));
}

export function useRetirarPacote() {
  return useMutacaoDePacote((id: string) => retirarPacote(id));
}

export function useChavePublicacao() {
  return useQuery({ queryKey: extensaoKeys.chave(), queryFn: obterChavePublicacao });
}

export function useGerarChavePublicacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: gerarChavePublicacao,
    onSuccess: (dados) => client.setQueryData(extensaoKeys.chave(), dados.situacao),
  });
}

export function useRevogarChavePublicacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: revogarChavePublicacao,
    onSuccess: (dados) => client.setQueryData(extensaoKeys.chave(), dados),
  });
}
