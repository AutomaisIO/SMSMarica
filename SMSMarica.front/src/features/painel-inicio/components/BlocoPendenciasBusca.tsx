import { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { formatarInstante } from '@/shared/lib/datas';
import { usePendenciasImportacaoBusca } from '@/features/painel-inicio/api/queries';
import { ROTULO_CAUSA } from '@/features/painel-inicio/components/LinhaPendencia';
import { ModalInformarCpf } from '@/features/painel-inicio/components/ModalInformarCpf';
import type { PendenciaImportacaoBusca } from '@/features/painel-inicio/types';

/**
 * O bloco que faz a recepção achar quem chegou e "não tem agendamento" (ADR-0035 §3).
 *
 * Fica ACIMA da lista e é visualmente distinto — nunca uma linha da tabela. O rótulo é literal e
 * negativo ("não entraram no sistema") porque o pior modo de falha deste módulo é a atendente
 * ler isto como "está agendado" e mandar o paciente embora.
 */
export function BlocoPendenciasBusca({ busca }: { busca: string }) {
  const [alvo, setAlvo] = useState<PendenciaImportacaoBusca | null>(null);
  const { data } = usePendenciasImportacaoBusca(busca);

  if (!data?.length) return null;

  return (
    <>
      <section className="rounded-lg border border-amber-300 bg-amber-50 p-3">
        <header className="mb-2 flex items-start gap-2">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
          <p className="text-sm font-semibold text-amber-900">
            {data.length} {data.length === 1 ? 'pendência' : 'pendências'} de importação para «{busca.trim()}» —{' '}
            <span className="font-bold">não entraram no sistema</span>
          </p>
        </header>

        <ul className="space-y-1.5">
          {data.map((p) => (
            <li key={p.id} className="flex flex-wrap items-baseline gap-x-2 gap-y-1 text-sm text-amber-900">
              <span className="font-medium">{p.nomePaciente ?? 'Paciente não identificado'}</span>
              {p.procedimentoTexto && <span className="text-amber-800">{p.procedimentoTexto}</span>}
              {p.dataAgendada && (
                <span className="tabular-nums text-amber-700">{formatarInstante(p.dataAgendada)}</span>
              )}
              {p.codigoSolicitacao && (
                <span className="text-xs text-amber-700">SISREG nº {p.codigoSolicitacao}</span>
              )}
              <span className="text-xs text-amber-700" title={p.motivo}>
                · {ROTULO_CAUSA[p.causa]}
              </span>
              {p.podeInformarCpf && (
                <Button variante="outline" className="ml-auto h-7 px-2 text-xs" onClick={() => setAlvo(p)}>
                  Informar CPF e importar
                </Button>
              )}
            </li>
          ))}
        </ul>
      </section>

      <ModalInformarCpf
        pendencia={
          alvo && {
            id: alvo.id,
            pacienteNome: alvo.nomePaciente,
            procedimento: alvo.procedimentoTexto,
            codigoSolicitacao: alvo.codigoSolicitacao,
          }
        }
        aoFechar={() => setAlvo(null)}
      />
    </>
  );
}
