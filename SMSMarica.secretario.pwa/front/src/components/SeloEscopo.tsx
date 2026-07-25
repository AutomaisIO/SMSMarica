/**
 * Etiqueta de escopo — aparece na aba "geral" sobre as seções que, apesar do
 * cabeçalho de rede, vêm de uma unidade só (internação e maternidade existem
 * apenas no Conde Modesto Leal).
 *
 * Sem isso, o número herda por vizinhança a leitura de "rede inteira": o
 * Secretário olharia 4 internações e concluiria que a rede internou 4, quando o
 * que ele está vendo é o hospital — e a UPA nem interna.
 */
export function SeloEscopo({ escopo }: { escopo: string | null | undefined }) {
  if (!escopo) return null;

  return (
    <span className="inline-flex items-center rounded-full border border-linha bg-painel px-2.5 py-1 text-[11.5px] font-semibold text-grafite">
      somente {escopo}
    </span>
  );
}
