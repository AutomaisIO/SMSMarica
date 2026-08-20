import { ChevronDown, ChevronUp, Plus, Trash2 } from 'lucide-react';
import { CATEGORIAS_BIRADS, rotuloBiRads } from '@/features/laudos/checklist/birads';
import type {
  Calculadora,
  CampoItem,
  CategoriaBiRads,
  ColunaTabela,
  EstruturaChecklist,
  ItemChecklist,
  SecaoChecklist,
  TipoCampo,
} from '@/features/laudos/checklist/types';

type Props = {
  estrutura: EstruturaChecklist;
  aoMudar: (e: EstruturaChecklist) => void;
};

function novoId(): string {
  return typeof crypto !== 'undefined' && crypto.randomUUID
    ? crypto.randomUUID()
    : `id-${Math.floor(Date.now() * Math.random())}`;
}

function mover<T>(arr: T[], de: number, para: number): T[] {
  if (para < 0 || para >= arr.length) return arr;
  const copia = [...arr];
  const [x] = copia.splice(de, 1);
  copia.splice(para, 0, x);
  return copia;
}

export function ConstrutorEstrutura({ estrutura, aoMudar }: Props) {
  function setSecoes(secoes: SecaoChecklist[]) {
    aoMudar({ ...estrutura, secoes });
  }

  function atualizarSecao(idx: number, patch: Partial<SecaoChecklist>) {
    setSecoes(estrutura.secoes.map((s, i) => (i === idx ? { ...s, ...patch } : s)));
  }

  function addSecao() {
    setSecoes([
      ...estrutura.secoes,
      { id: novoId(), titulo: 'NOVA SEÇÃO', selecao: 'multipla', itens: [] },
    ]);
  }

  function addSecaoBiRads() {
    setSecoes([
      ...estrutura.secoes,
      { id: novoId(), titulo: 'AVALIAÇÃO', selecao: 'unica', tipo: 'birads', itens: [] },
    ]);
  }

  function addSecaoTextoFixo() {
    setSecoes([
      ...estrutura.secoes,
      { id: novoId(), titulo: 'OBSERVAÇÕES', selecao: 'multipla', tipo: 'texto-fixo', itens: [] },
    ]);
  }

  function addSecaoDmo() {
    setSecoes([
      ...estrutura.secoes,
      { id: novoId(), titulo: 'DIAGNÓSTICO', selecao: 'unica', tipo: 'oms-dmo', itens: [] },
    ]);
  }

  /**
   * Nasce já no formato da densitometria — é o caso que motivou a seção e serve
   * de esqueleto para qualquer outra tabela de medidas (basta renomear).
   */
  function addSecaoTabela() {
    setSecoes([
      ...estrutura.secoes,
      {
        id: novoId(),
        titulo: 'RELATÓRIO DA ANÁLISE RESUMIDO',
        selecao: 'multipla',
        tipo: 'tabela',
        itens: [],
        colunas: [
          { chave: 'regiao', titulo: 'Região estudada', tipo: 'texto', fixa: true, larguraPct: 17 },
          { chave: 'sitio', titulo: 'Sítio', tipo: 'texto', fixa: true, larguraPct: 14 },
          { chave: 'dmo', titulo: 'DMO (g/cm²)', tipo: 'numero', larguraPct: 14 },
          { chave: 'tscore', titulo: 'T-Score', tipo: 'numero', larguraPct: 12, papel: 'tscore' },
          { chave: 'pr', titulo: 'PR (%)', tipo: 'numero', larguraPct: 11 },
          { chave: 'zscore', titulo: 'Z-Score', tipo: 'numero', larguraPct: 12, papel: 'zscore' },
          { chave: 'am', titulo: 'AM (%)', tipo: 'numero', larguraPct: 11 },
        ],
        linhas: [
          { id: novoId(), fixos: { regiao: 'Coluna Lombar', sitio: 'L1 a L4' }, diagnostica: true },
          { id: novoId(), fixos: { regiao: 'Fêmur Proximal', sitio: 'Fêmur Total' }, diagnostica: true },
          { id: novoId(), fixos: { regiao: 'Fêmur Proximal', sitio: 'Colo Femoral' }, diagnostica: true },
          // Rádio 33% só é diagnóstico em condições específicas (SBDens/ISCD).
          { id: novoId(), fixos: { regiao: 'Antebraço', sitio: 'Rádio 33%' }, diagnostica: false },
        ],
      },
    ]);
  }

  return (
    <div className="space-y-3">
      <label className="flex flex-wrap items-center gap-2 text-sm text-gray-700">
        Calculadora do template:
        <select
          value={estrutura.calculadora ?? ''}
          onChange={(e) =>
            aoMudar({ ...estrutura, calculadora: (e.target.value || null) as Calculadora })
          }
          className="rounded border border-gray-300 px-2 py-1 text-sm"
        >
          <option value="">Nenhuma (só texto)</option>
          <option value="BI-RADS">BI-RADS (mamografia)</option>
          <option value="OMS-DMO">OMS / DMO (densitometria óssea)</option>
        </select>
      </label>
      {estrutura.secoes.map((secao, idx) => (
        <div key={secao.id} className="rounded-lg border border-gray-200 bg-gray-50/60 p-3">
          <div className="flex flex-wrap items-center gap-2">
            <input
              value={secao.titulo}
              onChange={(e) => atualizarSecao(idx, { titulo: e.target.value })}
              className="flex-1 min-w-[200px] rounded border border-gray-300 px-2 py-1 text-sm font-semibold uppercase"
              placeholder="Título da seção"
            />
            {secao.tipo === 'birads' ? (
              <span className="rounded bg-primary-100 px-2 py-1 text-xs font-medium text-primary-700">
                Calculadora BI-RADS
              </span>
            ) : secao.tipo === 'oms-dmo' ? (
              <span className="rounded bg-primary-100 px-2 py-1 text-xs font-medium text-primary-700">
                Calculadora OMS / DMO
              </span>
            ) : secao.tipo === 'tabela' ? (
              <span className="rounded bg-sky-100 px-2 py-1 text-xs font-medium text-sky-700">
                Tabela de medidas
              </span>
            ) : secao.tipo === 'texto-fixo' ? (
              <span className="rounded bg-gray-200 px-2 py-1 text-xs font-medium text-gray-700">
                Texto fixo
              </span>
            ) : (
              <select
                value={secao.selecao}
                onChange={(e) =>
                  atualizarSecao(idx, { selecao: e.target.value as SecaoChecklist['selecao'] })
                }
                className="rounded border border-gray-300 px-2 py-1 text-xs"
                title="Tipo de seleção"
              >
                <option value="multipla">Múltipla (checkbox)</option>
                <option value="unica">Única (radio)</option>
              </select>
            )}
            <div className="flex items-center gap-1">
              <BotaoIcone titulo="Subir" onClick={() => setSecoes(mover(estrutura.secoes, idx, idx - 1))}>
                <ChevronUp className="h-4 w-4" />
              </BotaoIcone>
              <BotaoIcone titulo="Descer" onClick={() => setSecoes(mover(estrutura.secoes, idx, idx + 1))}>
                <ChevronDown className="h-4 w-4" />
              </BotaoIcone>
              <BotaoIcone
                titulo="Remover seção"
                onClick={() => setSecoes(estrutura.secoes.filter((_, i) => i !== idx))}
              >
                <Trash2 className="h-4 w-4 text-red-500" />
              </BotaoIcone>
            </div>
          </div>

          {secao.tipo === 'birads' ? (
            <p className="mt-2 text-xs text-gray-500">
              Esta seção exibe a categoria BI-RADS calculada (e editável) no laudo. Não tem itens.
            </p>
          ) : secao.tipo === 'oms-dmo' ? (
            <p className="mt-2 text-xs text-gray-500">
              Esta seção exibe o diagnóstico de densitometria calculado (e editável) no laudo —
              o menor T-score (ou Z-score) entre os sítios marcados como diagnósticos na tabela.
              Não tem itens.
            </p>
          ) : secao.tipo === 'tabela' ? (
            <EditorTabela
              secao={secao}
              aoMudar={(patch) => atualizarSecao(idx, patch)}
            />
          ) : secao.tipo === 'texto-fixo' ? (
            <>
              <p className="mt-2 text-xs text-gray-500">
                Todas as frases abaixo entram no laudo sempre, sem marcação (a médica não escolhe).
              </p>
              <EditorItens itens={secao.itens} aoMudar={(itens) => atualizarSecao(idx, { itens })} />
            </>
          ) : (
            <EditorItens
              itens={secao.itens}
              aoMudar={(itens) => atualizarSecao(idx, { itens })}
            />
          )}
        </div>
      ))}

      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          onClick={addSecao}
          className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
        >
          <Plus className="h-4 w-4" /> Adicionar seção
        </button>
        <button
          type="button"
          onClick={addSecaoTabela}
          className="inline-flex items-center gap-1 rounded-md border border-sky-300 bg-sky-50 px-3 py-1.5 text-sm font-medium text-sky-700 hover:bg-sky-100"
        >
          <Plus className="h-4 w-4" /> Seção de TABELA (medidas)
        </button>
        <button
          type="button"
          onClick={addSecaoTextoFixo}
          className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
        >
          <Plus className="h-4 w-4" /> Seção de TEXTO FIXO
        </button>
        {estrutura.calculadora === 'BI-RADS' && !estrutura.secoes.some((s) => s.tipo === 'birads') ? (
          <button
            type="button"
            onClick={addSecaoBiRads}
            className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-3 py-1.5 text-sm font-medium text-primary-700 hover:bg-primary-100"
          >
            <Plus className="h-4 w-4" /> Seção de AVALIAÇÃO (BI-RADS)
          </button>
        ) : null}
        {estrutura.calculadora === 'OMS-DMO' && !estrutura.secoes.some((s) => s.tipo === 'oms-dmo') ? (
          <button
            type="button"
            onClick={addSecaoDmo}
            className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-3 py-1.5 text-sm font-medium text-primary-700 hover:bg-primary-100"
          >
            <Plus className="h-4 w-4" /> Seção de DIAGNÓSTICO (OMS / DMO)
          </button>
        ) : null}
      </div>
    </div>
  );
}

