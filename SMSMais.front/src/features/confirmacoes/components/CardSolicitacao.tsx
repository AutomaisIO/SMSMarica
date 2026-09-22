import { useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertTriangle, ArrowRightLeft, Check, CheckCheck, Clock, Hand, MessagesSquare, PhoneOff, Undo2, UserRound, X } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { Button } from '@/shared/ui/Button';
import { TelefoneCopiavel } from '@/shared/ui/TelefoneCopiavel';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { CLASSE_RESPOSTA, ROTULO_RESPOSTA, ROTULO_STATUS, dataHora, hora, rotuloCanal } from '@/features/mensageria/lib/rotulos';
import { useConversaContexto } from '@/features/confirmacoes/api';
import type { AbaAtendimento, EnvioConfirmacao, SolicitacaoAtendimento } from '@/features/confirmacoes/types';

export type AcaoCard =
  | 'atender'
  | 'assumir'
  | 'liberar'
  | 'confirmar'
  | 'cancelar'
  | 'pendente'
  | 'contato-errado'
  | 'transferir'
  | 'contato-corrigido'
  | 'desfazer-pedido';

type Props = {
  item: SolicitacaoAtendimento;
  aba: AbaAtendimento;
  podeEditar: boolean;
  podeCancelar: boolean;
  ocupado: boolean;
  aoAcao: (acao: AcaoCard, item: SolicitacaoAtendimento) => void;
};

/** Situação do envio automático com o "check" do zap: ✓ enviada, ✓✓ entregue, ✓✓ azul lida, ⚠ falha. */
function BadgeEnvio({ envio }: { envio: EnvioConfirmacao | null }) {
  if (!envio) {
    return <span className="badge badge-gray" title="Nenhuma mensagem automática foi gerada para este agendamento.">Não enviada</span>;
  }
  const rotulo = ROTULO_STATUS[envio.status] ?? envio.status;
  const dica = [
    envio.enviadoEm ? `Enviada ${dataHora(envio.enviadoEm)}` : null,
    envio.entregueEm ? `Entregue ${dataHora(envio.entregueEm)}` : null,
    envio.lidoEm ? `Lida ${dataHora(envio.lidoEm)}` : null,
    envio.visualizadoEm ? `Abriu o link ${dataHora(envio.visualizadoEm)}` : null,
    envio.proximaTentativaEm && envio.status === 'Pendente' ? `Sai a partir de ${dataHora(envio.proximaTentativaEm)}` : null,
    envio.motivoFalha ?? envio.erroMeta,
  ].filter(Boolean).join(' · ');

  switch (envio.status) {
    case 'Enviada':
      return <span className="badge badge-info inline-flex items-center gap-1" title={dica}><Check className="h-3 w-3" /> Enviada</span>;
    case 'Entregue':
      return <span className="badge badge-info inline-flex items-center gap-1" title={dica}><CheckCheck className="h-3 w-3" /> Entregue</span>;
    case 'Lida':
      return <span className="badge badge-success inline-flex items-center gap-1" title={dica}><CheckCheck className="h-3 w-3 text-sky-600" /> Lida</span>;
    case 'Falha':
      return <span className="badge badge-danger inline-flex items-center gap-1" title={dica}><AlertTriangle className="h-3 w-3" /> Falhou</span>;
    case 'Pendente':
      return <span className="badge badge-gray inline-flex items-center gap-1" title={dica}><Clock className="h-3 w-3" /> Na fila</span>;
    case 'AguardandoCorrecaoContato':
      return <span className="badge badge-danger" title={dica}>Número negado</span>;
    case 'AguardandoVerificacaoCadastral':
      return <span className="badge badge-warning" title={dica}>Aguardando identificação</span>;
    case 'SubstituidaPorAtendente':
      return <span className="badge badge-gray" title={dica}>Atendida por pessoa</span>;
    default:
      return <span className="badge badge-warning" title={dica}>{rotulo}</span>;
  }
}

