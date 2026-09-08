import type { SituacaoEscopoExame } from '@/features/escopo-exames/types';

type Props = { situacao: SituacaoEscopoExame; compativeis: number };

/**
 * Estado da linha em forma, não só em palavra — a lista é escaneada, não lida. "Sem destino"
 * distingue os dois motivos (nenhum aparelho × vários) porque a ação do operador é diferente:
 * cadastrar equipamento ou escolher entre os que existem.
 */
export function SituacaoBadge({ situacao, compativeis }: Props) {
  if (situacao === 'Configurado') {
    return (
      <span className="inline-block rounded bg-green-50 px-2 py-0.5 text-xs font-medium text-green-800">
        configurado
      </span>
    );
  }

  if (situacao === 'Desligado') {
    return (
      <span className="inline-block rounded bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-800">
        a configurar
      </span>
    );
  }

  return (
    <span
      className="inline-block rounded bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-700"
      title={
        compativeis === 0
          ? 'A unidade não tem aparelho ativo desta modalidade.'
          : `${compativeis} aparelhos possíveis — a recepção escolhe a sala em cada autorização.`
      }
    >
      {compativeis === 0 ? 'sem aparelho' : `${compativeis} aparelhos`}
    </span>
  );
}