function EditorItens({
  itens,
  aoMudar,
}: {
  itens: ItemChecklist[];
  aoMudar: (itens: ItemChecklist[]) => void;
}) {
  function atualizar(idx: number, patch: Partial<ItemChecklist>) {
    aoMudar(itens.map((it, i) => (i === idx ? { ...it, ...patch } : it)));
  }

  return (
    <div className="mt-2 space-y-2">
      {itens.map((item, idx) => (
        <div key={item.id} className="rounded-md border border-gray-200 bg-white p-2">
          <div className="flex items-start gap-2">
            <textarea
              value={item.texto}
              onChange={(e) => atualizar(idx, { texto: e.target.value })}
              rows={2}
              className="flex-1 rounded border border-gray-300 px-2 py-1 text-sm"
              placeholder="Frase do laudo. Use {chave} para campos preenchíveis (ex.: medindo {medida} cm)."
            />
            <div className="flex flex-col items-stretch gap-1">
              <label className="text-[11px] text-gray-500">BI-RADS</label>
              <select
                value={item.birads ?? ''}
                onChange={(e) =>
                  atualizar(idx, { birads: (e.target.value || null) as CategoriaBiRads | null })
                }
                className="rounded border border-gray-300 px-1.5 py-1 text-xs"
                title="Contribuição BI-RADS deste item"
              >
                <option value="">— (não pontua)</option>
                {CATEGORIAS_BIRADS.map((c) => (
                  <option key={c} value={c}>
                    {rotuloBiRads(c)}
                  </option>
                ))}
              </select>
            </div>
            <div className="flex flex-col gap-1">
              <BotaoIcone titulo="Subir" onClick={() => aoMudar(mover(itens, idx, idx - 1))}>
                <ChevronUp className="h-4 w-4" />
              </BotaoIcone>
              <BotaoIcone titulo="Descer" onClick={() => aoMudar(mover(itens, idx, idx + 1))}>
                <ChevronDown className="h-4 w-4" />
              </BotaoIcone>
              <BotaoIcone
                titulo="Remover item"
                onClick={() => aoMudar(itens.filter((_, i) => i !== idx))}
              >
                <Trash2 className="h-4 w-4 text-red-500" />
              </BotaoIcone>
            </div>
          </div>
          <EditorCampos
            campos={item.campos ?? []}
            aoMudar={(campos) => atualizar(idx, { campos: campos.length ? campos : undefined })}
          />
        </div>
      ))}
      <button
        type="button"
        onClick={() => aoMudar([...itens, { id: novoId(), texto: '' }])}
        className="inline-flex items-center gap-1 text-xs font-medium text-primary-600 hover:text-primary-700"
      >
        <Plus className="h-3.5 w-3.5" /> Adicionar frase
      </button>
    </div>
  );
}