export function CardSolicitacao({ item, aba, podeEditar, podeCancelar, ocupado, aoAcao }: Props) {
  const a = item.atendimento;
  const emAtendimentoPorOutro = a?.situacao === 'EmAtendimento' && !a.ehMeu;
  const meuEmAtendimento = a?.situacao === 'EmAtendimento' && a.ehMeu;
  const estacionada = a?.situacao === 'Pendente' || a?.situacao === 'ContatoErrado';
  // "Confirmar" direto do card, sem passar antes por Atender/Retomar: o endpoint de confirmar já
  // auto-assume a ficha (409 só se OUTRA pessoa estiver ativamente em atendimento). Some quando já
  // está confirmado, quando é o meu em atendimento (o bloco abaixo já tem o botão) e quando está
  // preso por outra pessoa (aí o caminho é "Assumir atendimento"). Ticket #133.
  const abaConfirmaRapido = aba === 'Pendentes' || aba === 'NaoConfirmados' || aba === 'ContatoErrado';
  const podeConfirmarRapido =
    abaConfirmaRapido &&
    item.statusConfirmacao !== 'Confirmada' &&
    !meuEmAtendimento &&
    !emAtendimentoPorOutro;
  const rotaSolicitacao = item.exameId ? `/app/solicitacoes-exame/${item.exameId}` : `/app/consultas/${item.solicitacaoId}`;
  const dataAgendada = item.dataAgendada ? new Date(item.dataAgendada) : null;
  const ehHoje = dataAgendada ? dataAgendada.toDateString() === new Date().toDateString() : false;

  return (
    <article
      className={cn(
        'rounded-lg border bg-white p-3 shadow-sm transition-opacity',
        emAtendimentoPorOutro ? 'border-gray-200 opacity-60' : 'border-gray-200',
        meuEmAtendimento ? 'border-red-300 ring-1 ring-red-100' : null,
      )}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        {/* Paciente + contato */}
        <div className="min-w-[220px] flex-1 space-y-1">
          <div className="flex items-center gap-1">
            <NomePacienteComResumo
              pacienteId={item.pacienteId}
              nome={item.pacienteNome ?? '(sem nome)'}
              classNameNome="font-medium text-gray-900"
            />
          </div>
          <div className="flex flex-wrap items-center gap-2 text-xs text-gray-600">
            {item.pacienteCpf ? <span>CPF {item.pacienteCpf}</span> : null}
            <TelefoneCopiavel numero={item.telefone} />
            {item.telefoneVerificado ? <span className="text-emerald-700" title="Contato verificado">✔ verificado</span> : null}
            {item.contatoNegado ? <span className="text-red-700" title="Quem atende disse que não é o paciente">❗ número negado</span> : null}
            {item.motivoTelefoneComprometido ? (
              <span
                className="rounded bg-amber-50 px-1.5 py-0.5 text-amber-900"
                title={
                  item.motivoTelefoneComprometido === 'SemCelular'
                    ? 'O cadastro não tem celular válido — não há número para avisar. Pegue o número com o paciente.'
                    : 'A Meta recusou a entrega dizendo que o número não está no WhatsApp. O número pode existir e atender ligação.'
                }
              >
                {item.motivoTelefoneComprometido === 'SemCelular' ? 'sem celular' : 'não é WhatsApp'}
                {item.tentativasPerdidas > 1 ? ` · ${item.tentativasPerdidas} tentativas perdidas` : ''}
              </span>
            ) : null}
            {item.janelaZapAberta ? (
              <span className="rounded bg-emerald-50 px-1.5 py-0.5 text-emerald-800" title="O paciente escreveu no zap nas últimas 24h — o botão do WhatsApp abre a conversa direto.">
                respondeu no zap
              </span>
            ) : null}
          </div>
        </div>

        {/* Agendamento */}
        <div className="min-w-[240px] flex-1 space-y-0.5 text-sm">
          <div className={cn('font-semibold tabular-nums', ehHoje ? 'text-red-700' : 'text-gray-900')}>
            {dataHora(item.dataAgendada)}{ehHoje ? ' · hoje' : ''}
          </div>
          <div className="text-gray-800">
            <Link to={rotaSolicitacao} className="hover:underline">
              {item.procedimento ?? (item.categoria === 'Consulta' ? 'Consulta' : 'Exame')}
            </Link>
          </div>
          <div className="text-xs text-gray-600">{item.unidadeExecutante ?? '—'}{item.codigoSolicitacao ? ` · SISREG ${item.codigoSolicitacao}` : ''}</div>
        </div>

        {/* Situação */}
        <div className="flex min-w-[200px] flex-col items-start gap-1.5 text-xs">
          <div className="flex flex-wrap items-center gap-1.5">
            <BadgeEnvio envio={item.envio} />
            <span className={`badge ${CLASSE_RESPOSTA[item.statusConfirmacao]}`} title={item.respondidoEm ? `${dataHora(item.respondidoEm)} · ${rotuloCanal(item.confirmadoCanal)}` : undefined}>
              {ROTULO_RESPOSTA[item.statusConfirmacao]}
            </span>
          </div>
          {a ? (
            <div className={cn('inline-flex items-center gap-1 rounded px-1.5 py-0.5', a.ehMeu ? 'bg-red-50 text-red-800' : 'bg-gray-100 text-gray-700')}>
              <UserRound className="h-3 w-3" />
              {a.situacao === 'EmAtendimento'
                ? `${a.ehMeu ? 'Você está atendendo' : `Em atendimento por ${a.atendenteNome}`} desde ${hora(a.iniciadoEm)}`
                : a.situacao === 'Pendente'
                  ? `Pendente (${a.atendenteNome}): ${a.motivo ?? 'sem motivo'}`
                  : a.situacao === 'ContatoErrado'
                    ? `Contato errado (${a.atendenteNome})${a.motivo ? `: ${a.motivo}` : ''}`
                    : a.situacao}
            </div>
          ) : null}
        </div>
      </div>

      {aba === 'Cancelamento' ? (
        <PedidoDeCancelamento item={item} />
      ) : null}

      {/* Ações */}
      {podeEditar ? (
        <div className="mt-3 flex flex-wrap items-center gap-1.5 border-t border-gray-100 pt-2">
          {!a || a.situacao === 'Liberado' || a.situacao === 'ContatoCorrigido' ? (
            <Button tamanho="sm" disabled={ocupado} onClick={() => aoAcao('atender', item)}>
              <Hand className="mr-1 h-3.5 w-3.5" /> Atender
            </Button>
          ) : null}

          {emAtendimentoPorOutro ? (
            <Button tamanho="sm" variante="outline" disabled={ocupado} onClick={() => aoAcao('assumir', item)}>
              <ArrowRightLeft className="mr-1 h-3.5 w-3.5" /> Assumir atendimento
            </Button>
          ) : null}

          {estacionada ? (
            <Button tamanho="sm" variante="outline" disabled={ocupado} onClick={() => aoAcao('atender', item)}>
              <Undo2 className="mr-1 h-3.5 w-3.5" /> Retomar
            </Button>
          ) : null}

          {podeConfirmarRapido ? (
            <Button tamanho="sm" disabled={ocupado} onClick={() => aoAcao('confirmar', item)}>
              <Check className="mr-1 h-3.5 w-3.5" /> Confirmar
            </Button>
          ) : null}

          {meuEmAtendimento ? (
            <>
              {item.statusConfirmacao !== 'Confirmada' ? (
                <Button tamanho="sm" disabled={ocupado} onClick={() => aoAcao('confirmar', item)}>
                  <Check className="mr-1 h-3.5 w-3.5" /> Confirmar
                </Button>
              ) : null}
              <Button tamanho="sm" variante="outline" disabled={ocupado} onClick={() => aoAcao('pendente', item)}>
                <Clock className="mr-1 h-3.5 w-3.5" /> Enviar para pendente
              </Button>
              <Button tamanho="sm" variante="outline" disabled={ocupado} onClick={() => aoAcao('contato-errado', item)}>
                <PhoneOff className="mr-1 h-3.5 w-3.5" /> Contato errado
              </Button>
              <Button tamanho="sm" variante="ghost" disabled={ocupado} onClick={() => aoAcao('transferir', item)}>
                <ArrowRightLeft className="mr-1 h-3.5 w-3.5" /> Transferir
              </Button>
              <Button tamanho="sm" variante="ghost" disabled={ocupado} onClick={() => aoAcao('liberar', item)}>
                Liberar
              </Button>
            </>
          ) : null}

          {aba === 'Cancelamento' ? (
            <Button
              tamanho="sm"
              variante="outline"
              disabled={ocupado}
              onClick={() => aoAcao('desfazer-pedido', item)}
              title="A pessoa não pediu para cancelar — devolve a ficha para a fila de confirmação."
            >
              <Undo2 className="mr-1 h-3.5 w-3.5" /> Não era cancelamento
            </Button>
          ) : null}

          {aba === 'ContatoErrado' && item.pacienteCpf ? (
            <Button tamanho="sm" variante="outline" disabled={ocupado} onClick={() => aoAcao('contato-corrigido', item)}>
              Corrigir contato
            </Button>
          ) : null}

          {podeCancelar && (meuEmAtendimento || aba === 'Confirmados' || aba === 'Cancelamento' || (!a && aba === 'NaoConfirmados')) ? (
            <Button tamanho="sm" variante="danger" className="ml-auto" disabled={ocupado} onClick={() => aoAcao('cancelar', item)}>
              <X className="mr-1 h-3.5 w-3.5" /> Cancelar agendamento
            </Button>
          ) : null}
        </div>
      ) : null}
    </article>
  );
}

