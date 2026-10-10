import { useState } from 'react';
import {
  AlertTriangle,
  ArrowDownLeft,
  ArrowUpRight,
  BellOff,
  CalendarCheck,
  Check,
  CheckCheck,
  CheckCircle2,
  Clock3,
  ShieldCheck,
  XCircle,
} from 'lucide-react';

import { rotuloCanal, rotuloFinalidade } from '@/features/mensageria/lib/rotulos';
import { ChecksComunicacao } from '@/features/solicitacoes/components/ChecksComunicacao';
import { formatarInstante, formatarInstanteData } from '@/shared/lib/datas';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { formatarTelefoneBR } from '@/shared/ui/TelefoneCopiavel';

import { useContatoPaciente } from '../api/solicitacoesQueries';
import type { ContatoPacienteRegulacao, MensagemAoPaciente } from '../tiposSolicitacao';
import { ROTULO_SISTEMA_REGULACAO } from '../types';

/** O teto do servidor (`RegulacaoContatoPacienteService.MaxMensagens`). */
const MAX_MENSAGENS = 40;

const ROTULO_MEIO: Record<string, string> = {
  Ligacao: 'Ligação',
  WhatsApp: 'WhatsApp',
  Presencial: 'Presencial',
  Outro: 'Outro',
};
const ROTULO_RESULTADO: Record<string, string> = {
  Atendeu: 'atendeu',
  NaoAtendeu: 'não atendeu',
  CaixaPostal: 'caixa postal',
  NumeroInvalido: 'número inválido',
  Outro: 'outro',
};
const ROTULO_MIDIA: Record<string, string> = {
  Imagem: '(imagem)',
  Documento: '(documento)',
  Audio: '(áudio)',
  Video: '(vídeo)',
};

type Props = {
  solicitacaoId: string;
  /** O artigo do manual da tela onde o quadro aparece. */
  artigoManual: 'regulacao-gestao-fila' | 'regulacao-solicitacoes';
};

/**
 * O contato com o paciente sobre o pedido: se ele foi avisado do agendamento pelo WhatsApp, se a
 * mensagem chegou, se leu e o que respondeu — para a unidade e o regulador saberem sem abrir o
 * módulo Conversas.
 *
 * <p><b>Só o SISREG tem aviso automático.</b> Do SER, do SERNIT e do ESUS a plataforma ainda não
 * avisa sozinha; o quadro diz isso com todas as letras, e as mensagens da conversa mostram se
 * alguém da equipe avisou à mão. Aviso automático de <b>outro</b> pedido do mesmo paciente vem
 * marcado — o texto do modelo não diz o procedimento.</p>
 */
