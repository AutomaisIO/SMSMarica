import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarConfiguracaoSisreg,
  cancelarMapeamentoLote,
  obterAgendamentoMapeamentoLote,
  obterConfiguracaoSisreg,
  obterStatusMapeamentoLote,
  salvarAgendamentoMapeamentoLote,
  sincronizarMapeamentoLote,
} from '@/features/sisreg/api/sisregApi';
import type {
  AtualizarSisregConfiguracaoPayload,
  MapeamentoLoteAgendamento,
} from '@/features/sisreg/types';

export const sisregKeys = {
  configuracao: ['sisreg', 'configuracao'] as const,
  loteStatus: ['sisreg', 'lote', 'status'] as const,
  loteAgendamento: ['sisreg', 'lote', 'agendamento'] as const,
};

export function useConfiguracaoSisreg() {
  return useQuery({
    queryKey: sisregKeys.configuracao,
    queryFn: obterConfiguracaoSisreg,
  });
}

export function useAtualizarConfiguracaoSisreg() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AtualizarSisregConfiguracaoPayload) => atualizarConfiguracaoSisreg(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: sisregKeys.configuracao }),
  });
}

// ----------------------------------------------------------- lote "sincroniza tudo" (#118)

/** Enquanto há um lote rodando, refaz o status a cada 3s para a barra de progresso andar. */
export function useStatusMapeamentoLote() {
  return useQuery({
    queryKey: sisregKeys.loteStatus,
    queryFn: obterStatusMapeamentoLote,
    refetchInterval: (query) => (query.state.data?.emExecucao ? 3000 : false),
  });
}

export function useSincronizarMapeamentoLote() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: sincronizarMapeamentoLote,
    onSuccess: () => client.invalidateQueries({ queryKey: sisregKeys.loteStatus }),
  });
}

export function useCancelarMapeamentoLote() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: cancelarMapeamentoLote,
    onSuccess: () => client.invalidateQueries({ queryKey: sisregKeys.loteStatus }),
  });
}

export function useAgendamentoMapeamentoLote() {
  return useQuery({
    queryKey: sisregKeys.loteAgendamento,
    queryFn: obterAgendamentoMapeamentoLote,
  });
}

export function useSalvarAgendamentoMapeamentoLote() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: MapeamentoLoteAgendamento) => salvarAgendamentoMapeamentoLote(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: sisregKeys.loteAgendamento }),
  });
}
