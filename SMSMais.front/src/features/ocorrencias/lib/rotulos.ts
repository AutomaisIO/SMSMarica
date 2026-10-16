import { CarFront, HeartPulse, MessageSquareWarning, Wrench, type LucideIcon } from 'lucide-react';
import type { SituacaoOcorrencia, TipoOcorrencia } from '@/features/ocorrencias/types';

export const TIPOS_OCORRENCIA: Record<TipoOcorrencia, { rotulo: string; icone: LucideIcon }> = {
  Acidente: { rotulo: 'Acidente', icone: CarFront },
  PaneVeiculo: { rotulo: 'Pane no veículo', icone: Wrench },
  PacienteMalEstar: { rotulo: 'Paciente passou mal', icone: HeartPulse },
  Outro: { rotulo: 'Outro', icone: MessageSquareWarning },
};

export const SITUACOES_OCORRENCIA: Record<SituacaoOcorrencia, { rotulo: string; classe: string }> = {
  Aberta: { rotulo: 'Aberta', classe: 'bg-red-600 text-white' },
  EmAtendimento: { rotulo: 'Em atendimento', classe: 'bg-amber-500 text-white' },
  Encerrada: { rotulo: 'Encerrada', classe: 'bg-gray-200 text-gray-700' },
};

export function rotuloTipo(tipo: string): string {
  return TIPOS_OCORRENCIA[tipo as TipoOcorrencia]?.rotulo ?? tipo;
}
