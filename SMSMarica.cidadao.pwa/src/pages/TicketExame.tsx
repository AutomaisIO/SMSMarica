import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { QRCodeSVG } from 'qrcode.react';
import {
  ArrowLeft,
  Building2,
  CalendarPlus,
  CheckCircle2,
  Clock,
  FileText,
  MapPin,
  Phone,
  Stethoscope,
  XCircle,
} from 'lucide-react';
import { api, type AgendamentoExameDetalhe } from '@/lib/api';
import { extrairMensagemDeErro } from '@/lib/httpClient';
import { ErroCard, Spinner } from '@/components/ui';

const ROTULO_CANAL: Record<string, string> = {
  app: 'pelo aplicativo',
  'whatsapp-link': 'pelo link do WhatsApp',
  'whatsapp-quickreply': 'pelo WhatsApp',
  ligacao: 'por ligação',
  telefone: 'por ligação',
  sandbox: 'teste (sandbox)',
};

function canalTexto(canal: string | null): string {
  if (!canal) return '';
  return ROTULO_CANAL[canal] ?? `por ${canal}`;
}

export function TicketExame() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const [detalhe, setDetalhe] = useState<AgendamentoExameDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  // QR do "ticket" — por ora um UUID aleatório (estável enquanto a tela está aberta).
  const qrValor = useMemo(() => crypto.randomUUID(), []);

  useEffect(() => {
    let vivo = true;
    api
      .agendamentoExame(id)
      .then((d) => vivo && setDetalhe(d))
      .catch((e) => vivo && setErro(extrairMensagemDeErro(e)));
    return () => {
      vivo = false;
    };
  }, [id]);

  if (erro) {
    return (
      <div className="animate-rise pt-2">
        <Voltar aoVoltar={() => navigate(-1)} />
        <ErroCard mensagem={erro} />
      </div>
    );
  }
  if (!detalhe) {
    return (
      <div className="grid place-items-center py-24">
        <Spinner />
      </div>
    );
  }

  const d = detalhe;
  const confirmada = d.statusConfirmacao === 'Confirmada';
  const cancelada = d.statusConfirmacao === 'Cancelada';

  return (
    <div className="animate-rise space-y-4 pb-6">
      <Voltar aoVoltar={() => navigate(-1)} />

      <div className="overflow-hidden rounded-3xl bg-white shadow-cartao ring-1 ring-areia">
        {/* Cabeçalho do ticket */}
        <div className="bg-gradient-to-br from-vinho to-marica p-5 text-white">
          <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.2em] text-white/85">
            <CalendarPlus className="h-3.5 w-3.5" />
            Exame agendado
          </div>
          <h1 className="mt-2 font-display text-2xl font-bold leading-tight">{d.tipoExame}</h1>
          {d.dataAgendada && (
            <p className="mt-1 flex items-center gap-1.5 text-sm text-white/90">
              <Clock className="h-4 w-4" />
              {formatarDataHora(d.dataAgendada)}
            </p>
          )}
          <div className="mt-3">
            <EtiquetaStatus status={d.statusConfirmacao} />
          </div>
        </div>

        {/* Recorte perfurado */}
        <div className="relative h-6 bg-white">
          <div className="absolute -left-3 top-1/2 h-6 w-6 -translate-y-1/2 rounded-full bg-areia" />
          <div className="absolute -right-3 top-1/2 h-6 w-6 -translate-y-1/2 rounded-full bg-areia" />
          <div className="absolute inset-x-4 top-1/2 -translate-y-1/2 border-t-2 border-dashed border-areia" />
        </div>

        {/* Corpo do ticket */}
        <div className="space-y-4 px-5 pb-5">
          <Item icone={Building2} rotulo="Local do exame (executante)" valor={d.unidadeExecutoraNome} />
          {d.unidadeExecutoraEndereco && (
            <Item icone={MapPin} rotulo="Endereço" valor={d.unidadeExecutoraEndereco} />
          )}
          {d.unidadeExecutoraTelefone && (
            <Item icone={Phone} rotulo="Telefone da unidade" valor={d.unidadeExecutoraTelefone} />
          )}
          <Item icone={Building2} rotulo="Unidade solicitante" valor={d.unidadeSolicitanteNome} />
          <Item icone={Stethoscope} rotulo="Solicitante" valor={d.solicitanteNome} />

          <div className="grid grid-cols-2 gap-3">
            <ItemPequeno rotulo="Data da solicitação" valor={formatarData(d.dataSolicitacao)} />
            <ItemPequeno rotulo="Data da regulação" valor={formatarData(d.dataRegulacao)} />
            <ItemPequeno rotulo="Nº da solicitação" valor={d.codigoSolicitacao} />
            <ItemPequeno rotulo="Protocolo" valor={d.accessionNumber} />
          </div>

          {d.observacoes && <Item icone={FileText} rotulo="Observações" valor={d.observacoes} />}

          {/* Rastro da confirmação/cancelamento */}
          {(confirmada || cancelada) && (
            <div
              className={`rounded-2xl p-3 text-sm ${
                confirmada ? 'bg-green-50 text-green-800' : 'bg-red-50 text-red-800'
              }`}
            >
              {confirmada ? (
                <p className="flex items-start gap-2">
                  <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
                  <span>
                    Presença <strong>confirmada</strong>
                    {d.confirmadoCanal ? ` ${canalTexto(d.confirmadoCanal)}` : ''}
                    {d.confirmadoEm ? ` em ${formatarDataHora(d.confirmadoEm)}` : ''}.
                  </span>
                </p>
              ) : (
                <div className="space-y-1">
                  <p className="flex items-start gap-2">
                    <XCircle className="mt-0.5 h-4 w-4 shrink-0" />
                    <span>
                      Você informou que <strong>não poderá comparecer</strong>
                      {d.confirmadoCanal ? ` ${canalTexto(d.confirmadoCanal)}` : ''}
                      {d.confirmacaoCanceladaEm ? ` em ${formatarDataHora(d.confirmacaoCanceladaEm)}` : ''}.
                    </span>
                  </p>
                  {d.motivoCancelamentoPaciente && (
                    <p className="pl-6 text-red-700">Motivo: {d.motivoCancelamentoPaciente}</p>
                  )}
                </div>
              )}
            </div>
          )}

          {/* QR do ticket */}
          <div className="flex flex-col items-center gap-2 border-t border-dashed border-areia pt-5">
            <div className="rounded-xl bg-white p-2 ring-1 ring-areia">
              <QRCodeSVG value={qrValor} size={140} bgColor="#ffffff" fgColor="#7a1420" level="M" />
            </div>
            <p className="text-center text-[11px] text-tinta-mute">
              Apresente este código na recepção da unidade.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

