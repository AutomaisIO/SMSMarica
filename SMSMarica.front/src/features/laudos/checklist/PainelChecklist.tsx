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
import {
  classificacoesDaEscala,
  corDmo,
  escalaDe,
  rotuloDmo,
  sugerirDmo,
} from './densitometria';
import type {
  CampoItem,
  ClassificacaoDmo,
  ColunaTabela,
  EscalaDmo,
  RespostaItem,
  RespostasChecklist,
  SecaoChecklist,
  ItemChecklist,
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

  const escala = escalaDe(respostas);
  const sugeridoDmo = useMemo(
    () => sugerirDmo(estrutura, marcados, escala),
    [estrutura, marcados, escala],
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

  /**
   * Célula de tabela. Diferente dos itens, uma linha não precisa ser "marcada"
   * antes: digitar já cria a resposta da linha.
   */
  function definirCelula(secaoId: string, linhaId: string, chave: string, valor: string) {
    if (somenteLeitura) return;
    const atual = marcados[secaoId] ?? [];
    const novo = atual.some((r) => r.itemId === linhaId)
      ? atual.map((r) => (r.itemId === linhaId ? { ...r, campos: { ...r.campos, [chave]: valor } } : r))
      : [...atual, { itemId: linhaId, campos: { [chave]: valor } }];
    aoMudar({ ...respostas, marcados: { ...marcados, [secaoId]: novo } });
  }

  function definirOverride(valor: string) {
    if (somenteLeitura) return;
    aoMudar({ ...respostas, biRadsFinal: normalizarBiRads(valor) });
  }

  function definirOverrideDmo(valor: string) {
    if (somenteLeitura) return;
    aoMudar({ ...respostas, dmoFinal: (valor || null) as ClassificacaoDmo | null });
  }

  function definirEscala(valor: EscalaDmo) {
    if (somenteLeitura) return;
    // Trocar de escala invalida um override feito na escala anterior
    // (osteopenia não existe em Z-score).
    aoMudar({ ...respostas, escalaDmo: valor, dmoFinal: null });
  }

  const temSecaoBiRads = estrutura.secoes.some((s) => s.tipo === 'birads');
  const temSecaoDmo = estrutura.secoes.some((s) => s.tipo === 'oms-dmo');

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
        ) : secao.tipo === 'tabela' ? (
          <GradeTabela
            key={secao.id}
            secao={secao}
            respostas={marcados[secao.id] ?? []}
            somenteLeitura={somenteLeitura}
            aoMudarCelula={(linhaId, chave, valor) =>
              definirCelula(secao.id, linhaId, chave, valor)
            }
          />
        ) : secao.tipo === 'texto-fixo' ? (
          <fieldset key={secao.id} className="rounded-lg border border-gray-200 bg-gray-50/60 p-4">
            <legend className="px-1 text-xs font-semibold uppercase tracking-wide text-gray-500">
              {secao.titulo}
              <span className="ml-2 font-normal normal-case text-gray-400">
                (texto fixo — sempre entra no laudo)
              </span>
            </legend>
            <ul className="mt-1 space-y-1 text-sm text-gray-600">
              {secao.itens.map((item) => (
                <li key={item.id}>{item.texto}</li>
              ))}
            </ul>
          </fieldset>
        ) : secao.tipo === 'oms-dmo' ? (
          <CalculadoraDmoBox
            key={secao.id}
            titulo={secao.titulo}
            escala={escala}
            sugerido={sugeridoDmo}
            override={respostas.dmoFinal ?? null}
            somenteLeitura={somenteLeitura}
            aoMudarEscala={definirEscala}
            aoMudarOverride={definirOverrideDmo}
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

      {estrutura.calculadora === 'OMS-DMO' && !temSecaoDmo ? (
        <CalculadoraDmoBox
          titulo="Diagnóstico"
          escala={escala}
          sugerido={sugeridoDmo}
          override={respostas.dmoFinal ?? null}
          somenteLeitura={somenteLeitura}
          aoMudarEscala={definirEscala}
          aoMudarOverride={definirOverrideDmo}
        />
      ) : null}
    </div>
  );
}

/**
 * Grade de medidas: colunas tipadas × linhas fixas (os sítios). Colunas `fixa`
 * são rótulo; as demais são preenchidas pela profissional.
 */
