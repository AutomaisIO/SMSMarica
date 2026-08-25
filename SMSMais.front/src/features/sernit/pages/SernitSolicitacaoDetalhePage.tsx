import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, History, Phone, User } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { useSolicitacaoSernit } from '@/features/sernit/api/queries';
import { SituacaoSernitBadge } from '@/features/sernit/components/SituacaoSernitBadge';
import type { EventoSernit } from '@/features/sernit/types';

function dataHora(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('pt-BR');
}

function data(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('pt-BR');
}

/**
 * Cor do marcador do evento. FollowUP fica destacado de propósito: é tentativa de contato com
 * o paciente, o dado que a regulação mais procura na trilha — e o único que não muda a situação.
 */
function corDoEvento(evento: string): string {
  const e = evento.toLowerCase();
  if (e.includes('followup') || e.includes('follow-up')) return 'bg-purple-500';
  if (e.includes('cancel')) return 'bg-red-500';
  if (e.includes('pendenc')) return 'bg-orange-500';
  if (e.includes('solicit')) return 'bg-blue-500';
  return 'bg-slate-400';
}

function Linha({ rotulo, valor }: { rotulo: string; valor: string | null | undefined }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{rotulo}</dt>
      <dd className="text-sm">{valor && valor.trim() !== '' ? valor : '—'}</dd>
    </div>
  );
}

function EventoItem({ evento }: { evento: EventoSernit }) {
  return (
    <li className="relative pl-6">
      <span
        className={`absolute left-0 top-1.5 size-3 rounded-full ring-4 ring-white ${corDoEvento(evento.evento)}`}
      />
      <div className="rounded-lg border border-slate-200 bg-white p-3">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
          <span className="text-sm font-semibold">{evento.evento}</span>
          <span className="text-xs text-slate-500">{dataHora(evento.dataEvento)}</span>
          {evento.estadoAnterior && evento.estadoAtual && (
            <span className="text-xs text-slate-500">
              {evento.estadoAnterior} → {evento.estadoAtual}
            </span>
          )}
        </div>

        <div className="mt-1 flex flex-wrap gap-x-4 text-xs text-slate-500">
          {evento.usuario && <span>por {evento.usuario}</span>}
          {evento.centralRegulacao && <span>{evento.centralRegulacao}</span>}
          {evento.lotacaoEvento && <span>{evento.lotacaoEvento}</span>}
          {evento.ip && <span className="font-mono">IP {evento.ip}</span>}
        </div>

        {evento.observacao && (
          <p className="mt-2 whitespace-pre-wrap rounded bg-slate-50 p-2 text-sm text-slate-700">
            {evento.observacao}
          </p>
        )}
      </div>
    </li>
  );
}

export function SernitSolicitacaoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navegar = useNavigate();
  const { data: detalhe, isLoading } = useSolicitacaoSernit(id);

  if (isLoading) return <div className="p-6 text-sm text-slate-500">Carregando…</div>;
  if (!detalhe) return <div className="p-6 text-sm text-slate-500">Solicitação não encontrada.</div>;

  const r = detalhe.resumo;
  const endereco = [
    [r ? detalhe.tipoLogradouro : null, detalhe.logradouro].filter(Boolean).join(' '),
    detalhe.numero,
    detalhe.complemento,
    detalhe.bairro,
    [detalhe.municipioPaciente, detalhe.uf].filter(Boolean).join('/'),
    detalhe.cep,
  ]
    .filter((p) => p && p.trim() !== '')
    .join(', ');

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <Button variante="secundaria" onClick={() => navegar('/app/regulacao/sernit')}>
          <ArrowLeft className="size-4" /> Voltar
        </Button>
        <div className="flex-1">
          <h1 className="text-xl font-semibold">
            Solicitação <span className="font-mono">{r.idSernit}</span>
          </h1>
          <p className="text-sm text-slate-500">{r.recurso}</p>
        </div>
        <SituacaoSernitBadge situacao={r.situacao} />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <section className="rounded-lg border border-slate-200 bg-white p-4 lg:col-span-1">
          <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold">
            <User className="size-4 text-red-600" /> Paciente
          </h2>
          <dl className="space-y-2">
            <Linha rotulo="Nome" valor={r.pacienteNome} />
            <Linha rotulo="Idade" valor={r.idadeTexto} />
            <Linha rotulo="Nascimento" valor={data(detalhe.dataNascimento)} />
            <Linha rotulo="Sexo" valor={detalhe.sexo} />
            <Linha rotulo="Nome da mãe" valor={detalhe.nomeMae} />
            <Linha rotulo="CPF" valor={r.cpf} />
            <Linha rotulo="CNS" valor={r.cns} />
            <Linha rotulo="Endereço" valor={endereco} />
          </dl>

          <h3 className="mb-2 mt-4 flex items-center gap-2 text-sm font-semibold">
            <Phone className="size-4 text-red-600" /> Contatos
          </h3>
          <dl className="space-y-2">
            <Linha rotulo="Residencial" valor={detalhe.telefoneResidencial} />
            <Linha rotulo="WhatsApp" valor={detalhe.telefoneWhatsapp} />
            <Linha rotulo="Contato" valor={detalhe.telefoneContato} />
          </dl>
        </section>

        <section className="space-y-4 lg:col-span-2">
          <div className="rounded-lg border border-slate-200 bg-white p-4">
            <h2 className="mb-3 text-sm font-semibold">Solicitação</h2>
            <dl className="grid gap-3 sm:grid-cols-3">
              <Linha rotulo="Tipo" valor={r.tipo} />
              <Linha rotulo="Solicitado em" valor={data(r.dataSolicitacao)} />
              <Linha
                rotulo="Espera"
                valor={r.diasNaFila != null ? `${r.diasNaFila} dias` : null}
              />
              <Linha rotulo="CID" valor={r.cid} />
              <Linha rotulo="Solicitante" valor={r.solicitanteNome} />
              <Linha rotulo="Município solicitante" valor={r.municipioSolicitante} />
              <Linha rotulo="Agendado para" valor={r.agendadoParaTexto} />
              <Linha rotulo="Sincronizado em" valor={dataHora(r.sincronizadoEm)} />
              <Linha rotulo="Histórico lido em" valor={dataHora(r.historicoLidoEm)} />
            </dl>
          </div>

          <div className="rounded-lg border border-slate-200 bg-white p-4">
            <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold">
              <History className="size-4 text-red-600" /> Histórico da solicitação
              <span className="text-xs font-normal text-slate-500">
                ({detalhe.eventos.length} eventos)
              </span>
            </h2>

            {r.historicoIndisponivel ? (
              // Vale explicar em vez de mostrar vazio: em Alta o menu do SERNIT não oferece o item,
              // e como Alta é estado terminal, a trilha nunca mais ficará legível lá.
              <p className="rounded bg-amber-50 p-3 text-sm text-amber-800">
                O SERNIT não oferece o histórico desta solicitação — solicitações em Alta não têm o
                item no menu de opções. O que aparece aqui é o que foi capturado antes da alta.
              </p>
            ) : detalhe.eventos.length === 0 ? (
              <p className="text-sm text-slate-500">
                Histórico ainda não lido pelo motor de varredura.
              </p>
            ) : (
              <ol className="relative space-y-3 border-l border-slate-200 pl-2">
                {detalhe.eventos.map((e) => (
                  <EventoItem key={e.id} evento={e} />
                ))}
              </ol>
            )}
          </div>
        </section>
      </div>
    </div>
  );
}