export function ContatoComPaciente({ solicitacaoId, artigoManual }: Props) {
  const q = useContatoPaciente(solicitacaoId);
  const c = q.data;

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <div className="mb-3 flex items-center gap-1">
        <h2 className="text-sm font-semibold text-slate-900">Contato com o paciente</h2>
        <AjudaManual artigo={artigoManual} secao="contato-paciente" />
      </div>

      {q.isLoading ? (
        <p className="text-sm text-slate-500">Carregando…</p>
      ) : !c ? (
        <p className="text-sm text-slate-500">Não foi possível carregar o contato com o paciente.</p>
      ) : (
        <div className="space-y-3">
          <Agendamento c={c} />
          <AvisoDoAgendamento c={c} />
          <Telefone confirmado={c.telefoneConfirmado} />
          <Mensagens mensagens={c.mensagens} />
          {c.contatos.length > 0 ? (
            <div>
              <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
                Contatos registrados pela equipe
              </h3>
              <ul className="space-y-1 text-sm text-slate-700">
                {c.contatos.map((r) => (
                  <li key={r.id}>
                    <span className="font-medium">{ROTULO_MEIO[r.meio] ?? r.meio}</span>
                    {' — '}
                    {ROTULO_RESULTADO[r.resultado] ?? r.resultado}, {formatarInstante(r.criadoEm)}
                    {r.registradoPorNome ? ` · por ${r.registradoPorNome}` : ''}
                    {r.observacao ? <span className="text-slate-500"> · {r.observacao}</span> : null}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      )}
    </section>
  );
}

function Agendamento({ c }: { c: ContatoPacienteRegulacao }) {
  const a = c.agendamento;
  if (!a) return null;
  const quando = a.dataHora ? formatarInstante(a.dataHora) : a.dataTexto;
  return (
    <p className="flex items-start gap-2 text-sm text-slate-800">
      <CalendarCheck className="mt-0.5 size-4 shrink-0 text-primary-600" />
      <span>
        Agendado no {ROTULO_SISTEMA_REGULACAO[a.sistema] ?? a.sistema} para <b>{quando}</b>
        {a.local ? <> — {a.local}</> : null}.
      </span>
    </p>
  );
}

function AvisoDoAgendamento({ c }: { c: ContatoPacienteRegulacao }) {
  if (!c.destinoComAvisoAutomatico) {
    const sistema = c.agendamento ? (ROTULO_SISTEMA_REGULACAO[c.agendamento.sistema] ?? 'sistema de destino') : null;
    return (
      <p className="flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50 p-2 text-sm text-amber-900">
        <BellOff className="mt-0.5 size-4 shrink-0" />
        <span>
          <b>A plataforma não avisa sozinha os agendamentos {sistema ? `do ${sistema}` : 'deste destino'}.</b> Veja
          nas mensagens abaixo se alguém da equipe avisou o paciente pelo WhatsApp. Se ninguém avisou, ele pode não
          saber da data.
        </span>
      </p>
    );
  }

  if (c.avisos.length === 0) {
    return (
      <p className="flex items-start gap-2 text-sm text-slate-600">
        <BellOff className="mt-0.5 size-4 shrink-0 text-slate-400" />
        <span>
          Nenhum aviso automático saiu para este agendamento. O aviso do SISREG só sai para unidade executante e
          procedimento ligados nas Confirmações, e para data que ainda não passou.
        </span>
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {c.avisos.map((a) => (
        <div key={a.id} className="rounded-md border border-slate-100 bg-slate-50/60 px-3 py-2 text-sm">
          <div className="flex flex-wrap items-center gap-2">
            <ChecksComunicacao
              chip={{ status: a.status, visualizado: a.visualizadoEm != null, motivo: a.motivoFalha }}
              finalidade={a.finalidade}
            />
            <span className="font-medium text-slate-900">{rotuloFinalidade(a.finalidade)}</span>
            {a.telefone ? <span className="text-xs text-slate-500">→ {formatarTelefoneBR(a.telefone)}</span> : null}
          </div>
          <div className="mt-1 flex flex-wrap gap-x-4 gap-y-0.5 text-xs text-slate-600">
            {a.enviadoEm ? <span>Enviada: {formatarInstante(a.enviadoEm)}</span> : <span>Na fila de envio</span>}
            {a.entregueEm ? <span>Entregue: {formatarInstante(a.entregueEm)}</span> : null}
            {a.lidoEm ? <span>Lida: {formatarInstante(a.lidoEm)}</span> : null}
            {a.visualizadoEm ? <span className="text-sky-700">Abriu o link: {formatarInstante(a.visualizadoEm)}</span> : null}
          </div>
          {a.motivoFalha || a.erroMeta ? (
            <p className="mt-1 text-xs text-amber-800">{a.motivoFalha ?? a.erroMeta}</p>
          ) : null}
        </div>
      ))}
      <Resposta c={c} />
    </div>
  );
}

function Resposta({ c }: { c: ContatoPacienteRegulacao }) {
  if (c.confirmacao === 'Confirmada') {
    return (
      <p className="flex items-start gap-2 text-sm text-emerald-800">
        <CheckCircle2 className="mt-0.5 size-4 shrink-0" />
        <span>
          <b>O paciente confirmou que vai.</b>
          {c.confirmadoEm ? ` ${formatarInstante(c.confirmadoEm)}` : ''}
          {c.confirmadoCanal ? ` · ${rotuloCanal(c.confirmadoCanal)}` : ''}
        </span>
      </p>
    );
  }
  if (c.confirmacao === 'Cancelada') {
    return (
      <p className="flex items-start gap-2 text-sm text-amber-900">
        <XCircle className="mt-0.5 size-4 shrink-0" />
        <span>
          <b>O paciente avisou que não vai.</b>
          {c.motivoCancelamentoPaciente ? ` Motivo: “${c.motivoCancelamentoPaciente}”` : ''}
        </span>
      </p>
    );
  }
  if (c.confirmacao === 'Pendente') {
    return (
      <p className="flex items-start gap-2 text-sm text-slate-600">
        <Clock3 className="mt-0.5 size-4 shrink-0 text-slate-400" />
        <span>O paciente ainda não respondeu ao aviso.</span>
      </p>
    );
  }
  return null;
}

function Telefone({ confirmado }: { confirmado: string | null }) {
  return confirmado ? (
    <p className="flex items-start gap-2 text-sm text-emerald-800">
      <ShieldCheck className="mt-0.5 size-4 shrink-0" />
      <span>
        Telefone confirmado pelo paciente: <b>{formatarTelefoneBR(confirmado)}</b>.
      </span>
    </p>
  ) : (
    <p className="flex items-start gap-2 text-sm text-slate-600">
      <AlertTriangle className="mt-0.5 size-4 shrink-0 text-amber-500" />
      <span>
        Nenhum telefone confirmado pelo paciente — a mensagem pode ter ido a um número que não é dele.
      </span>
    </p>
  );
}

function Mensagens({ mensagens }: { mensagens: MensagemAoPaciente[] }) {
  return (
    <div>
      <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
        Mensagens pelo WhatsApp desde a abertura do pedido
      </h3>
      {mensagens.length === 0 ? (
        <p className="text-sm text-slate-500">Nenhuma mensagem com o paciente desde a abertura do pedido.</p>
      ) : (
        <ul className="space-y-2">
          {mensagens.map((m) => (
            <Mensagem key={m.id} m={m} />
          ))}
        </ul>
      )}
      {mensagens.length >= MAX_MENSAGENS ? (
        <p className="mt-1 text-xs text-slate-500">
          Mostrando as {MAX_MENSAGENS} mais recentes. A conversa inteira está no módulo Conversas.
        </p>
      ) : null}
    </div>
  );
}

function Mensagem({ m }: { m: MensagemAoPaciente }) {
  const [aberta, setAberta] = useState(false);
  const texto = m.texto ?? (m.tipo ? ROTULO_MIDIA[m.tipo] : null) ?? '(sem texto)';
  const longo = texto.length > 280;
  const quem = m.doPaciente
    ? 'Paciente'
    : (m.autor ?? (m.tipo === 'Robo' ? 'Robô de atendimento' : 'Aviso automático'));

  return (
    <li
      className={
        m.doPaciente
          ? 'rounded-md border border-primary-100 bg-primary-50/60 px-3 py-2 text-sm'
          : 'rounded-md border border-slate-100 bg-white px-3 py-2 text-sm'
      }
    >
      <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs text-slate-500">
        {m.doPaciente ? (
          <ArrowDownLeft className="size-3.5 text-primary-600" />
        ) : (
          <ArrowUpRight className="size-3.5 text-slate-400" />
        )}
        <span className="font-medium text-slate-700">{quem}</span>
        <span>· {formatarInstante(m.em)}</span>
        {m.telefone ? <span>· {formatarTelefoneBR(m.telefone)}</span> : null}
        {!m.doPaciente ? <StatusMensagem m={m} /> : null}
      </div>
      {m.aviso ? (
        <p className="mt-0.5 text-xs text-slate-600">
          {m.aviso.destePedido ? (
            <>{rotuloFinalidade(m.aviso.finalidade)} deste agendamento</>
          ) : (
            <>
              {rotuloFinalidade(m.aviso.finalidade)} de <b>outro pedido</b>
              {m.aviso.procedimento ? `: ${m.aviso.procedimento}` : ''}
              {m.aviso.dataAgendada ? `, ${formatarInstanteData(m.aviso.dataAgendada)}` : ''}
            </>
          )}
        </p>
      ) : null}
      <p className={`mt-1 whitespace-pre-line break-words text-slate-800 ${longo && !aberta ? 'line-clamp-4' : ''}`}>
        {texto}
      </p>
      {longo ? (
        <button
          type="button"
          onClick={() => setAberta((v) => !v)}
          className="mt-0.5 text-xs text-primary-700 hover:underline"
        >
          {aberta ? 'Mostrar menos' : 'Ver a mensagem inteira'}
        </button>
      ) : null}
    </li>
  );
}

/** ✓ enviada · ✓✓ entregue · ✓✓ azul lida · ⚠ falha — como no WhatsApp. */
function StatusMensagem({ m }: { m: MensagemAoPaciente }) {
  if (m.status === 'Falha') {
    return (
      <span className="inline-flex items-center gap-1 text-amber-700" title={m.erro ?? undefined}>
        <AlertTriangle className="size-3.5" /> não chegou{m.erro ? ` — ${m.erro}` : ''}
      </span>
    );
  }
  if (m.status === 'Lida') {
    return (
      <span className="inline-flex items-center gap-1 text-sky-700">
        <CheckCheck className="size-3.5" /> lida
      </span>
    );
  }
  if (m.status === 'Entregue') {
    return (
      <span className="inline-flex items-center gap-1">
        <CheckCheck className="size-3.5" /> entregue
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1">
      <Check className="size-3.5" /> enviada
    </span>
  );
}
