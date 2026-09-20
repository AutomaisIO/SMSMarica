import { AlertOctagon, EyeOff, FileQuestion, Lightbulb, MessageSquareWarning, Sparkles, UserX } from 'lucide-react';
import type { OuvidoriaIdentificacao, OuvidoriaPrioridade, OuvidoriaStatus, OuvidoriaTipo } from '@/features/ouvidoria/types';
import {
  CLASSE_PRIORIDADE,
  CLASSE_STATUS,
  CLASSE_TIPO,
  ROTULO_PRIORIDADE,
  ROTULO_STATUS,
  ROTULO_TIPO,
} from '@/features/ouvidoria/lib/rotulos';

const base = 'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset whitespace-nowrap';

const ICONE_TIPO = {
  Solicitacao: FileQuestion,
  Reclamacao: MessageSquareWarning,
  Denuncia: AlertOctagon,
  Sugestao: Lightbulb,
  Elogio: Sparkles,
  Informacao: FileQuestion,
} as const;

export function StatusManifestacaoBadge({ status }: { status: OuvidoriaStatus }) {
  return <span className={`${base} ${CLASSE_STATUS[status]}`}>{ROTULO_STATUS[status]}</span>;
}

export function TipoManifestacaoBadge({ tipo }: { tipo: OuvidoriaTipo }) {
  const Icone = ICONE_TIPO[tipo];
  return (
    <span className={`${base} ${CLASSE_TIPO[tipo]}`}>
      <Icone className="h-3 w-3" aria-hidden="true" />
      {ROTULO_TIPO[tipo]}
    </span>
  );
}

export function PrioridadeManifestacaoBadge({ prioridade }: { prioridade: OuvidoriaPrioridade }) {
  return <span className={`${base} ${CLASSE_PRIORIDADE[prioridade]}`}>{ROTULO_PRIORIDADE[prioridade]}</span>;
}

/** Só aparece quando NÃO é identificada — chama atenção para o sigilo. */
export function IdentificacaoBadge({ identificacao }: { identificacao: OuvidoriaIdentificacao }) {
  if (identificacao === 'Identificada') return null;
  const anonima = identificacao === 'Anonima';
  const Icone = anonima ? UserX : EyeOff;
  return (
    <span
      className={`${base} bg-purple-50 text-purple-700 ring-purple-600/20`}
      title={anonima ? 'Sem dados do manifestante e sem código de acesso' : 'Identidade restrita à ouvidoria'}
    >
      <Icone className="h-3 w-3" aria-hidden="true" />
      {anonima ? 'Anônima' : 'Sigilosa'}
    </span>
  );
}
