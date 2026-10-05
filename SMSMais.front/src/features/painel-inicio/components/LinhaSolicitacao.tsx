import { useNavigate } from 'react-router-dom';
import { ArrowDownLeft, ArrowUpRight } from 'lucide-react';
import { formatarInstante } from '@/shared/lib/datas';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import type { ItemSolicitacaoPainel } from '@/features/painel-inicio/types';

/**
 * Uma linha de solicitação numa raia. A seta reusa o vocabulário que a listagem de solicitações
 * já ensinou ao operador: ↓ recebida (sou a executante) · ↑ enviada (eu pedi).
 *
 * A linha é um div clicável (não <Link>) para poder aninhar o nome-padrão do paciente, que tem
 * botões (resumo, WhatsApp) e abre diálogos — dentro de um <a> qualquer clique neles navegaria.
 */
export function LinhaSolicitacao({ item }: { item: ItemSolicitacaoPainel }) {
  const navigate = useNavigate();
  const rota = `/app/solicitacoes/${item.id}`;

  function abrir(novaAba: boolean) {
    if (novaAba) window.open(rota, '_blank', 'noopener');
    else navigate(rota);
  }

  return (
    <li>
      <div
        role="link"
        tabIndex={0}
        onClick={(e) => abrir(e.ctrlKey || e.metaKey)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') abrir(e.ctrlKey || e.metaKey);
        }}
        className="flex cursor-pointer flex-wrap items-baseline gap-x-2 gap-y-1 py-2 text-sm hover:bg-gray-50"
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

        {/* Nome do paciente NÃO é abreviado: se não couber, quebra. Cliques e teclas no nome
            (resumo, WhatsApp e os diálogos que eles abrem) não chegam à linha. */}
        <span
          onClick={(e) => e.stopPropagation()}
          onKeyDown={(e) => e.stopPropagation()}
        >
          <NomePacienteComResumo
            pacienteId={item.pacienteId}
            nome={item.pacienteNome ?? 'Paciente não identificado'}
            classNameNome="font-medium text-gray-900"
          />
        </span>

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
      </div>
    </li>
  );
}
