import { ChevronDown, ChevronUp, Plus, Trash2 } from 'lucide-react';
import { CATEGORIAS_BIRADS, rotuloBiRads } from '@/features/laudos/checklist/birads';
import type {
  CampoItem,
  CategoriaBiRads,
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

  return (
    <div className="space-y-3">
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
        {!estrutura.secoes.some((s) => s.tipo === 'birads') ? (
          <button
            type="button"
            onClick={addSecaoBiRads}
            className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-3 py-1.5 text-sm font-medium text-primary-700 hover:bg-primary-100"
          >
            <Plus className="h-4 w-4" /> Seção de AVALIAÇÃO (BI-RADS)
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
