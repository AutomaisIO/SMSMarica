import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ExternalLink, Loader2 } from 'lucide-react';

import { Modal } from '@/shared/ui/Modal';
import { formatarInstante, formatarWallClock } from '@/shared/lib/datas';
import { PainelAnaliseRegras } from '@/shared/regulacao/analiseRegras/PainelAnaliseRegras';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { useSolicitacaoEsusSg } from '@/features/esussg/api/queries';
import { SituacaoEsusSgBadge } from '@/features/esussg/components/SituacaoEsusSgBadge';
import { TrilhaEsusSg } from '@/features/esussg/components/TrilhaEsusSg';
import { dataHoraAgendada } from '@/features/esussg/lib/agendamento';
import { formatarCpf, limparTexto } from '@/features/esussg/lib/formatacao';
import { ROTULO_SITUACAO_ESUSSG } from '@/features/esussg/types';

/**
 * Detalhe do pedido do ESUS São Gonçalo, em modal — para as Notificações e para a ficha do
 * paciente, onde navegar para outra página custaria o filtro e a posição de leitura.
 *
 * Só leitura, como a integração inteira (ADR-0063).
 */
export function ModalSolicitacaoEsusSg({
  solicitacaoId,
  aoFechar,
}: {
  solicitacaoId: string | null;
  aoFechar: () => void;
}) {
  const { data, isLoading } = useSolicitacaoEsusSg(solicitacaoId ?? undefined);
  const r = data?.resumo;

  return (
    <Modal
      aberto={solicitacaoId !== null}
      aoFechar={aoFechar}
      largura="lg"
      titulo={r ? `Pedido ${r.idEsusSg} — ESUS São Gonçalo` : 'Pedido — ESUS São Gonçalo'}
      descricao={r?.recurso ?? undefined}
    >
      {isLoading && (
        <div className="flex items-center gap-2 p-6 text-slate-500">
          <Loader2 className="size-4 animate-spin" /> carregando…
        </div>
      )}

      {data && r && solicitacaoId && (
        <div className="space-y-5 text-sm">
          <section className="flex flex-wrap items-center gap-3">
            <SituacaoEsusSgBadge situacao={r.situacao} />
            {r.situacaoAnterior && (
              <span className="text-xs text-slate-500">
                antes: {ROTULO_SITUACAO_ESUSSG[r.situacaoAnterior]}
                {r.situacaoMudouEm && ` · mudou em ${formatarInstante(r.situacaoMudouEm)}`}
              </span>
            )}
            {r.diasNaFila != null && (
              <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-700">
                {r.diasNaFila} dia(s) na fila
              </span>
            )}
            <Link
              to={`/app/regulacao/esussg/${solicitacaoId}`}
              className="ml-auto inline-flex items-center gap-1 text-xs text-red-700 hover:underline"
            >
              <ExternalLink className="size-3.5" /> Abrir a página do pedido
            </Link>
          </section>

          <Bloco titulo="Paciente">
            {r.pacienteId ? (
              <div className="flex items-center gap-2 pb-1">
                <span className="text-xs text-slate-500">Nome</span>
                <NomePacienteComResumo
                  pacienteId={r.pacienteId}
                  nome={r.pacienteNome}
                  classNameNome="text-sm font-medium text-slate-900"
                  mostrarWhatsApp
                />
              </div>
            ) : (
              <Item rotulo="Nome" valor={r.pacienteNome} />
            )}
            <Item rotulo="CPF" valor={formatarCpf(r.cpf)} />
            <Item rotulo="CNS" valor={r.cns} />
            <Item rotulo="Nascimento" valor={r.dataNascimento ? formatarWallClock(r.dataNascimento) : null} />
            <Item rotulo="Telefone" valor={data.telefone} />
            <Item rotulo="Celular" valor={data.celular} />
          </Bloco>

          <Bloco titulo="Pedido">
            <Item rotulo="Procedimento" valor={r.recurso} />
            <Item rotulo="Prioridade" valor={r.prioridade} />
            <Item rotulo="Posição na fila" valor={r.posicaoFila != null ? `${r.posicaoFila}º` : null} />
            <Item rotulo="Entrada na fila" valor={r.dataEntradaFila ? formatarWallClock(r.dataEntradaFila) : null} />
            <Item rotulo="Quem incluiu" valor={data.usuarioInclusao} />
            <Item rotulo="Pendência" valor={r.pendencia} />
          </Bloco>

          {(r.dataAgendada || r.dataHoraAgendadaTexto) && (
            <Bloco titulo="Agendamento">
              <Item rotulo="Data e hora" valor={dataHoraAgendada(r.dataHoraAgendadaTexto, r.dataAgendada)} />
              <Item rotulo="Unidade" valor={limparTexto(r.unidadeExecutora)} />
              <Item rotulo="Setor" valor={data.setor} />
              <Item rotulo="Quem agendou" valor={data.usuarioAgendamento} />
              <Item rotulo="Resposta do paciente" valor={r.notificacaoResposta} />
            </Bloco>
          )}

          <PainelAnaliseRegras sistema="esussg" espelhoId={solicitacaoId} analise={data.analise} />

          <section>
            <h3 className="mb-1 font-semibold text-slate-800">
              Trilha do pedido{' '}
              <span className="text-xs font-normal text-slate-500">
                ({data.eventos.length} {data.eventos.length === 1 ? 'marco' : 'marcos'})
              </span>
            </h3>
            <p className="mb-2 text-xs text-slate-500">
              Montada pela varredura — o ESUS não mostra histórico por pedido à nossa conta.
            </p>
            <TrilhaEsusSg eventos={data.eventos} compacta />
          </section>
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
