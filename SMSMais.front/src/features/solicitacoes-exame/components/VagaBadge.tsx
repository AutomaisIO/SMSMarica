import type { TipoVaga } from '@/features/solicitacoes-exame/types';

/**
 * Selo da natureza da vaga no SISREG (coluna 8 "Vaga (flag)" do TXT). "Retorno" ganha destaque
 * (âmbar) — é a marca que o ticket #135 pediu para ficar evidente no card. "Primeira Vez" e o
 * desconhecido (null) não renderizam nada por padrão (o comum não precisa de selo); passe
 * `mostrarPrimeiraVez` para exibir também a 1ª vez (usado no detalhe).
 */
export function VagaBadge({
  tipoVaga,
  mostrarPrimeiraVez = false,
}: {
  tipoVaga: TipoVaga | null;
  mostrarPrimeiraVez?: boolean;
}) {
  if (tipoVaga === 'Retorno') {
    return (
      <span
        title="Vaga de RETORNO no SISREG"
        aria-label="Retorno"
        className="inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-800 ring-1 ring-inset ring-amber-300"
      >
        Retorno
      </span>
    );
  }
  if (tipoVaga === 'PrimeiraVez' && mostrarPrimeiraVez) {
    return (
      <span
        title="Vaga de 1ª vez no SISREG"
        aria-label="Primeira vez"
        className="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-600 ring-1 ring-inset ring-gray-300"
      >
        1ª vez
      </span>
    );
  }
  return null;
}
