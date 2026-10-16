import {
  TAG_CODIGO_PADRAO,
  type CamposWorklistConfig,
  type CodigoProcedimentoConfig,
} from '@/features/equipamentos/types';

const SELO = 'ml-2 inline-flex items-center rounded bg-blue-50 px-1.5 py-0.5 text-[11px] font-medium text-blue-700';

/**
 * Selos ao lado do AE Title nas listas de equipamentos: dizem, sem abrir o cadastro, o que este
 * aparelho recebe a mais na worklist — código do procedimento (ADR-0074), médico solicitante e
 * unidade (adendo de 16/10/2026) —, e em que tag. Nada ligado, nada aparece.
 */
export function SelosWorklist({ equipamento: e }: { equipamento: CodigoProcedimentoConfig & CamposWorklistConfig }) {
  const enviaCodigo = e.codigoNoProcedimentoSolicitado || e.codigoNoProtocoloAgendado || e.codigoProcedimentoTag !== null;
  return (
    <>
      {enviaCodigo ? (
        <span
          className={SELO}
          title="Este aparelho recebe o código interno do SISREG de cada exame, para escolher o protocolo."
        >
          envia código ({e.codigoProcedimentoTag ?? TAG_CODIGO_PADRAO})
        </span>
      ) : null}
      {e.medicoSolicitanteTag ? (
        <span className={SELO} title="Este aparelho recebe o nome do médico solicitante.">
          envia médico ({e.medicoSolicitanteTag})
        </span>
      ) : null}
      {e.unidadeTag ? (
        <span className={SELO} title="Este aparelho recebe o nome da unidade (clínica).">
          envia unidade {e.unidadeOrigem === 'Solicitante' ? 'solicitante' : 'executante'} ({e.unidadeTag})
        </span>
      ) : null}
    </>
  );
}
