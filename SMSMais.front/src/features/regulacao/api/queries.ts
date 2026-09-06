import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  buscarProcedimentos,
  buscarPacienteLocal,
  confirmarCadsus,
  consultarCadsus,
  informarCpfPaciente,
  obterConfiguracaoFluxo,
  obterConfiguracaoRegulacao,
  salvarConfiguracaoRegulacao,
  confirmarPareamento,
  listarSugestoesPareamento,
  obterProcedimento,
  rejeitarPareamento,
  renomearCanonico,
  sincronizarCatalogo,
} from './regulacaoApi';
import type { TipoProcedimentoRegulacao } from '../types';

const raiz = ['regulacao', 'procedimentos'] as const;

/** O backend recusa termo com menos de 3 caracteres — não vale gastar requisição para levar 400. */
export const TAMANHO_MINIMO_BUSCA = 3;

export function useBuscaProcedimentos(q: string, tipo?: TipoProcedimentoRegulacao) {
  const termo = q.trim();
  return useQuery({
    queryKey: [...raiz, 'busca', termo, tipo ?? null],
    queryFn: () => buscarProcedimentos(termo, tipo),
    enabled: termo.length >= TAMANHO_MINIMO_BUSCA,
    // O catálogo muda por sincronismo, não por digitação: repetir o mesmo termo dentro de um
    // minuto não deve pagar embedding de novo.
    staleTime: 60_000,
  });
}

export function useProcedimento(id: string | null) {
  return useQuery({
    queryKey: [...raiz, 'detalhe', id],
    queryFn: () => obterProcedimento(id!),
    enabled: !!id,
  });
}

export function useSugestoesPareamento() {
  return useQuery({
    queryKey: [...raiz, 'sugestoes'],
    queryFn: listarSugestoesPareamento,
  });
}

function useInvalidarCatalogo() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: raiz });
}

export function useSincronizarCatalogo() {
  const invalidar = useInvalidarCatalogo();
  return useMutation({ mutationFn: sincronizarCatalogo, onSuccess: invalidar });
}

export function useConfirmarPareamento() {
  const invalidar = useInvalidarCatalogo();
  return useMutation({
    mutationFn: ({ origemId, procedimentoId }: { origemId: string; procedimentoId: string }) =>
      confirmarPareamento(origemId, procedimentoId),
    onSuccess: invalidar,
  });
}

export function useRejeitarPareamento() {
  const invalidar = useInvalidarCatalogo();
  return useMutation({ mutationFn: rejeitarPareamento, onSuccess: invalidar });
}

export function useRenomearCanonico() {
  const invalidar = useInvalidarCatalogo();
  return useMutation({
    mutationFn: ({ id, nome }: { id: string; nome: string }) => renomearCanonico(id, nome),
    onSuccess: invalidar,
  });
}

// ---------------------------------------------------------------- configuração

const raizConfig = ['regulacao', 'configuracao'] as const;

export function useConfiguracaoRegulacao() {
  return useQuery({ queryKey: raizConfig, queryFn: obterConfiguracaoRegulacao });
}

/** Usado pelo wizard: qualquer solicitante enxerga, e muda pouco. */
export function useConfiguracaoFluxo() {
  return useQuery({
    queryKey: [...raizConfig, 'fluxo'],
    queryFn: obterConfiguracaoFluxo,
    staleTime: 5 * 60_000,
  });
}

export function useSalvarConfiguracaoRegulacao() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: salvarConfiguracaoRegulacao,
    onSuccess: () => qc.invalidateQueries({ queryKey: raizConfig }),
  });
}

// ---------------------------------------------------------------- paciente

const raizPaciente = ['regulacao', 'pacientes'] as const;

export function useBuscarPacienteLocal(termo: string) {
  const t = termo.trim();
  return useQuery({
    queryKey: [...raizPaciente, 'busca', t],
    queryFn: () => buscarPacienteLocal(t),
    enabled: t.length >= 3,
  });
}

/** Mutation, e não query: consultar o CADSUS é ida a sistema externo e não pode disparar sozinho. */
export function useConsultarCadsus() {
  return useMutation({ mutationFn: consultarCadsus });
}

export function useConfirmarCadsus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: confirmarCadsus,
    onSuccess: () => qc.invalidateQueries({ queryKey: raizPaciente }),
  });
}

export function useInformarCpf() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, cpf }: { id: string; cpf: string }) => informarCpfPaciente(id, cpf),
    onSuccess: () => qc.invalidateQueries({ queryKey: raizPaciente }),
  });
}
