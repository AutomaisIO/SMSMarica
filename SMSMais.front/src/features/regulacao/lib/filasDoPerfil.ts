import { useAuth } from '@/shared/auth/authStore';
import type { ModuloPermissao } from '@/shared/auth/authStore';

import type { FluxoRegulacao } from '../tiposSolicitacao';

/** Que módulo abre qual fila da Gestão de fila (espelho de `RegulacaoEscopo.ModuloDaFila`). */
export const MODULO_DA_FILA: Record<FluxoRegulacao, ModuloPermissao> = {
  Nar: 'RegulacaoFilaNar',
  Interno: 'RegulacaoFilaInterna',
  Externo: 'RegulacaoFilaIntermunicipal',
};

/** As filas que o perfil do usuário enxerga na Gestão de fila — a tela só oferece estas. */
export function useFilasDoPerfil(): FluxoRegulacao[] {
  const permissoes = useAuth((s) => s.permissoes);
  return (Object.keys(MODULO_DA_FILA) as FluxoRegulacao[]).filter((f) =>
    (permissoes[MODULO_DA_FILA[f]] ?? []).includes('Consulta'),
  );
}
