import { Loader2 } from 'lucide-react';

import { useSolicitacaoSer } from '@/features/ser/api/queries';
import { SituacaoSerBadge } from '@/features/ser/components/SituacaoSerBadge';
import { Modal } from '@/shared/ui/Modal';
import { formatarInstante, formatarInstanteData } from '@/shared/lib/datas';

/**
 * Detalhe completo da solicitação do SER, em modal.
 *
 * Existe para a tela de Notificações: quem está triando movimentações precisa ver o caso inteiro
 * — dados do paciente, contatos e a trilha de eventos — sem sair da fila e perder o filtro e a
 * posição da leitura. Navegar para outra página, ali, custava o contexto todo.
 */
export function ModalSolicitacaoSer({
  solicitacaoId,
  aoFechar,
}: {
  solicitacaoId: string | null;
  aoFechar: () => void;
}) {
  const { data, isLoading } = useSolicitacaoSer(solicitacaoId ?? undefined);
  const r = data?.resumo;

  return (
    <Modal
      aberto={solicitacaoId !== null}
      aoFechar={aoFechar}
      largura="lg"
      titulo={r ? `Solicitação ${r.idSer}` : 'Solicitação'}
      descricao={r?.recurso ?? undefined}
    >
      {isLoading && (
        <div className="flex items-center gap-2 p-6 text-slate-500">
          <Loader2 className="size-4 animate-spin" /> carregando…
        </div>
      )}

      {data && r && (
        <div className="space-y-5 text-sm">
          <section className="flex flex-wrap items-center gap-3">
            <SituacaoSerBadge situacao={r.situacao} />
            {r.situacaoAnterior && (
              <span className="text-xs text-slate-500">
                antes: {r.situacaoAnterior}
                {r.situacaoMudouEm && ` · mudou em ${formatarInstante(r.situacaoMudouEm)}`}
              </span>
            )}
            {r.diasNaFila != null && (
              <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-700">
                {r.diasNaFila} dia(s) na fila
              </span>
            )}
          </section>

          <Bloco titulo="Paciente">
            <Item rotulo="Nome" valor={r.pacienteNome} />
            <Item rotulo="Idade" valor={r.idadeTexto} />
            <Item rotulo="Nascimento" valor={formatarInstanteData(data.dataNascimento)} />
            <Item rotulo="Sexo" valor={data.sexo} />
            <Item rotulo="CPF" valor={r.cpf} />
            <Item rotulo="CNS" valor={r.cns} />
            <Item rotulo="Nome da mãe" valor={data.nomeMae} />
            <Item rotulo="Município" valor={data.municipioPaciente} />
          </Bloco>

          {/* Os telefones são o motivo prático de abrir o detalhe: é por eles que a regulação
              corre atrás do paciente quando a movimentação exige contato. */}
          <Bloco titulo="Contato">
            <Item rotulo="WhatsApp" valor={data.telefoneWhatsapp} />
            <Item rotulo="Celular/contato" valor={data.telefoneContato} />
            <Item rotulo="Residencial" valor={data.telefoneResidencial} />
            <Item
              rotulo="Endereço"
              valor={[data.tipoLogradouro, data.logradouro, data.numero, data.bairro]
                .filter(Boolean)
                .join(' ')}
            />
          </Bloco>

          <Bloco titulo="Solicitação">
            <Item rotulo="Recurso" valor={r.recurso} />
            <Item rotulo="Tipo" valor={r.tipo} />
            <Item rotulo="CID" valor={r.cid} />
            <Item rotulo="Data da solicitação" valor={formatarInstanteData(r.dataSolicitacao)} />
            <Item rotulo="Agendado para" valor={r.agendadoParaTexto} />
            <Item rotulo="Solicitante" valor={r.solicitanteNome} />
          </Bloco>

          <section>
            <h3 className="mb-2 font-semibold text-slate-800">
              Histórico no SER{' '}
              <span className="text-xs font-normal text-slate-500">
                ({data.eventos.length} evento{data.eventos.length === 1 ? '' : 's'})
              </span>
            </h3>

            {data.eventos.length === 0 && (
              // Distinguir "não tem" de "não lemos ainda" evita a conclusão errada de que a
              // solicitação nunca se moveu.
              <p className="text-xs text-slate-500">
                {r.historicoIndisponivel
                  ? 'O SER não oferece histórico para esta situação (Alta).'
                  : r.historicoLidoEm
                    ? 'Nenhum evento registrado.'
                    : 'Histórico ainda não lido pelo motor de varredura.'}
              </p>
            )}

            <ol className="space-y-2">
              {data.eventos.map((e) => (
                <li key={e.id} className="border-l-2 border-slate-200 pl-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium text-slate-800">{e.evento}</span>
                    <span className="text-xs text-slate-500">{formatarInstante(e.dataEvento)}</span>
                    {e.estadoAnterior && e.estadoAtual && (
                      <span className="text-xs text-slate-500">
                        {e.estadoAnterior} → {e.estadoAtual}
                      </span>
                    )}
                  </div>
                  {e.observacao && <p className="text-xs text-slate-600">{e.observacao}</p>}
                  <p className="text-[11px] text-slate-400">
                    {[e.usuario, e.lotacaoEvento, e.unidadeExecutora].filter(Boolean).join(' · ')}
                  </p>
                </li>
              ))}
            </ol>
          </section>
        </div>
      )}
    </Modal>
  );
}

function Bloco({ titulo, children }: { titulo: string; children: React.ReactNode }) {
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
