import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  CalendarClock,
  CalendarPlus,
  CheckCircle2,
  ChevronRight,
  Clock,
  MapPin,
  Stethoscope,
  X,
} from 'lucide-react';
import { api, type Agendamento } from '@/lib/api';
import { Card, GhostButton, PrimaryButton } from '@/components/ui';
import { Lista } from '@/components/Lista';
import { Etiqueta } from '@/components/Etiqueta';
import { CHAVE_CONFIRMACAO_AGENDAMENTO } from '@/pages/Entrar';

export function ConsultasAgendadas() {
  return (
    <Lista
      eyebrow="Agenda"
      titulo="Consultas agendadas"
      carregar={() => api.agendamentos('consulta')}
      emptyIcon={CalendarClock}
      emptyTitulo="Nenhuma consulta agendada"
      emptyDescricao="Suas próximas consultas marcadas vão aparecer aqui com data, profissional e local."
      renderItem={(a) => <AgendamentoCard agendamento={a} />}
    />
  );
}

type ConfirmacaoMagicLink = {
  solicitacaoExameId: string;
  titulo: string;
  inicioEm: string | null;
  unidade: string | null;
  confirmadaAgora: boolean;
};

export function ExamesAgendados() {
  // Modal "Agenda confirmada" — gravado pelo Entrar.tsx quando o magic link confirmou o exame.
  const [confirmacao, setConfirmacao] = useState<ConfirmacaoMagicLink | null>(null);
  // Força recarregar a Lista após responder num card (key remount).
  const [versao, setVersao] = useState(0);

  useEffect(() => {
    const bruto = sessionStorage.getItem(CHAVE_CONFIRMACAO_AGENDAMENTO);
    if (!bruto) return;
    sessionStorage.removeItem(CHAVE_CONFIRMACAO_AGENDAMENTO);
    try {
      setConfirmacao(JSON.parse(bruto) as ConfirmacaoMagicLink);
    } catch {
      /* conteúdo inesperado — ignora */
    }
  }, []);

  return (
    <>
      <Lista
        key={versao}
        eyebrow="Agenda"
        titulo="Exames agendados"
        carregar={() => api.agendamentos('exame')}
        emptyIcon={CalendarPlus}
        emptyTitulo="Nenhum exame agendado"
        emptyDescricao="Seus próximos exames marcados vão aparecer aqui com data, tipo e local."
        renderItem={(a) => (
          <AgendamentoCard
            agendamento={a}
            destacadoId={confirmacao?.solicitacaoExameId ?? null}
            aoResponder={() => setVersao((v) => v + 1)}
          />
        )}
      />
      {confirmacao && (
        <ModalAgendaConfirmada confirmacao={confirmacao} aoFechar={() => setConfirmacao(null)} />
      )}
    </>
  );
}

function ModalAgendaConfirmada({
  confirmacao,
  aoFechar,
}: {
  confirmacao: ConfirmacaoMagicLink;
  aoFechar: () => void;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-6">
      <Card className="w-full max-w-sm p-6 text-center">
        <CheckCircle2 className="mx-auto h-12 w-12 text-green-600" />
        <h2 className="mt-3 font-display text-lg font-bold text-tinta">Agenda confirmada!</h2>
        <p className="mt-2 text-sm text-tinta-mute">
          Sua presença no exame <strong>{confirmacao.titulo}</strong>
          {confirmacao.inicioEm ? ` em ${formatarDataHora(confirmacao.inicioEm)}` : ''}
          {confirmacao.unidade ? `, ${confirmacao.unidade},` : ''} está confirmada. Obrigado! 😊
        </p>
        <PrimaryButton className="mt-5 w-full" onClick={aoFechar}>
          Ok, entendi
        </PrimaryButton>
      </Card>
    </div>
  );
}

