import type { ReactNode } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, CalendarCheck, ClipboardList, Eye, History, Phone, User } from 'lucide-react';

import { Button } from '@/shared/ui/Button';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { formatarInstante, formatarWallClock } from '@/shared/lib/datas';
import { PainelAnaliseRegras } from '@/shared/regulacao/analiseRegras/PainelAnaliseRegras';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { useSolicitacaoEsusSg } from '@/features/esussg/api/queries';
import { SituacaoEsusSgBadge } from '@/features/esussg/components/SituacaoEsusSgBadge';
import { PrioridadeEsusSg } from '@/features/esussg/components/PrioridadeEsusSg';
import { TrilhaEsusSg } from '@/features/esussg/components/TrilhaEsusSg';
import { dataHoraAgendada } from '@/features/esussg/lib/agendamento';
import { formatarCpf, limparTexto, simNao } from '@/features/esussg/lib/formatacao';
import { DICA_SITUACAO_ESUSSG, ROTULO_SITUACAO_ESUSSG } from '@/features/esussg/types';

function Linha({
  rotulo,
  valor,
  dica,
}: {
  rotulo: string;
  valor: ReactNode;
  /** Explicação curta embaixo do valor — para o campo que engana à primeira vista. */
  dica?: string;
}) {
  const vazio = valor == null || (typeof valor === 'string' && valor.trim() === '');
  return (
    <div>
      <dt className="text-xs text-slate-500">{rotulo}</dt>
      <dd className="text-sm">{vazio ? <span className="text-slate-400">—</span> : valor}</dd>
      {dica && !vazio && <p className="text-[11px] text-slate-400">{dica}</p>}
    </div>
  );
}

/**
 * Regulação → ESUS SG → pedido: tudo o que o espelho sabe de um pedido do município no ESUS de
 * São Gonçalo — paciente, pedido, agendamento, a trilha montada pela varredura e o parecer da
 * análise de regras.
 *
 * Sem ação de escrita (ADR-0063): a integração com o ESUS é só leitura. Nem FollowUP (o ESUS não
 * tem), nem edição de telefone.
 */
