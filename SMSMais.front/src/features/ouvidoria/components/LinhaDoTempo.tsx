import {
  AlertTriangle,
  Archive,
  ArrowRightLeft,
  BellRing,
  CheckCircle2,
  ClipboardList,
  Eye,
  EyeOff,
  FilePlus2,
  Gavel,
  KeyRound,
  MessageCircle,
  MessageSquareReply,
  RefreshCw,
  Reply,
  RotateCcw,
  Send,
  ShieldAlert,
  StickyNote,
  Tags,
  TimerReset,
  Undo2,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { formatarInstante } from '@/shared/lib/datas';
import { ROTULO_STATUS, ROTULO_TIPO_EVENTO } from '@/features/ouvidoria/lib/rotulos';
import { AnexosOuvidoriaLista } from '@/features/ouvidoria/components/AnexosOuvidoria';
import type { EventoDto, OuvidoriaTipoEvento } from '@/features/ouvidoria/types';

const ICONE: Record<OuvidoriaTipoEvento, LucideIcon> = {
  Registro: FilePlus2,
  Triagem: ClipboardList,
  Reclassificacao: Tags,
  Encaminhamento: Send,
  PedidoComplementacao: MessageCircle,
  Complementacao: MessageSquareReply,
  RespostaArea: Reply,
  DevolucaoParaReanalise: Undo2,
  RespostaIntermediaria: MessageCircle,
  RespostaConclusiva: CheckCircle2,
  Prorrogacao: TimerReset,
  Cobranca: BellRing,
  Escalonamento: AlertTriangle,
  Recurso: Gavel,
  Conclusao: CheckCircle2,
  Arquivamento: Archive,
  EncaminhamentoExterno: ArrowRightLeft,
  Anotacao: StickyNote,
  Habilitacao: ShieldAlert,
  Reabertura: RotateCcw,
  AcessoIdentidade: KeyRound,
};

const COR: Partial<Record<OuvidoriaTipoEvento, string>> = {
  RespostaConclusiva: 'bg-green-100 text-green-700',
  Conclusao: 'bg-green-100 text-green-700',
  Arquivamento: 'bg-gray-200 text-gray-600',
  Cobranca: 'bg-amber-100 text-amber-700',
  Escalonamento: 'bg-orange-100 text-orange-700',
  Recurso: 'bg-orange-100 text-orange-700',
  Habilitacao: 'bg-red-100 text-red-700',
  AcessoIdentidade: 'bg-purple-100 text-purple-700',
};

type Props = { eventos: EventoDto[] };

/** Trilha append-only da manifestação; destaca o que o cidadão vê e o que é interno. */
export function LinhaDoTempo({ eventos }: Props) {
  if (eventos.length === 0) return <p className="text-sm text-slate-400">Nenhum evento registrado.</p>;

  const ordenados = [...eventos].sort((a, b) => a.criadoEm.localeCompare(b.criadoEm));

  return (
    <ol className="relative space-y-4 border-l border-slate-200 pl-6" aria-label="Linha do tempo da manifestação">
      {ordenados.map((e) => {
        const Icone = ICONE[e.tipo] ?? RefreshCw;
        return (
          <li key={e.id} className="relative">
            <span
              className={cn(
                'absolute -left-[1.95rem] flex h-7 w-7 items-center justify-center rounded-full ring-4 ring-white',
                COR[e.tipo] ?? 'bg-slate-100 text-slate-600',
              )}
              aria-hidden="true"
            >
              <Icone className="h-3.5 w-3.5" />
            </span>
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <span className="text-sm font-medium text-slate-800">{ROTULO_TIPO_EVENTO[e.tipo] ?? e.tipo}</span>
              <span
                className={cn(
                  'inline-flex items-center gap-1 rounded-full px-1.5 py-0.5 text-[11px] font-medium',
                  e.visivelAoCidadao ? 'bg-sky-50 text-sky-700' : 'bg-slate-100 text-slate-500',
                )}
                title={e.visivelAoCidadao ? 'O cidadão vê este evento no acompanhamento' : 'Evento interno — o cidadão não vê'}
              >
                {e.visivelAoCidadao ? <Eye className="h-3 w-3" aria-hidden="true" /> : <EyeOff className="h-3 w-3" aria-hidden="true" />}
                {e.visivelAoCidadao ? 'visível ao cidadão' : 'interno'}
              </span>
              <span className="text-xs text-slate-500">
                {formatarInstante(e.criadoEm)}
                {e.autorNome ? ` · ${e.autorNome}` : ''}
                {e.pontoRespostaNome ? ` · ${e.pontoRespostaNome}` : ''}
              </span>
            </div>
            {(e.statusAnterior || e.statusNovo) && e.statusAnterior !== e.statusNovo ? (
              <p className="mt-0.5 text-xs text-slate-500">
                {e.statusAnterior ? ROTULO_STATUS[e.statusAnterior] : '—'} → {e.statusNovo ? ROTULO_STATUS[e.statusNovo] : '—'}
              </p>
            ) : null}
            {e.texto ? <p className="mt-1 whitespace-pre-wrap text-sm text-slate-700">{e.texto}</p> : null}
            {e.anexos.length > 0 ? (
              <div className="mt-2">
                <AnexosOuvidoriaLista anexos={e.anexos} />
              </div>
            ) : null}
          </li>
        );
      })}
    </ol>
  );
}