function GradeTabela({
  secao,
  respostas,
  somenteLeitura,
  aoMudarCelula,
}: {
  secao: SecaoChecklist;
  respostas: RespostaItem[];
  somenteLeitura?: boolean;
  aoMudarCelula: (linhaId: string, chave: string, valor: string) => void;
}) {
  const colunas = secao.colunas ?? [];
  const linhas = secao.linhas ?? [];

  return (
    <fieldset className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <legend className="px-1 text-xs font-semibold uppercase tracking-wide text-gray-500">
        {secao.titulo}
        <span className="ml-2 font-normal normal-case text-gray-400">
          (linha em branco não entra no laudo)
        </span>
      </legend>

      {colunas.length === 0 || linhas.length === 0 ? (
        <p className="mt-2 text-sm text-gray-400">Tabela sem colunas ou linhas definidas.</p>
      ) : (
        <div className="mt-2 overflow-x-auto">
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr>
                {colunas.map((c) => (
                  <th
                    key={c.chave}
                    className="border border-gray-200 bg-gray-50 px-2 py-1.5 text-left text-xs font-semibold text-gray-600"
                    style={c.larguraPct ? { width: `${c.larguraPct}%` } : undefined}
                  >
                    {c.titulo}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {linhas.map((linha) => {
                const campos = respostas.find((r) => r.itemId === linha.id)?.campos ?? {};
                return (
                  <tr key={linha.id}>
                    {colunas.map((c) => (
                      <td key={c.chave} className="border border-gray-200 px-2 py-1">
                        {c.fixa ? (
                          <span className="text-gray-800">{linha.fixos[c.chave] ?? ''}</span>
                        ) : (
                          <CelulaEditavel
                            coluna={c}
                            valor={campos[c.chave] ?? ''}
                            somenteLeitura={somenteLeitura}
                            aoMudar={(v) => aoMudarCelula(linha.id, c.chave, v)}
                          />
                        )}
                      </td>
                    ))}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </fieldset>
  );
}

function CelulaEditavel({
  coluna,
  valor,
  somenteLeitura,
  aoMudar,
}: {
  coluna: ColunaTabela;
  valor: string;
  somenteLeitura?: boolean;
  aoMudar: (v: string) => void;
}) {
  return (
    <span className="flex items-center gap-1">
      <input
        type="text"
        // Vírgula decimal e sinal negativo são o padrão do laudo ("-2,5"); com
        // type="number" o browser recusa a vírgula em boa parte dos locales.
        inputMode={coluna.tipo === 'numero' ? 'decimal' : 'text'}
        value={valor}
        onChange={(e) => aoMudar(e.target.value)}
        disabled={somenteLeitura}
        placeholder={coluna.tipo === 'numero' ? '—' : ''}
        className="w-full min-w-[56px] rounded border border-gray-300 px-1.5 py-0.5 text-sm focus:border-primary-400 focus:outline-none focus:ring-1 focus:ring-primary-100 disabled:bg-gray-50"
      />
      {coluna.sufixo ? <span className="text-xs text-gray-500">{coluna.sufixo}</span> : null}
    </span>
  );
}

function CalculadoraDmoBox({
  titulo,
  escala,
  sugerido,
  override,
  somenteLeitura,
  aoMudarEscala,
  aoMudarOverride,
}: {
  titulo: string;
  escala: EscalaDmo;
  sugerido: { classificacao: ClassificacaoDmo; valor: number; sitio: string } | null;
  override: ClassificacaoDmo | null;
  somenteLeitura?: boolean;
  aoMudarEscala: (e: EscalaDmo) => void;
  aoMudarOverride: (v: string) => void;
}) {
  const efetivo = override ?? sugerido?.classificacao ?? null;

  return (
    <div className="rounded-lg border border-primary-200 bg-primary-50/40 p-4 shadow-sm">
      <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-primary-700">
        <Calculator className="h-4 w-4" />
        {titulo} · OMS / SBDens / ISCD
      </div>

      <label className="mt-2 flex flex-wrap items-center gap-2 text-sm text-gray-700">
        Parâmetro:
        <select
          value={escala}
          onChange={(e) => aoMudarEscala(e.target.value as EscalaDmo)}
          disabled={somenteLeitura}
          className="rounded-md border border-gray-300 px-2 py-1 text-sm focus:border-primary-400 focus:outline-none focus:ring-1 focus:ring-primary-100 disabled:bg-gray-50"
        >
          <option value="T">T-score — perimenopausa em diante e homens ≥ 50 anos</option>
          <option value="Z">Z-score — crianças, pré-menopausa e homens &lt; 50 anos</option>
        </select>
      </label>

      <div className="mt-2 flex flex-wrap items-center gap-3">
        <span className="text-sm text-gray-600">
          Sugerido pelo cálculo:{' '}
          {sugerido ? (
            <span
              className={`inline-block rounded border px-2 py-0.5 text-sm font-semibold ${corDmo(
                sugerido.classificacao,
              )}`}
            >
              {rotuloDmo(sugerido.classificacao)}
            </span>
          ) : (
            <span className="text-gray-400">— (preencha os scores na tabela)</span>
          )}
        </span>

        <label className="flex items-center gap-2 text-sm text-gray-700">
          Diagnóstico final:
          <select
            value={override ?? ''}
            onChange={(e) => aoMudarOverride(e.target.value)}
            disabled={somenteLeitura}
            className="rounded-md border border-gray-300 px-2 py-1 text-sm focus:border-primary-400 focus:outline-none focus:ring-1 focus:ring-primary-100 disabled:bg-gray-50"
          >
            <option value="">
              {sugerido ? `Usar sugerido (${rotuloDmo(sugerido.classificacao)})` : 'Usar sugerido'}
            </option>
            {classificacoesDaEscala(escala).map((c) => (
              <option key={c} value={c}>
                {rotuloDmo(c)}
              </option>
            ))}
          </select>
        </label>
      </div>

      {sugerido ? (
        <p className="mt-2 text-xs text-gray-500">
          Menor valor entre os sítios diagnósticos: {escala}-score{' '}
          {sugerido.valor.toFixed(1).replace('.', ',')} em {sugerido.sitio}.
        </p>
      ) : null}

      {override && override !== sugerido?.classificacao ? (
        <p className="mt-1 text-xs text-amber-700">
          O diagnóstico final foi ajustado manualmente (o cálculo sugeria{' '}
          {sugerido ? rotuloDmo(sugerido.classificacao) : '—'}).
        </p>
      ) : null}

      {efetivo ? (
        <p className="mt-2 text-sm text-gray-600">
          No laudo:{' '}
          <strong className={`rounded border px-2 py-0.5 font-semibold ${corDmo(efetivo)}`}>
            {rotuloDmo(efetivo)}
          </strong>
        </p>
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
