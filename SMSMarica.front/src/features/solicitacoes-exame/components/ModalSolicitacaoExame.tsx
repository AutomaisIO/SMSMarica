import type { ReactNode } from 'react';
import { Loader2 } from 'lucide-react';

import { useSolicitacaoPorId } from '@/features/solicitacoes-exame/api/queries';
import { StatusBadgeSolicitacao } from '@/features/solicitacoes-exame/components/StatusBadgeSolicitacao';
import { ConfirmacaoBadge } from '@/features/solicitacoes-exame/components/ConfirmacaoBadge';
import { Modal } from '@/shared/ui/Modal';
import { formatarInstante, formatarInstanteData } from '@/shared/lib/datas';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';

/**
 * Detalhe da solicitação de exame (SISREG/nossa), em modal — espelha o
 * {@link import('@/features/ser/components/ModalSolicitacaoSer').ModalSolicitacaoSer} para que a
 * aba de Agendamentos do paciente abra o caso sem sair da tela.
 *
 * É somente-leitura de propósito: as ações (autorizar, cancelar, trocar equipamento, editar)
 * vivem na página `/app/solicitacoes-exame/:id`, que continua sendo o lugar de operar.
 */
export function ModalSolicitacaoExame({
  solicitacaoId,
  aoFechar,
}: {
  solicitacaoId: string | null;
  aoFechar: () => void;
}) {
  const { data: s, isLoading } = useSolicitacaoPorId(solicitacaoId);

  return (
    <Modal
      aberto={solicitacaoId !== null}
      aoFechar={aoFechar}
      largura="lg"
      titulo={s ? `Solicitação ${s.codigoSolicitacao ?? s.accessionNumber}` : 'Solicitação'}
      descricao={s?.tipoExameNome ?? undefined}
    >
      {isLoading && (
        <div className="flex items-center gap-2 p-6 text-slate-500">
          <Loader2 className="size-4 animate-spin" /> carregando…
        </div>
      )}

      {s && (
        <div className="space-y-5 text-sm">
          <section className="flex flex-wrap items-center gap-3">
            <StatusBadgeSolicitacao status={s.status} />
            <ConfirmacaoBadge status={s.statusConfirmacao} />
            {s.prioridade && s.prioridade !== 'Eletiva' && (
              <span className="rounded bg-amber-100 px-2 py-0.5 text-xs text-amber-800">
                {s.prioridade}
              </span>
            )}
          </section>

          <Bloco titulo="Paciente">
            {s.pacienteId ? (
              <div className="flex items-center gap-2 pb-1">
                <span className="text-xs text-slate-500">Nome</span>
                <NomePacienteComResumo
                  pacienteId={s.pacienteId}
                  nome={s.pacienteNome}
                  classNameNome="text-sm font-medium text-slate-900"
                  mostrarWhatsApp
                />
              </div>
            ) : (
              <Item rotulo="Nome" valor={s.pacienteNome} />
            )}
            <Item rotulo="CPF" valor={s.pacienteCpf} />
            <Item rotulo="CNS" valor={s.pacienteCns} />
          </Bloco>

          <Bloco titulo="Exame">
            <Item rotulo="Procedimento" valor={s.tipoExameNome} />
            <Item rotulo="Modalidade" valor={s.modalidadeDicom} />
            <Item rotulo="Unidade executante" valor={s.unidadeNome} />
            <Item rotulo="Unidade solicitante" valor={s.unidadeSolicitanteNome} />
            <Item rotulo="Solicitante" valor={s.solicitanteNome} />
            <Item rotulo="Equipamento" valor={s.equipamentoNome} />
            <Item rotulo="Nº SISREG" valor={s.codigoSolicitacao} />
            <Item rotulo="Accession" valor={s.accessionNumber} />
            <Item rotulo="Justificativa" valor={s.justificativa} />
            <Item rotulo="Observações" valor={s.observacoes} />
          </Bloco>

          <Bloco titulo="Datas">
            <Item rotulo="Solicitação" valor={formatarInstanteData(s.dataSolicitacao)} />
            <Item rotulo="Regulação" valor={formatarInstanteData(s.dataRegulacao)} />
            <Item rotulo="Agendada" valor={formatarInstante(s.dataAgendada)} />
            <Item
              rotulo="Comparecimento"
              valor={
                s.autorizadoEm
                  ? `${formatarInstante(s.autorizadoEm)}${s.autorizadoPorNome ? ` · ${s.autorizadoPorNome}` : ''}`
                  : null
              }
            />
            <Item rotulo="Realizado" valor={formatarInstante(s.realizadoEm)} />
            {s.canceladoEm && (
              <Item
                rotulo="Cancelado"
                valor={`${formatarInstante(s.canceladoEm)}${s.motivoCancelamento ? ` · ${s.motivoCancelamento}` : ''}`}
              />
            )}
          </Bloco>
        </div>
      )}
    </Modal>
  );
}

function Bloco({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <section>
      <h3 className="mb-2 font-semibold text-slate-800">{titulo}</h3>
      <dl className="grid grid-cols-2 gap-x-4 gap-y-1">{children}</dl>
    </section>
  );
}

function Item({ rotulo, valor }: { rotulo: string; valor?: string | null }) {
  if (!valor) return null;
  return (
    <div className="flex gap-2">
      <dt className="shrink-0 text-slate-500">{rotulo}:</dt>
      <dd className="min-w-0 break-words text-slate-800">{valor}</dd>
    </div>
  );
}
