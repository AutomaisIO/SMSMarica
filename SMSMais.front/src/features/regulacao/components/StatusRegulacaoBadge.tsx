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

export function StatusRegulacaoBadge({ status }: { status: StatusRegulacao }) {
  return (
    <span className={`inline-block whitespace-nowrap rounded px-2 py-0.5 text-xs font-medium ${CORES[status]}`}>
      {ROTULO_STATUS[status]}
    </span>
  );
}
