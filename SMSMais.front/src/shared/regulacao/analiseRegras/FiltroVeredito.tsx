import { cn } from '@/shared/lib/cn';
import { ICONE_VEREDITO } from '@/shared/regulacao/analiseRegras/icones';
import {
  CLASSE_VEREDITO,
  DESCRICAO_VEREDITO,
  ROTULO_VEREDITO,
  VEREDITOS_ANALISE,
  type AnaliseRegrasContagem,
  type VereditoAnaliseRegras,
} from '@/shared/regulacao/analiseRegras/tipos';

/**
 * Chips de filtro por veredito da análise de regras. Com `contagens`, cada chip mostra quantos
 * pedidos há naquele veredito (e os vereditos sem nenhum pedido somem — chip zerado é ruído);
 * sem contagem (SER/SERNIT, cujo resumo não conta por veredito), aparecem todos.
 */
export function ChipsVeredito({
  valor,
  aoMudar,
  contagens,
  className,
}: {
  valor: VereditoAnaliseRegras | '';
  aoMudar: (v: VereditoAnaliseRegras | '') => void;
  contagens?: AnaliseRegrasContagem[];
  className?: string;
}) {
  const quantidade = (v: VereditoAnaliseRegras) =>
    contagens?.find((c) => c.veredito === v)?.quantidade ?? 0;
  const visiveis = contagens
    ? VEREDITOS_ANALISE.filter((v) => quantidade(v) > 0 || v === valor)
    : VEREDITOS_ANALISE;

  return (
    <div className={cn('flex flex-wrap items-center gap-2', className)}>
      <span className="text-xs font-medium uppercase tracking-wide text-slate-500">
        Análise das regras
      </span>
      <button
        type="button"
        onClick={() => aoMudar('')}
        className={cn(
          'rounded-full border px-3 py-1 text-xs',
          valor === ''
            ? 'border-red-300 bg-red-50 text-red-800'
            : 'border-slate-300 bg-white text-slate-600 hover:bg-slate-50',
        )}
      >
        Todos
      </button>
      {visiveis.map((v) => {
        const Icone = ICONE_VEREDITO[v];
        const ativo = valor === v;
        return (
          <button
            key={v}
            type="button"
            title={DESCRICAO_VEREDITO[v]}
            onClick={() => aoMudar(ativo ? '' : v)}
            className={cn(
              'inline-flex items-center gap-1 rounded-full border px-3 py-1 text-xs font-medium transition',
              CLASSE_VEREDITO[v],
              ativo ? 'ring-2 ring-red-300 ring-offset-1' : 'opacity-80 hover:opacity-100',
            )}
          >
            <Icone className="size-3.5" aria-hidden />
            {ROTULO_VEREDITO[v]}
            {contagens && <span className="tabular-nums opacity-80">{quantidade(v)}</span>}
          </button>
        );
      })}
    </div>
  );
}

/** As opções do `<Select>` de veredito da barra de filtros. */
export function OpcoesVeredito() {
  return (
    <>
      <option value="">Todos</option>
      {VEREDITOS_ANALISE.map((v) => (
        <option key={v} value={v}>
          {ROTULO_VEREDITO[v]}
        </option>
      ))}
    </>
  );
}