function Voltar({ aoVoltar }: { aoVoltar: () => void }) {
  return (
    <button
      type="button"
      onClick={aoVoltar}
      className="mb-1 flex items-center gap-1.5 text-sm font-medium text-tinta-mute transition active:scale-95"
    >
      <ArrowLeft className="h-4 w-4" /> Voltar
    </button>
  );
}

function EtiquetaStatus({ status }: { status: AgendamentoExameDetalhe['statusConfirmacao'] }) {
  const mapa = {
    Confirmada: { txt: 'Confirmada', cls: 'bg-white/20 text-white' },
    Cancelada: { txt: 'Não poderá comparecer', cls: 'bg-white/20 text-white' },
    Pendente: { txt: 'Aguardando sua confirmação', cls: 'bg-white/20 text-white' },
  }[status];
  return (
    <span className={`inline-flex rounded-full px-3 py-1 text-xs font-semibold ${mapa.cls}`}>{mapa.txt}</span>
  );
}

function Item({
  icone: Icone,
  rotulo,
  valor,
}: {
  icone: typeof Building2;
  rotulo: string;
  valor: string | null;
}) {
  if (!valor) return null;
  return (
    <div className="flex items-start gap-3">
      <span className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-lagoa-claro text-lagoa">
        <Icone className="h-4 w-4" />
      </span>
      <div className="min-w-0">
        <p className="text-[11px] font-medium uppercase tracking-wide text-tinta-mute">{rotulo}</p>
        <p className="text-sm font-medium text-tinta">{valor}</p>
      </div>
    </div>
  );
}

function ItemPequeno({ rotulo, valor }: { rotulo: string; valor: string | null }) {
  return (
    <div className="rounded-xl bg-papel/60 px-3 py-2">
      <p className="text-[10px] font-medium uppercase tracking-wide text-tinta-mute">{rotulo}</p>
      <p className="truncate text-sm font-medium text-tinta">{valor || '—'}</p>
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

function formatarData(iso: string | null): string | null {
  if (!iso) return null;
  const [a, m, dia] = iso.slice(0, 10).split('-');
  return dia && m && a ? `${dia}/${m}/${a}` : iso;
}
