import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/ui/Modal';
import { useOcupacaoDoDia } from '../api/queries';
import type { OcupanteDaVaga } from '../types';

/** O dia clicado no "Quando dá para marcar". */
export type AlvoOcupacao = {
  codigo: string;
  unidadeId: string;
  unidadeNome: string;
  agendaLocal: boolean;
  /** aaaa-mm-dd */
  data: string;
  /** "sex 13/11" */
  rotuloDia: string;
  /** "14:00–14:40" */
  horario: string;
};

const CONFIRMACAO: Record<OcupanteDaVaga['statusConfirmacao'], { rotulo: string; classe: string }> = {
  Pendente: { rotulo: 'sem resposta', classe: 'bg-gray-100 text-gray-600' },
  Confirmada: { rotulo: 'confirmou', classe: 'bg-emerald-100 text-emerald-800' },
  Cancelada: { rotulo: 'avisou que não vai', classe: 'bg-red-100 text-red-700' },
};

function Linha({ o }: { o: OcupanteDaVaga }) {
  const conf = CONFIRMACAO[o.statusConfirmacao] ?? CONFIRMACAO.Pendente;
  return (
    <li className="flex items-start gap-3 py-2.5">
      <span className="w-12 shrink-0 pt-0.5 text-sm font-semibold tabular-nums text-gray-900">
        {o.hora.slice(0, 5)}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-gray-900">
          {o.pacienteNome ?? <span className="italic text-gray-500">nome não encontrado</span>}
          {o.idadeAnos != null ? <span className="font-normal text-gray-500"> · {o.idadeAnos} anos</span> : null}
        </p>
        <p className="mt-0.5 text-xs text-gray-600">
          {[o.procedimentoTexto, o.profissionalExecutanteNome].filter(Boolean).join(' · ')}
        </p>
        <p className="mt-0.5 text-[11px] text-gray-500">
          {[
            o.unidadeSolicitante ? `pedido por ${o.unidadeSolicitante}` : null,
            o.codigoSolicitacao ? `SISREG ${o.codigoSolicitacao}` : null,
            o.cns ? `CNS ${o.cns}` : null,
          ]
            .filter(Boolean)
            .join(' · ')}
        </p>
      </div>
      <span className={`shrink-0 rounded px-1.5 py-0.5 text-[11px] ${conf.classe}`}>{conf.rotulo}</span>
    </li>
  );
}

/**
 * Quem está ocupando as vagas de um dia — o clique no cartão "3 de 4 livres".
 *
 * <p>Mesma conta do cartão: agendamentos já importados do SISREG para a unidade, o dia e o
 * procedimento (ou a família dele, quando a escala é de grupo), sem os cancelados.</p>
 */
export function OcupacaoDoDiaModal({ alvo, aoFechar }: { alvo: AlvoOcupacao | null; aoFechar: () => void }) {
  const { data, isLoading, isError } = useOcupacaoDoDia(alvo);

  return (
    <Modal
      aberto={alvo !== null}
      aoFechar={aoFechar}
      titulo={alvo ? `Quem está na vaga — ${alvo.rotuloDia}` : ''}
      descricao={alvo ? `${alvo.unidadeNome} · ${alvo.horario}${alvo.agendaLocal ? ' · agenda local' : ''}` : undefined}
      largura="lg"
    >
      {isLoading ? (
        <p className="flex items-center gap-2 py-4 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" />
          Buscando os agendamentos do dia…
        </p>
      ) : null}
      {isError ? (
        <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">Não foi possível carregar quem ocupa a vaga.</p>
      ) : null}

      {data ? (
        <>
          <p className="text-sm text-gray-700">
            <strong className="text-gray-900">{data.ocupantes.length}</strong> agendado(s) ·{' '}
            <strong className="text-emerald-700">{data.livres}</strong> de {data.vagas} vaga(s) da regulação
            livre(s)
          </p>

          {data.ocupantes.length > 0 ? (
            <ul className="mt-3 divide-y divide-gray-100 rounded-lg border border-gray-200 px-3">
              {data.ocupantes.map((o) => (
                <Linha key={o.solicitacaoId} o={o} />
              ))}
            </ul>
          ) : (
            <p className="mt-3 rounded-lg border border-dashed border-gray-200 p-4 text-center text-sm text-gray-500">
              Ninguém marcado neste dia até agora.
            </p>
          )}

          <p className="mt-3 text-[11px] text-gray-500">
            São os agendamentos que já importamos do SISREG. Uma marcação feita há pouco pode ainda não
            ter chegado — a grade do SISREG é a verdade.
          </p>
        </>
      ) : null}
    </Modal>
  );
}
