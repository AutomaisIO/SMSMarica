import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';

import { listarProcedimentosRegrados } from '../api/regulacaoApi';
import {
  SISTEMAS_REGULADORES,
  sistemasVisiveis,
  useSistemasOcultos,
} from '../store/sistemasOcultosPreferencia';
import type { ProcedimentoRegrado } from '../tiposSolicitacao';

const ROTULO_SISTEMA: Record<string, string> = {
  Sisreg: 'SISREG',
  Ser: 'SER',
  Sernit: 'SERNIT',
  Esus: 'e-SUS',
};

const formatar = new Intl.NumberFormat('pt-BR');

/**
 * Os procedimentos mais pedidos, com quantas regras cada um já tem.
 *
 * <p><b>Por que a tela abre por aqui e não por uma busca:</b> o catálogo tem 999 procedimentos e a
 * curadoria tem tempo para poucos. Uma caixa de busca vazia exige saber de antemão o que procurar;
 * esta lista põe na frente o que a rede realmente pede, ordenado por demanda histórica — e mostra,
 * na mesma linha, quantas regras já existem e quantas estão valendo.</p>
 *
 * <p>Os filtros de sistema ficam salvos no usuário. Omitir um sistema o tira <b>da listagem</b>;
 * as regras dele seguem valendo no wizard.</p>
 */
export function ListaProcedimentosRegrados({
  selecionadoId,
  aoSelecionar,
}: {
  selecionadoId?: string;
  aoSelecionar: (p: ProcedimentoRegrado) => void;
}) {
  const ocultos = useSistemasOcultos((s) => s.ocultos);
  const alternar = useSistemasOcultos((s) => s.alternar);
  const visiveis = sistemasVisiveis(ocultos);

  const consulta = useQuery({
    queryKey: ['regulacao', 'regras', 'procedimentos', visiveis],
    queryFn: () => listarProcedimentosRegrados(visiveis),
    // O topo do SISREG agrega 1 milhão de linhas; o servidor cacheia por 6 h, e aqui basta não
    // refazer a chamada a cada foco de janela.
    staleTime: 10 * 60_000,
    enabled: visiveis.length > 0,
  });

  const lista = consulta.data ?? [];

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-slate-600">Mostrar:</span>
        {SISTEMAS_REGULADORES.map((s) => {
          const ativo = !ocultos.includes(s.valor);
          return (
            <button
              key={s.valor}
              type="button"
              onClick={() => alternar(s.valor)}
              aria-pressed={ativo}
              className={`rounded-full border px-3 py-1 text-xs font-medium ${
                ativo
                  ? 'border-red-300 bg-red-50 text-red-800'
                  : 'border-slate-300 bg-white text-slate-400 line-through'
              }`}
            >
              {s.rotulo}
            </button>
          );
        })}
        <span className="text-xs text-slate-500">
          Fica salvo no seu usuário. Omitir some da lista; as regras seguem valendo.
        </span>
      </div>

      {visiveis.length === 0 && (
        <p className="rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          Todos os sistemas estão omitidos — marque ao menos um para ver a lista.
        </p>
      )}

      {consulta.isLoading && (
        <p className="flex items-center gap-2 text-sm text-slate-500">
          <Loader2 className="size-4 animate-spin" />
          Somando a demanda histórica…
        </p>
      )}

      {lista.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="w-full min-w-[46rem] text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
              <tr>
                <th className="px-3 py-2 font-medium">Procedimento</th>
                <th className="px-3 py-2 font-medium">Onde é pedido</th>
                <th className="px-3 py-2 text-right font-medium">Pedidos</th>
                <th className="px-3 py-2 text-right font-medium">Regras</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {lista.map((p) => (
                <tr
                  key={p.procedimentoId}
                  onClick={() => aoSelecionar(p)}
                  className={`cursor-pointer hover:bg-slate-50 ${
                    p.procedimentoId === selecionadoId ? 'bg-red-50' : ''
                  }`}
                >
                  <td className="px-3 py-2 text-slate-900">{p.nome}</td>
                  <td className="px-3 py-2">
                    <div className="flex flex-wrap gap-1">
                      {p.porSistema.map((d) => (
                        <span
                          key={d.sistema}
                          title={`${formatar.format(d.demanda)} pedidos`}
                          className="rounded bg-slate-100 px-1.5 py-0.5 text-xs text-slate-600"
                        >
                          {ROTULO_SISTEMA[d.sistema] ?? d.sistema}
                        </span>
                      ))}
                    </div>
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums text-slate-700">
                    {formatar.format(p.demanda)}
                  </td>
                  <td className="px-3 py-2 text-right">
                    <Contagem ativas={p.regrasAtivas} total={p.totalRegras} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {!consulta.isLoading && visiveis.length > 0 && lista.length === 0 && (
        <p className="rounded border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">
          Nenhum procedimento com demanda nos sistemas selecionados.
        </p>
      )}
    </div>
  );
}

/**
 * "12 de 30" quando parte das regras está desligada, e só o número quando todas valem — mostrar
 * "30 de 30" em toda linha viraria ruído.
 */
function Contagem({ ativas, total }: { ativas: number; total: number }) {
  if (total === 0) return <span className="text-xs text-slate-400">nenhuma</span>;
  if (ativas === total) {
    return <span className="tabular-nums text-slate-700">{total}</span>;
  }
  return (
    <span className="tabular-nums text-slate-700">
      <strong>{ativas}</strong>
      <span className="text-slate-400"> de {total}</span>
    </span>
  );
}
