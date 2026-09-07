import { useQuery } from '@tanstack/react-query';
import { ArrowRight, Archive } from 'lucide-react';
import { Link } from 'react-router-dom';

import { http } from '@/shared/api/httpClient';

/**
 * As telas "Nova solicitação (SER)" e "Nova solicitação (SERNIT)" foram substituídas pelo wizard
 * de Regulação → Solicitações (tarefa 2.9 do plano 02). Depois que os rascunhos são migrados,
 * elas continuam abrindo — para consulta do que existia — mas não aceitam mais escrita.
 *
 * <p>A tela pergunta o estado ao servidor em vez de decidir sozinha porque o corte é uma data em
 * `regulacao_configuracao`, ligada pela migração: sem isso, ou a tela fecharia antes de os
 * rascunhos serem copiados, ou continuaria aceitando edição que se perderia na cópia.</p>
 */
export type SistemaRascunhoLegado = 'ser' | 'sernit';

export interface EstadoRascunhoLegado {
  somenteLeitura: boolean;
  migradoEm: string | null;
  /** Rota do wizard que substitui esta tela. */
  substituto: string;
}

export async function obterEstadoRascunhoLegado(
  sistema: SistemaRascunhoLegado,
): Promise<EstadoRascunhoLegado> {
  const { data } = await http.get<EstadoRascunhoLegado>(`/regulacao/${sistema}/rascunhos/estado`);
  return data;
}

export function useEstadoRascunhoLegado(sistema: SistemaRascunhoLegado) {
  return useQuery({
    queryKey: [sistema, 'rascunhos', 'estado'],
    queryFn: () => obterEstadoRascunhoLegado(sistema),
    staleTime: 60_000,
  });
}

/**
 * Banner de tela aposentada. Aparece só depois da migração — antes dela a página é a de sempre.
 */
export function AvisoRascunhoLegado({
  estado,
  sistema,
}: {
  estado: EstadoRascunhoLegado | undefined;
  sistema: 'SER' | 'SERNIT';
}) {
  if (!estado?.somenteLeitura) return null;

  return (
    <div className="flex flex-wrap items-start gap-2 rounded-lg border border-sky-300 bg-sky-50 p-3 text-sm text-sky-900">
      <Archive className="mt-0.5 size-4 shrink-0" />
      <p className="flex-1">
        <strong>Esta tela virou histórico.</strong> Os rascunhos do {sistema} foram migrados para{' '}
        <em>Regulação → Solicitações</em> e continuam aqui só para consulta. Abra novos pedidos
        pelo caminho novo — lá o pedido vale para o SER e para o SERNIT, com anexos e fila.
      </p>
      <Link
        to={estado.substituto}
        className="inline-flex items-center gap-1 rounded bg-sky-700 px-3 py-1.5 font-medium text-white hover:bg-sky-800"
      >
        Nova solicitação <ArrowRight className="size-4" />
      </Link>
    </div>
  );
}
