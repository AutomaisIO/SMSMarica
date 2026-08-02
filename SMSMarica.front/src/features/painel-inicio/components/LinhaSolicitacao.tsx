import { Link } from 'react-router-dom';
import { ArrowDownLeft, ArrowUpRight } from 'lucide-react';
import { formatarInstante } from '@/shared/lib/datas';
import type { ItemSolicitacaoPainel } from '@/features/painel-inicio/types';

/**
 * Uma linha de solicitação numa raia. A seta reusa o vocabulário que a listagem de solicitações
 * já ensinou ao operador: ↓ recebida (sou a executante) · ↑ enviada (eu pedi).
 */
export function LinhaSolicitacao({ item }: { item: ItemSolicitacaoPainel }) {
  return (
    <li>
      <Link
        to={`/app/solicitacoes-exame/${item.id}`}
        className="flex flex-wrap items-baseline gap-x-2 gap-y-1 py-2 text-sm hover:bg-gray-50"
      >
        {item.direcao && (
          <span
            title={item.direcao === 'Recebida' ? 'Recebida — sua unidade executa' : 'Enviada — sua unidade solicitou'}
            className="shrink-0"
          >
            {item.direcao === 'Recebida' ? (
              <ArrowDownLeft className="h-3.5 w-3.5 text-gray-400" />
            ) : (
              <ArrowUpRight className="h-3.5 w-3.5 text-gray-400" />
            )}
          </span>
        )}

        {/* Nome do paciente NÃO é abreviado: se não couber, quebra. */}
        <span className="font-medium text-gray-900">{item.pacienteNome ?? 'Paciente não identificado'}</span>

        {item.procedimento && <span className="text-gray-600">{item.procedimento}</span>}

        {item.dataAgendada && (
          <span className="text-gray-500 tabular-nums">{formatarInstante(item.dataAgendada)}</span>
        )}

        {item.unidadeNome && <span className="text-gray-500">· {item.unidadeNome}</span>}

        {/* O motivo vai LITERAL, entre aspas, sem categorizar (ADR-0034 §4). */}
        {item.motivoCancelamento && (
          <span className="basis-full truncate text-xs italic text-gray-500" title={item.motivoCancelamento}>
            “{item.motivoCancelamento}”
          </span>
        )}
      </Link>
    </li>
  );
}