export function EsusSgSolicitacaoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navegar = useNavigate();
  const { data: detalhe, isLoading } = useSolicitacaoEsusSg(id);

  if (isLoading) return <div className="p-6 text-sm text-slate-500">Carregando…</div>;
  if (!detalhe || !id) return <div className="p-6 text-sm text-slate-500">Pedido não encontrado.</div>;

  const r = detalhe.resumo;
  const cnes = detalhe.cnesExecutora;
  const unidade = limparTexto(r.unidadeExecutora);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <Button variante="secundaria" onClick={() => navegar('/app/regulacao/esussg')}>
          <ArrowLeft className="size-4" /> Voltar
        </Button>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-1">
            <h1 className="text-xl font-semibold">
              Pedido <span className="font-mono">{r.idEsusSg}</span>{' '}
              <span className="text-sm font-normal text-slate-500">no ESUS São Gonçalo</span>
            </h1>
            <AjudaManual artigo="esus-sao-goncalo" secao="detalhe" />
          </div>
          <p className="text-sm text-slate-500">{r.recurso}</p>
        </div>
        <span
          className="rounded-full border border-slate-200 bg-slate-50 px-2 py-0.5 text-xs text-slate-500"
          title="A integração com o ESUS é só leitura: nada nesta tela escreve no ESUS."
        >
          só leitura
        </span>
        <SituacaoEsusSgBadge situacao={r.situacao} />
      </div>

      {r.situacao === 'SaiuDaFila' && (
        <p className="rounded bg-slate-100 p-3 text-sm text-slate-700">
          <strong>Saiu da fila.</strong> {DICA_SITUACAO_ESUSSG.SaiuDaFila} Pode ter sido excluído,
          cancelado ou transferido — o ESUS não mostra essa tela à nossa conta.
          {r.situacaoAnterior && ` Antes estava: ${ROTULO_SITUACAO_ESUSSG[r.situacaoAnterior]}.`}
        </p>
      )}

      <div className="grid gap-4 lg:grid-cols-3">
        <section className="rounded-lg border border-slate-200 bg-white p-4 lg:col-span-1">
          <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold">
            <User className="size-4 text-red-600" /> Paciente
          </h2>
          <dl className="space-y-2">
            <div>
              <dt className="text-xs text-slate-500">Nome</dt>
              <dd className="text-sm">
                {r.pacienteId ? (
                  <NomePacienteComResumo
                    pacienteId={r.pacienteId}
                    nome={r.pacienteNome}
                    mostrarWhatsApp
                    classNameNome="font-medium"
                  />
                ) : (
                  r.pacienteNome || <span className="text-slate-400">—</span>
                )}
              </dd>
              {r.pacienteId ? (
                <Link
                  to={`/app/pacientes/${r.pacienteId}`}
                  className="mt-1 inline-flex items-center gap-1 text-xs text-red-700 hover:underline"
                >
                  <Eye className="size-3.5" /> Abrir a ficha do paciente
                </Link>
              ) : (
                <p className="mt-1 text-[11px] text-slate-400">
                  Ainda não conciliado com um cadastro da nossa base.
                </p>
              )}
            </div>
            <Linha rotulo="CPF" valor={formatarCpf(r.cpf)} />
            <Linha rotulo="CNS" valor={r.cns} />
            <Linha rotulo="Nascimento" valor={r.dataNascimento ? formatarWallClock(r.dataNascimento) : null} />
            <Linha rotulo="Sexo" valor={detalhe.sexo} />
            <Linha rotulo="Nome da mãe" valor={detalhe.nomeMae} />
            <Linha
              rotulo="Bairro / município"
              valor={[detalhe.bairro, detalhe.municipioPaciente].filter(Boolean).join(' — ') || null}
            />
          </dl>

          <h3 className="mb-2 mt-4 flex items-center gap-2 text-sm font-semibold">
            <Phone className="size-4 text-red-600" /> Telefones no ESUS
          </h3>
          <dl className="space-y-2">
            <Linha rotulo="Telefone" valor={detalhe.telefone} />
            <Linha rotulo="Celular" valor={detalhe.celular} />
          </dl>
        </section>

        <section className="space-y-4 lg:col-span-2">
          <div className="rounded-lg border border-slate-200 bg-white p-4">
            <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold">
              <ClipboardList className="size-4 text-red-600" /> Pedido
            </h2>
            <dl className="grid gap-3 sm:grid-cols-3">
              <Linha rotulo="Procedimento" valor={r.recurso} />
              <Linha rotulo="Tipo" valor={r.tipo} />
              <Linha rotulo="Subprocedimentos" valor={detalhe.subprocedimentos} />
              <Linha
                rotulo="Código interno"
                valor={detalhe.codigoInterno ? <span className="font-mono">{detalhe.codigoInterno}</span> : null}
                dica="Código interno do ESUS, não é SIGTAP."
              />
              <Linha rotulo="Prioridade" valor={r.prioridade ? <PrioridadeEsusSg prioridade={r.prioridade} cor={r.prioridadeCor} /> : null} />
              <Linha rotulo="Pendência" valor={r.pendencia} />
              <Linha
                rotulo="Posição na fila"
                valor={r.posicaoFila != null ? `${r.posicaoFila}º` : null}
                dica="Posição regulada na fila do procedimento."
              />
              <Linha rotulo="Ordem de entrada" valor={detalhe.ordemEntrada != null ? String(detalhe.ordemEntrada) : null} />
              <Linha
                rotulo="Entrada na fila"
                valor={
                  r.dataEntradaFila
                    ? `${formatarWallClock(r.dataEntradaFila)}${r.diasNaFila != null ? ` · ${r.diasNaFila} dias` : ''}`
                    : null
                }
              />
              <Linha rotulo="Data do pedido" valor={r.dataSolicitacao ? formatarWallClock(r.dataSolicitacao) : null} />
              <Linha rotulo="Profissional solicitante" valor={detalhe.profissionalSolicitante} />
              <Linha rotulo="Unidade solicitante" valor={detalhe.unidadeSolicitante} />
              <Linha
                rotulo="Quem incluiu na fila"
                valor={detalhe.usuarioInclusao}
                dica="Servidor do município que incluiu o pedido no ESUS."
              />
              <Linha rotulo="Regulador" valor={detalhe.regulador} />
              <Linha rotulo="Sincronizado em" valor={formatarInstante(r.sincronizadoEm)} />
              <Linha rotulo="Visto na fila em" valor={detalhe.vistoNaFilaEm ? formatarInstante(detalhe.vistoNaFilaEm) : null} />
              <Linha
                rotulo="Visto nos agendados em"
                valor={detalhe.vistoNosAgendadosEm ? formatarInstante(detalhe.vistoNosAgendadosEm) : null}
              />
            </dl>
          </div>

          <div className="rounded-lg border border-slate-200 bg-white p-4">
            <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold">
              <CalendarCheck className="size-4 text-red-600" /> Agendamento
            </h2>
            {r.situacao !== 'Agendada' && !r.dataAgendada && !r.dataHoraAgendadaTexto ? (
              <p className="text-sm text-slate-500">
                Sem agendamento — o pedido {r.situacao === 'SaiuDaFila' ? 'saiu da fila sem data marcada visível' : 'ainda está na fila'}.
              </p>
            ) : (
              <dl className="grid gap-3 sm:grid-cols-3">
                <Linha rotulo="Data e hora" valor={dataHoraAgendada(r.dataHoraAgendadaTexto, r.dataAgendada)} />
                <Linha
                  rotulo="Unidade executora"
                  valor={unidade}
                  dica={cnes ? `CNES ${cnes}` : undefined}
                />
                <Linha rotulo="Setor" valor={detalhe.setor} />
                <Linha rotulo="Local" valor={detalhe.local} />
                <Linha rotulo="Quem agendou" valor={detalhe.usuarioAgendamento} />
                <Linha
                  rotulo="Agendamento cadastrado em"
                  valor={detalhe.agendamentoCadastradoEm ? formatarWallClock(detalhe.agendamentoCadastradoEm) : null}
                />
                <Linha rotulo="Saída da fila" valor={detalhe.dataSaidaFila ? formatarWallClock(detalhe.dataSaidaFila) : null} />
                <Linha rotulo="Comprovante impresso" valor={simNao(detalhe.comprovanteImpresso)} />
                <Linha rotulo="Agendado com TFD" valor={simNao(detalhe.agendadoTfd)} />
                <Linha rotulo="Notificação (canal)" valor={detalhe.notificacaoTipo} />
                <Linha rotulo="Notificação (entrega)" valor={detalhe.notificacaoEntrega} />
                <Linha
                  rotulo="Resposta do paciente"
                  valor={r.notificacaoResposta}
                  dica="Resposta ao aviso que o próprio ESUS enviou."
                />
              </dl>
            )}
          </div>

          <PainelAnaliseRegras sistema="esussg" espelhoId={id} analise={detalhe.analise} />

          <div className="rounded-lg border border-slate-200 bg-white p-4">
            <h2 className="mb-1 flex items-center gap-2 text-sm font-semibold">
              <History className="size-4 text-red-600" /> Trilha do pedido
              <span className="text-xs font-normal text-slate-500">
                ({detalhe.eventos.length} {detalhe.eventos.length === 1 ? 'marco' : 'marcos'})
              </span>
            </h2>
            <p className="mb-3 text-xs text-slate-500">
              O ESUS não mostra histórico por pedido à nossa conta: esta trilha é montada pela
              varredura, com os marcos que as listas trazem (inclusão na fila, agendamento) e as
              diferenças entre uma varredura e a seguinte (prioridade, pendência, remarcação, saída
              da fila). Os eventos “percebidos pela varredura” levam a data em que a varredura notou
              a mudança.
            </p>
            <TrilhaEsusSg eventos={detalhe.eventos} />
          </div>
        </section>
      </div>
    </div>
  );
}
