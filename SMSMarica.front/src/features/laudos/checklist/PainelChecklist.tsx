import { useMemo } from 'react';
import { Calculator } from 'lucide-react';
import {
  CATEGORIAS_BIRADS,
  corBiRads,
  normalizarBiRads,
  rotuloBiRads,
  sugerirBiRads,
} from './birads';
import { coletarContribuicoes } from './gerarTexto';
import type {
  CampoItem,
  ItemChecklist,
  RespostaItem,
  RespostasChecklist,
  SecaoChecklist,
} from './types';

type Props = {
  respostas: RespostasChecklist;
  somenteLeitura?: boolean;
  aoMudar: (r: RespostasChecklist) => void;
};

export function PainelChecklist({ respostas, somenteLeitura, aoMudar }: Props) {
  const { estrutura, marcados } = respostas;

  const sugerido = useMemo(
    () => sugerirBiRads(coletarContribuicoes(estrutura, marcados)),
    [estrutura, marcados],
  );

  function estaMarcado(secaoId: string, itemId: string): boolean {
    return (marcados[secaoId] ?? []).some((r) => r.itemId === itemId);
  }

  function camposDe(secaoId: string, itemId: string): Record<string, string> {
    return (marcados[secaoId] ?? []).find((r) => r.itemId === itemId)?.campos ?? {};
  }

  function alternarItem(secao: SecaoChecklist, item: ItemChecklist) {
    if (somenteLeitura) return;
    const atual = marcados[secao.id] ?? [];
    const jaTem = atual.some((r) => r.itemId === item.id);
    let novo: RespostaItem[];
    if (secao.selecao === 'unica') {
      novo = jaTem ? [] : [{ itemId: item.id, campos: {} }];
    } else {
      novo = jaTem
        ? atual.filter((r) => r.itemId !== item.id)
        : [...atual, { itemId: item.id, campos: {} }];
    }
    aoMudar({ ...respostas, marcados: { ...marcados, [secao.id]: novo } });
  }

  function definirCampo(secaoId: string, itemId: string, chave: string, valor: string) {
    if (somenteLeitura) return;
    const atual = marcados[secaoId] ?? [];
    const novo = atual.map((r) =>
      r.itemId === itemId ? { ...r, campos: { ...r.campos, [chave]: valor } } : r,
    );
    aoMudar({ ...respostas, marcados: { ...marcados, [secaoId]: novo } });
  }

  function definirOverride(valor: string) {
    if (somenteLeitura) return;
    aoMudar({ ...respostas, biRadsFinal: normalizarBiRads(valor) });
  }

  const temSecaoBiRads = estrutura.secoes.some((s) => s.tipo === 'birads');

  return (
    <div className="space-y-4">
      {estrutura.secoes.map((secao) =>
        secao.tipo === 'birads' ? (
          <CalculadoraBiRadsBox
            key={secao.id}
            titulo={secao.titulo}
            sugerido={sugerido}
            override={respostas.biRadsFinal}
            somenteLeitura={somenteLeitura}
            aoMudarOverride={definirOverride}
          />
        ) : (
          <fieldset
            key={secao.id}
            className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm"
          >
            <legend className="px-1 text-xs font-semibold uppercase tracking-wide text-gray-500">
              {secao.titulo}
              {secao.selecao === 'unica' ? (
                <span className="ml-2 font-normal normal-case text-gray-400">(escolha uma)</span>
              ) : null}
            </legend>
            <ul className="mt-1 space-y-1.5">
              {secao.itens.map((item) => {
                const marcado = estaMarcado(secao.id, item.id);
                return (
                  <li key={item.id}>
                    <label className="flex cursor-pointer items-start gap-2 rounded-md px-1.5 py-1 hover:bg-gray-50">
                      <input
                        type={secao.selecao === 'unica' ? 'radio' : 'checkbox'}
                        name={`secao-${secao.id}`}
                        checked={marcado}
                        onChange={() => alternarItem(secao, item)}
                        disabled={somenteLeitura}
                        className="mt-1 h-4 w-4 flex-shrink-0 accent-primary-600"
                      />
                      <span className="min-w-0 flex-1 text-sm text-gray-800">
                        <span>{previa(item)}</span>
                        {item.birads ? (
                          <span
                            className={`ml-2 inline-block rounded border px-1.5 text-[11px] font-medium ${corBiRads(
                              item.birads,
                            )}`}
                          >
                            BI-RADS {item.birads}
                          </span>
                        ) : null}
                        {marcado && item.campos?.length ? (
                          <span className="mt-1.5 flex flex-wrap items-center gap-2">
                            {item.campos.map((campo) => (
                              <CampoInline
                                key={campo.chave}
                                campo={campo}
                                valor={camposDe(secao.id, item.id)[campo.chave] ?? ''}
                                somenteLeitura={somenteLeitura}
                                aoMudar={(v) => definirCampo(secao.id, item.id, campo.chave, v)}
                              />
                            ))}
                          </span>
                        ) : null}
                      </span>
                    </label>
                  </li>
                );
              })}
            </ul>
          </fieldset>
        ),
      )}

      {estrutura.calculadora === 'BI-RADS' && !temSecaoBiRads ? (
        <CalculadoraBiRadsBox
          titulo="Avaliação"
          sugerido={sugerido}
          override={respostas.biRadsFinal}
          somenteLeitura={somenteLeitura}
          aoMudarOverride={definirOverride}
        />
      ) : null}
    </div>
  );
}

