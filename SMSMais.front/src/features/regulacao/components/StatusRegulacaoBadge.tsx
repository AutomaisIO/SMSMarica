import { Waypoints } from 'lucide-react';

import { cn } from '@/shared/lib/cn';

import type { FluxoRegulacao, StatusRegulacao } from '../tiposSolicitacao';

/**
 * A cor conta a mesma história em toda tela: cinza é rascunho (ainda em casa), âmbar é espera,
 * azul é "andando lá fora", verde é resolvido, vermelho é fim sem atendimento.
 *
 * <p>`FalhaEnvio` é vermelho de propósito, e não âmbar: é o único estado que exige alguém agir
 * agora — a solicitação parou no meio do caminho.</p>
 */
const CORES: Record<StatusRegulacao, string> = {
  Rascunho: 'bg-slate-100 text-slate-700',
  PendenteRegulacao: 'bg-amber-100 text-amber-900',
  EmAnalise: 'bg-indigo-100 text-indigo-800',
  Devolvida: 'bg-orange-100 text-orange-900',
  EnviandoAoSistema: 'bg-sky-100 text-sky-800',
  EnviadaAoSistema: 'bg-sky-100 text-sky-800',
  EmFilaExterna: 'bg-blue-100 text-blue-800',
  Agendada: 'bg-emerald-100 text-emerald-800',
  Concluida: 'bg-emerald-100 text-emerald-800',
  Cancelada: 'bg-slate-200 text-slate-600',
  Recusada: 'bg-red-100 text-red-800',
  FalhaEnvio: 'bg-red-100 text-red-800',
};

/** O nome que a pessoa lê. O enum é do código; a fila é de quem atende. */
export const ROTULO_STATUS: Record<StatusRegulacao, string> = {
  Rascunho: 'Rascunho',
  PendenteRegulacao: 'Na pré-regulação',
  EmAnalise: 'Em análise',
  Devolvida: 'Devolvida à unidade',
  EnviandoAoSistema: 'Enviando…',
  EnviadaAoSistema: 'Enviada ao sistema',
  EmFilaExterna: 'Na fila do sistema',
  Agendada: 'Agendada',
  Concluida: 'Concluída',
  Cancelada: 'Cancelada',
  Recusada: 'Recusada',
  FalhaEnvio: 'Falha no envio',
};

export const ROTULO_FLUXO: Record<FluxoRegulacao, string> = {
  Interno: 'Interno (SISREG)',
  Externo: 'Externo',
  Nar: 'NAR',
};

/** As filas da Gestão de fila. "Intermunicipal" é o fluxo Externo (SER, SERNIT, ESUS). */
export const ROTULO_FILA: Record<FluxoRegulacao, string> = {
  Nar: 'NAR',
  Interno: 'Interna',
  Externo: 'Intermunicipal',
};

export const FILAS: FluxoRegulacao[] = ['Nar', 'Interno', 'Externo'];

export function StatusRegulacaoBadge({ status }: { status: StatusRegulacao }) {
  return (
    <span className={`inline-block whitespace-nowrap rounded px-2 py-0.5 text-xs font-medium ${CORES[status]}`}>
      {ROTULO_STATUS[status]}
    </span>
  );
}

/**
 * O NAR tem cara própria em toda tela — fila da unidade, gestão de fila e o cabeçalho do caso.
 *
 * <p>Não é só mais um fluxo: o pedido entra no SISREG <b>em nome de outra unidade</b>, com o CNES
 * dela, e quem lança precisa notar isso antes de abrir o SISREG. Por isso violeta cheio, e não a
 * cor clara dos status — a etiqueta tem de ser vista de longe numa fila de cinquenta linhas
 * (pedido do Bernardo, 22/10/2026).</p>
 */
export function BadgeNar({ emNomeDe, className }: { emNomeDe?: string | null; className?: string }) {
  return (
    <span
      className={cn(
        'inline-flex max-w-full items-center gap-1 rounded bg-violet-600 px-2 py-0.5 text-xs font-semibold text-white',
        className,
      )}
      title={emNomeDe ? `NAR — agendamento indireto, em nome de ${emNomeDe}` : 'NAR — agendamento indireto'}
    >
      <Waypoints className="size-3.5 shrink-0" aria-hidden />
      NAR
      {emNomeDe && <span className="truncate font-normal">· {emNomeDe}</span>}
    </span>
  );
}

/** Fundo das linhas NAR na tabela: o selo diz o que é, o fundo faz a linha saltar ao rolar. */
export const CLASSE_LINHA_NAR = 'bg-violet-50 hover:bg-violet-100';