function AgendamentoCard({
  agendamento: a,
  destacadoId = null,
  aoResponder,
}: {
  agendamento: Agendamento;
  destacadoId?: string | null;
  aoResponder?: () => void;
}) {
  const navigate = useNavigate();
  const destacado = a.solicitacaoExameId != null && a.solicitacaoExameId === destacadoId;
  // Exames importados (SISREG) têm ticket com detalhes; consultas (sem solicitação) não.
  const abrirTicket = a.solicitacaoExameId
    ? () => navigate(`/agendados/exames/${a.solicitacaoExameId}`)
    : undefined;
  return (
    <Card className={`p-4 ${destacado ? 'ring-2 ring-green-500' : ''}`}>
      <button
        type="button"
        onClick={abrirTicket}
        disabled={!abrirTicket}
        className="w-full text-left disabled:cursor-default"
      >
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="truncate font-display font-semibold text-tinta">{a.titulo}</p>
            <p className="mt-0.5 flex items-center gap-1.5 text-sm text-tinta-mute">
              <Clock className="h-4 w-4 shrink-0" />
              {formatarDataHora(a.inicioEm)}
            </p>
          </div>
          <div className="flex items-center gap-1">
            <EtiquetaAgendamento agendamento={a} />
            {abrirTicket && <ChevronRight className="h-4 w-4 shrink-0 text-tinta-mute" />}
          </div>
        </div>

        <div className="mt-3 space-y-1.5 text-sm text-tinta-mute">
          {a.profissional && (
            <p className="flex items-center gap-1.5">
              <Stethoscope className="h-4 w-4 shrink-0" />
              <span className="truncate">{a.profissional}</span>
            </p>
          )}
          {a.unidade && (
            <p className="flex items-center gap-1.5">
              <MapPin className="h-4 w-4 shrink-0" />
              <span className="truncate">{a.unidade}</span>
            </p>
          )}
        </div>
      </button>

      {a.podeResponder && a.solicitacaoExameId && (
        <RespostaConfirmacao solicitacaoExameId={a.solicitacaoExameId} aoResponder={aoResponder} />
      )}
    </Card>
  );
}

function EtiquetaAgendamento({ agendamento: a }: { agendamento: Agendamento }) {
  if (a.statusConfirmacao === 'Confirmada') return <Etiqueta status="Confirmada" />;
  if (a.statusConfirmacao === 'Cancelada') return <Etiqueta status="Cancelada" />;
  return <Etiqueta status={a.status} />;
}

/** Botões Confirmar / Não poderei ir do exame ainda sem resposta (importado do SISREG). */
function RespostaConfirmacao({
  solicitacaoExameId,
  aoResponder,
}: {
  solicitacaoExameId: string;
  aoResponder?: () => void;
}) {
  const [pedindoMotivo, setPedindoMotivo] = useState(false);
  const [motivo, setMotivo] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function confirmar() {
    setEnviando(true);
    setErro(null);
    try {
      await api.confirmarExame(solicitacaoExameId);
      aoResponder?.();
    } catch {
      setErro('Não foi possível registrar. Tente novamente.');
      setEnviando(false);
    }
  }

  async function cancelar() {
    if (!motivo.trim()) {
      setErro('Conte pra gente o motivo, assim oferecemos a vaga a outra pessoa.');
      return;
    }
    setEnviando(true);
    setErro(null);
    try {
      await api.cancelarExame(solicitacaoExameId, motivo.trim());
      aoResponder?.();
    } catch {
      setErro('Não foi possível registrar. Tente novamente.');
      setEnviando(false);
    }
  }

  return (
    <div className="mt-4 border-t border-black/5 pt-3">
      {!pedindoMotivo ? (
        <div className="flex gap-2">
          <PrimaryButton className="flex-1" disabled={enviando} onClick={confirmar}>
            Confirmar presença
          </PrimaryButton>
          <GhostButton className="flex-1" disabled={enviando} onClick={() => setPedindoMotivo(true)}>
            Não poderei ir
          </GhostButton>
        </div>
      ) : (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <p className="text-sm font-medium text-tinta">Qual o motivo?</p>
            <button
              type="button"
              aria-label="Fechar"
              className="text-tinta-mute"
              onClick={() => {
                setPedindoMotivo(false);
                setErro(null);
              }}
            >
              <X className="h-4 w-4" />
            </button>
          </div>
          <textarea
            className="w-full rounded-xl border border-black/10 bg-white p-3 text-sm text-tinta outline-none focus:border-marica"
            rows={3}
            maxLength={500}
            placeholder="Ex.: estarei viajando, consegui em outro lugar…"
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
          />
          <PrimaryButton className="w-full" disabled={enviando} onClick={cancelar}>
            Enviar e avisar a unidade
          </PrimaryButton>
        </div>
      )}
      {erro && <p className="mt-2 text-sm text-marica">{erro}</p>}
    </div>
  );
}

function formatarDataHora(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