/**
 * Editor de uma seção `tabela`: as COLUNAS (o que se mede) e as LINHAS (onde se
 * mede). O preenchimento em si acontece no laudo, no PainelChecklist.
 */
function EditorTabela({
  secao,
  aoMudar,
}: {
  secao: SecaoChecklist;
  aoMudar: (patch: Partial<SecaoChecklist>) => void;
}) {
  const colunas = secao.colunas ?? [];
  const linhas = secao.linhas ?? [];

  function setColunas(novas: ColunaTabela[]) {
    aoMudar({ colunas: novas });
  }

  function atualizarColuna(idx: number, patch: Partial<ColunaTabela>) {
    setColunas(colunas.map((c, i) => (i === idx ? { ...c, ...patch } : c)));
  }

  function removerColuna(idx: number) {
    const chave = colunas[idx]?.chave;
    setColunas(colunas.filter((_, i) => i !== idx));
    // Limpa o rótulo órfão que a coluna deixaria nas linhas.
    if (chave) {
      aoMudar({
        colunas: colunas.filter((_, i) => i !== idx),
        linhas: linhas.map((l) => {
          const { [chave]: _removido, ...resto } = l.fixos;
          return { ...l, fixos: resto };
        }),
      });
    }
  }

  const somaLarguras = colunas.reduce((t, c) => t + (c.larguraPct ?? 0), 0);

  return (
    <div className="mt-2 space-y-3">
      <div>
        <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500">Colunas</p>
        <div className="mt-1 space-y-1.5">
          {colunas.map((coluna, idx) => (
            <div key={coluna.chave} className="flex flex-wrap items-center gap-1.5 rounded-md border border-gray-200 bg-white p-2">
              <input
                value={coluna.titulo}
                onChange={(e) => atualizarColuna(idx, { titulo: e.target.value })}
                className="min-w-[130px] flex-1 rounded border border-gray-300 px-2 py-1 text-sm"
                placeholder="Título da coluna"
              />
              <input
                value={coluna.chave}
                onChange={(e) => atualizarColuna(idx, { chave: e.target.value.trim() })}
                className="w-24 rounded border border-gray-300 px-2 py-1 font-mono text-xs"
                placeholder="chave"
                title="Chave interna (identifica o valor nas respostas)"
              />
              <select
                value={coluna.tipo}
                onChange={(e) => atualizarColuna(idx, { tipo: e.target.value as ColunaTabela['tipo'] })}
                className="rounded border border-gray-300 px-1.5 py-1 text-xs"
                title="Tipo do valor"
              >
                <option value="numero">Número</option>
                <option value="texto">Texto</option>
              </select>
              <label className="flex items-center gap-1 text-xs text-gray-600" title="Coluna de rótulo: o valor vem da linha, não é preenchido no laudo">
                <input
                  type="checkbox"
                  checked={!!coluna.fixa}
                  onChange={(e) => atualizarColuna(idx, { fixa: e.target.checked || undefined })}
                  className="h-3.5 w-3.5 accent-primary-600"
                />
                rótulo
              </label>
              <select
                value={coluna.papel ?? ''}
                onChange={(e) =>
                  atualizarColuna(idx, { papel: (e.target.value || undefined) as ColunaTabela['papel'] })
                }
                className="rounded border border-gray-300 px-1.5 py-1 text-xs"
                title="O que a calculadora lê desta coluna"
              >
                <option value="">— não calcula</option>
                <option value="tscore">T-score</option>
                <option value="zscore">Z-score</option>
              </select>
              <span className="inline-flex items-center gap-1 text-xs text-gray-500">
                <input
                  type="number"
                  min={1}
                  max={100}
                  value={coluna.larguraPct ?? ''}
                  onChange={(e) =>
                    atualizarColuna(idx, {
                      larguraPct: e.target.value ? Number(e.target.value) : undefined,
                    })
                  }
                  className="w-14 rounded border border-gray-300 px-1.5 py-1 text-xs"
                  placeholder="larg."
                  title="Largura da coluna no PDF (%)"
                />
                %
              </span>
              <div className="flex items-center gap-1">
                <BotaoIcone titulo="Mover para a esquerda" onClick={() => setColunas(mover(colunas, idx, idx - 1))}>
                  <ChevronUp className="h-4 w-4" />
                </BotaoIcone>
                <BotaoIcone titulo="Mover para a direita" onClick={() => setColunas(mover(colunas, idx, idx + 1))}>
                  <ChevronDown className="h-4 w-4" />
                </BotaoIcone>
                <BotaoIcone titulo="Remover coluna" onClick={() => removerColuna(idx)}>
                  <Trash2 className="h-4 w-4 text-red-500" />
                </BotaoIcone>
              </div>
            </div>
          ))}
        </div>
        <div className="mt-1 flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={() =>
              setColunas([...colunas, { chave: `col${colunas.length + 1}`, titulo: '', tipo: 'numero' }])
            }
            className="inline-flex items-center gap-1 text-xs font-medium text-primary-600 hover:text-primary-700"
          >
            <Plus className="h-3.5 w-3.5" /> Adicionar coluna
          </button>
          {somaLarguras > 0 && Math.abs(somaLarguras - 100) > 1 ? (
            <span className="text-[11px] text-amber-700">
              Larguras somam {somaLarguras}% — o ideal é 100% (valores fora disso viram proporção).
            </span>
          ) : null}
        </div>
      </div>

      <div>
        <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500">
          Linhas (sítios medidos)
        </p>
        <div className="mt-1 space-y-1.5">
          {linhas.map((linha, idx) => (
            <div key={linha.id} className="flex flex-wrap items-center gap-1.5 rounded-md border border-gray-200 bg-white p-2">
              {colunas
                .filter((c) => c.fixa)
                .map((c) => (
                  <input
                    key={c.chave}
                    value={linha.fixos[c.chave] ?? ''}
                    onChange={(e) =>
                      aoMudar({
                        linhas: linhas.map((l, i) =>
                          i === idx ? { ...l, fixos: { ...l.fixos, [c.chave]: e.target.value } } : l,
                        ),
                      })
                    }
                    className="min-w-[120px] flex-1 rounded border border-gray-300 px-2 py-1 text-sm"
                    placeholder={c.titulo}
                  />
                ))}
              <label
                className="flex items-center gap-1 text-xs text-gray-600"
                title="Sítio elegível como parâmetro do diagnóstico (coluna lombar e fêmur; rádio 33% só em condições específicas)"
              >
                <input
                  type="checkbox"
                  checked={!!linha.diagnostica}
                  onChange={(e) =>
                    aoMudar({
                      linhas: linhas.map((l, i) =>
                        i === idx ? { ...l, diagnostica: e.target.checked || undefined } : l,
                      ),
                    })
                  }
                  className="h-3.5 w-3.5 accent-primary-600"
                />
                diagnóstico
              </label>
              <div className="flex items-center gap-1">
                <BotaoIcone titulo="Subir" onClick={() => aoMudar({ linhas: mover(linhas, idx, idx - 1) })}>
                  <ChevronUp className="h-4 w-4" />
                </BotaoIcone>
                <BotaoIcone titulo="Descer" onClick={() => aoMudar({ linhas: mover(linhas, idx, idx + 1) })}>
                  <ChevronDown className="h-4 w-4" />
                </BotaoIcone>
                <BotaoIcone
                  titulo="Remover linha"
                  onClick={() => aoMudar({ linhas: linhas.filter((_, i) => i !== idx) })}
                >
                  <Trash2 className="h-4 w-4 text-red-500" />
                </BotaoIcone>
              </div>
            </div>
          ))}
        </div>
        <button
          type="button"
          onClick={() => aoMudar({ linhas: [...linhas, { id: novoId(), fixos: {} }] })}
          className="mt-1 inline-flex items-center gap-1 text-xs font-medium text-primary-600 hover:text-primary-700"
        >
          <Plus className="h-3.5 w-3.5" /> Adicionar linha
        </button>
      </div>
    </div>
  );
}

