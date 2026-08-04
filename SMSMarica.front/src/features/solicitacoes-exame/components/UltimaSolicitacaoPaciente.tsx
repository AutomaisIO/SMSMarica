import { useQuery } from '@tanstack/react-query';
import { ChevronRight, Loader2 } from 'lucide-react';
import { usePermissao } from '@/shared/auth/authStore';
import { formatarInstante } from '@/shared/lib/datas';
import { listarSolicitacoes } from '@/features/solicitacoes-exame/api/solicitacoesExameApi';
import { StatusBadgeSolicitacao } from '@/features/solicitacoes-exame/components/StatusBadgeSolicitacao';

type Props = {
  /** Id do paciente cujas solicitações serão consultadas. */
  pacienteId: string;
  /** Chamado ao clicar no card — o caller decide como abrir a solicitação. */
  aoAbrir: (solicitacaoId: string) => void;
};

/**
 * Card-resumo da ÚLTIMA solicitação de exame do paciente (a mais recente por
 * criação, dentro do escopo de unidade do usuário). Usado no modal de resumo do
 * paciente; clicar no card abre a solicitação via `aoAbrir`. Some por completo
 * quando o usuário não tem consulta ao módulo de solicitações.
 */
export function UltimaSolicitacaoPaciente({ pacienteId, aoAbrir }: Props) {
  const podeVer = usePermissao('SolicitacoesExame', 'Consulta');

  const consulta = useQuery({
    queryKey: ['solicitacoes-exame', 'ultima-paciente', pacienteId],
    queryFn: () => listarSolicitacoes({ pacienteId, limite: 20 }),
    enabled: podeVer,
    staleTime: 30_000,
  });

  if (!podeVer) return null;

  // A listagem ordena urgentes primeiro; a "última" aqui é a mais RECENTE por criação.
  const itens = consulta.data?.itens ?? [];
  const ultima =
    itens.length > 0
      ? [...itens].sort((a, b) => b.criadoEm.localeCompare(a.criadoEm))[0]
      : null;

  return (
    <div className="mt-5 border-t border-gray-100 pt-4">
      <span className="text-xs font-medium uppercase tracking-wide text-gray-500">
        Última solicitação
      </span>

      {consulta.isLoading ? (
        <div className="mt-1.5 flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Buscando solicitações…
        </div>
      ) : consulta.isError ? (
        <p className="mt-1.5 text-sm text-gray-400">Não foi possível consultar as solicitações.</p>
      ) : !ultima ? (
        <p className="mt-1.5 text-sm text-gray-400">Nenhuma solicitação para este paciente.</p>
      ) : (
        <button
          type="button"
          onClick={() => aoAbrir(ultima.id)}
          title="Abrir a solicitação"
          className="mt-1.5 flex w-full items-center justify-between gap-3 rounded-lg border border-gray-200 px-3 py-2 text-left hover:border-primary-300 hover:bg-primary-50"
        >
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <span className="truncate text-sm font-medium text-gray-900">{ultima.tipoExameNome}</span>
              <StatusBadgeSolicitacao status={ultima.status} />
            </div>
            <div className="mt-0.5 truncate text-xs text-gray-500">
              {ultima.dataAgendada
                ? `Agendada para ${formatarInstante(ultima.dataAgendada)}`
                : `Criada em ${formatarInstante(ultima.criadoEm)}`}
              {' · Pedido '}
              {ultima.codigoSolicitacao || ultima.accessionNumber}
              {ultima.unidadeNome ? ` · ${ultima.unidadeNome}` : ''}
            </div>
          </div>
          <ChevronRight className="h-4 w-4 shrink-0 text-gray-400" />
        </button>
      )}
    </div>
  );
}
