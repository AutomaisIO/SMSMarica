import type { CodigoProcedimentoConfig } from '@/features/equipamentos/types';

/**
 * Selo "envia código" ao lado do AE Title nas listas de equipamentos: diz, sem abrir o cadastro,
 * quais aparelhos recebem o código do procedimento (0008,0100) na worklist — ADR-0074.
 */
export function SeloCodigoProcedimento({ equipamento }: { equipamento: CodigoProcedimentoConfig }) {
  if (!equipamento.codigoNoProcedimentoSolicitado && !equipamento.codigoNoProtocoloAgendado) return null;
  return (
    <span
      className="ml-2 inline-flex items-center rounded bg-blue-50 px-1.5 py-0.5 text-[11px] font-medium text-blue-700"
      title="Este aparelho recebe o código interno do SISREG de cada exame em (0008,0100), para escolher o protocolo."
    >
      envia código (0008,0100)
    </span>
  );
}