function EditorCampos({
  campos,
  aoMudar,
}: {
  campos: CampoItem[];
  aoMudar: (campos: CampoItem[]) => void;
}) {
  function atualizar(idx: number, patch: Partial<CampoItem>) {
    aoMudar(campos.map((c, i) => (i === idx ? { ...c, ...patch } : c)));
  }

  if (campos.length === 0) {
    return (
      <button
        type="button"
        onClick={() => aoMudar([{ chave: 'campo', tipo: 'texto' }])}
        className="mt-1.5 text-[11px] font-medium text-gray-500 hover:text-gray-700"
      >
        + campo preenchível
      </button>
    );
  }

  return (
    <div className="mt-2 space-y-1.5 rounded border border-dashed border-gray-200 p-2">
      <div className="text-[11px] font-semibold uppercase tracking-wide text-gray-400">
        Campos preenchíveis (use {'{chave}'} no texto)
      </div>
      {campos.map((campo, idx) => (
        <div key={idx} className="flex flex-wrap items-center gap-1.5">
          <input
            value={campo.chave}
            onChange={(e) => atualizar(idx, { chave: e.target.value.replace(/\s/g, '') })}
            placeholder="chave"
            className="w-24 rounded border border-gray-300 px-1.5 py-0.5 text-xs"
          />
          <select
            value={campo.tipo}
            onChange={(e) => atualizar(idx, { tipo: e.target.value as TipoCampo })}
            className="rounded border border-gray-300 px-1.5 py-0.5 text-xs"
          >
            <option value="texto">Texto</option>
            <option value="numero">Número</option>
            <option value="opcao">Opções</option>
          </select>
          {campo.tipo === 'opcao' ? (
            <input
              value={(campo.opcoes ?? []).join(', ')}
              onChange={(e) =>
                atualizar(idx, {
                  opcoes: e.target.value
                    .split(',')
                    .map((o) => o.trim())
                    .filter(Boolean),
                })
              }
              placeholder="opção 1, opção 2, …"
              className="flex-1 min-w-[160px] rounded border border-gray-300 px-1.5 py-0.5 text-xs"
            />
          ) : null}
          <input
            value={campo.sufixo ?? ''}
            onChange={(e) => atualizar(idx, { sufixo: e.target.value || undefined })}
            placeholder="sufixo"
            className="w-16 rounded border border-gray-300 px-1.5 py-0.5 text-xs"
          />
          <BotaoIcone titulo="Remover campo" onClick={() => aoMudar(campos.filter((_, i) => i !== idx))}>
            <Trash2 className="h-3.5 w-3.5 text-red-500" />
          </BotaoIcone>
        </div>
      ))}
      <button
        type="button"
        onClick={() => aoMudar([...campos, { chave: '', tipo: 'texto' }])}
        className="text-[11px] font-medium text-primary-600 hover:text-primary-700"
      >
        + campo
      </button>
    </div>
  );
}

function BotaoIcone({
  titulo,
  onClick,
  children,
}: {
  titulo: string;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      title={titulo}
      onClick={onClick}
      className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-700"
    >
      {children}
    </button>
  );
}