/** Texto do item com `{chave}` trocado por um placeholder legível antes de marcar. */
function previa(item: ItemChecklist): string {
  return item.texto.replace(/\{(\w+)\}/g, '____');
}

function CampoInline({
  campo,
  valor,
  somenteLeitura,
  aoMudar,
}: {
  campo: CampoItem;
  valor: string;
  somenteLeitura?: boolean;
  aoMudar: (v: string) => void;
}) {
  const base =
    'rounded border border-gray-300 px-1.5 py-0.5 text-xs focus:border-primary-400 focus:outline-none focus:ring-1 focus:ring-primary-100 disabled:bg-gray-50';
  if (campo.tipo === 'opcao') {
    return (
      <span className="inline-flex items-center gap-1">
        <select
          value={valor}
          onChange={(e) => aoMudar(e.target.value)}
          disabled={somenteLeitura}
          className={base}
        >
          <option value="">{campo.chave}…</option>
          {(campo.opcoes ?? []).map((o) => (
            <option key={o} value={o}>
              {o}
            </option>
          ))}
        </select>
        {campo.sufixo ? <span className="text-xs text-gray-500">{campo.sufixo}</span> : null}
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1">
      <input
        type={campo.tipo === 'numero' ? 'number' : 'text'}
        value={valor}
        onChange={(e) => aoMudar(e.target.value)}
        disabled={somenteLeitura}
        placeholder={campo.chave}
        className={`${base} w-24`}
      />
      {campo.sufixo ? <span className="text-xs text-gray-500">{campo.sufixo}</span> : null}
    </span>
  );
}

function CalculadoraBiRadsBox({
  titulo,
  sugerido,
  override,
  somenteLeitura,
  aoMudarOverride,
}: {
  titulo: string;
  sugerido: string | null;
  override: string | null;
  somenteLeitura?: boolean;
  aoMudarOverride: (v: string) => void;
}) {
  const efetivo = override ?? sugerido;
  return (
    <div className="rounded-lg border border-primary-200 bg-primary-50/40 p-4 shadow-sm">
      <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-primary-700">
        <Calculator className="h-4 w-4" />
        {titulo} · BI-RADS
      </div>
      <div className="mt-2 flex flex-wrap items-center gap-3">
        <span className="text-sm text-gray-600">
          Sugerido pelo cálculo:{' '}
          {sugerido ? (
            <span
              className={`inline-block rounded border px-2 py-0.5 text-sm font-semibold ${corBiRads(sugerido)}`}
            >
              {rotuloBiRads(sugerido)}
            </span>
          ) : (
            <span className="text-gray-400">— (marque os achados)</span>
          )}
        </span>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          Categoria final:
          <select
            value={override ?? ''}
            onChange={(e) => aoMudarOverride(e.target.value)}
            disabled={somenteLeitura}
            className="rounded-md border border-gray-300 px-2 py-1 text-sm focus:border-primary-400 focus:outline-none focus:ring-1 focus:ring-primary-100 disabled:bg-gray-50"
          >
            <option value="">{sugerido ? `Usar sugerido (${sugerido})` : 'Usar sugerido'}</option>
            {CATEGORIAS_BIRADS.map((c) => (
              <option key={c} value={c}>
                {rotuloBiRads(c)}
              </option>
            ))}
          </select>
        </label>
        {efetivo ? (
          <span className="ml-auto text-sm text-gray-600">
            No laudo:{' '}
            <strong className={`rounded border px-2 py-0.5 font-semibold ${corBiRads(efetivo)}`}>
              BI-RADS {efetivo}
            </strong>
          </span>
        ) : null}
      </div>
      {override && override !== sugerido ? (
        <p className="mt-2 text-xs text-amber-700">
          A categoria final foi ajustada manualmente (o cálculo sugeria {sugerido ?? '—'}).
        </p>
      ) : null}
    </div>
  );
}