/**
 * O pedido de cancelamento com o que a atendente precisa para decidir: quem pediu, por onde,
 * quando, com que palavras — e a conversa em volta, sob demanda.
 *
 * A conversa não vem aberta de propósito: são dezenas de mensagens por card e a maioria dos
 * pedidos é óbvia pelo motivo. Quem precisa do contexto é justamente o caso duvidoso, e aí um
 * clique é barato perto de cancelar o exame de quem queria ir.
 */
function PedidoDeCancelamento({ item }: { item: SolicitacaoAtendimento }) {
  const [aberta, setAberta] = useState(false);
  const conversa = useConversaContexto(aberta ? item.solicitacaoId : null);

  return (
    <div className="mt-3 rounded-md border border-amber-200 bg-amber-50/60 px-3 py-2 text-sm">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="font-medium text-amber-900">Pediu para cancelar</span>
        <span className="text-xs text-amber-800">
          {rotuloCanal(item.confirmadoCanal)}
          {item.respondidoEm ? ` · ${dataHora(item.respondidoEm)}` : ''}
        </span>
      </div>
      <p className="mt-1 text-gray-800">
        {item.motivoCancelamentoPaciente
          ? <>Motivo: <q>{item.motivoCancelamentoPaciente}</q></>
          : <span className="text-gray-500">A pessoa não escreveu o motivo — leia a conversa antes de decidir.</span>}
      </p>

      <button
        type="button"
        className="mt-1.5 inline-flex items-center gap-1 text-xs text-amber-900 underline"
        onClick={() => setAberta((v) => !v)}
      >
        <MessagesSquare className="h-3.5 w-3.5" />
        {aberta ? 'ocultar conversa' : 'ver a conversa'}
      </button>

      {aberta ? (
        conversa.isLoading ? (
          <p className="mt-2 text-xs text-gray-500">Carregando a conversa…</p>
        ) : (conversa.data?.length ?? 0) === 0 ? (
          <p className="mt-2 text-xs text-gray-500">Nenhuma mensagem registrada com este contato.</p>
        ) : (
          <div className="mt-2 max-h-72 space-y-1 overflow-y-auto rounded border border-amber-200 bg-white p-2">
            {conversa.data!.map((m, i) => (
              <div key={i} className={cn('flex', m.doPaciente ? 'justify-start' : 'justify-end')}>
                <div
                  className={cn(
                    'max-w-[80%] whitespace-pre-wrap break-words rounded px-2 py-1 text-xs',
                    m.doPaciente ? 'bg-gray-100 text-gray-900' : 'bg-emerald-50 text-emerald-900',
                  )}
                  title={`${dataHora(m.ocorridoEm)}${m.autor ? ` · ${m.autor}` : ''}`}
                >
                  {m.texto ?? (m.template ? `[modelo ${m.template}]` : '—')}
                </div>
              </div>
            ))}
          </div>
        )
      ) : null}
    </div>
  );
}
