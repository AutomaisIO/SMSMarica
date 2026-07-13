import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import {
  useAtualizarRespostaRapida,
  useCriarRespostaRapida,
  useTagsAutomaticas,
} from '@/features/respostas-rapidas/api/queries';
import { useListarUnidades } from '@/features/unidades/api/queries';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  ROTULO_TIPO_CAMPO,
  type RespostaRapida,
  type RespostaRapidaCampo,
  type TipoCampoResposta,
} from '@/features/respostas-rapidas/types';

type Props = {
  /** null = criando. */
  resposta: RespostaRapida | null;
  onFechar: () => void;
};

const TIPOS: TipoCampoResposta[] = ['Texto', 'Data', 'Numero'];

/** Tags {{...}} presentes no texto, na ordem, sem repetir. */
function tagsDoCorpo(corpo: string): string[] {
  const achadas = corpo.match(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g) ?? [];
  const nomes = achadas.map((t) => t.replace(/[{}\s]/g, ''));
  return [...new Set(nomes)];
}

export function RespostaRapidaFormDialog({ resposta, onFechar }: Props) {
  const { data: tagsAutomaticas } = useTagsAutomaticas();
  const { data: unidades } = useListarUnidades();
  const criar = useCriarRespostaRapida();
  const atualizar = useAtualizarRespostaRapida();

  const [titulo, setTitulo] = useState(resposta?.titulo ?? '');
  const [categoria, setCategoria] = useState(resposta?.categoria ?? '');
  const [corpo, setCorpo] = useState(resposta?.corpo ?? '');
  const [unidadeId, setUnidadeId] = useState(resposta?.unidadeId ?? '');
  const [ativo, setAtivo] = useState(resposta?.ativo ?? true);
  const [ordem, setOrdem] = useState(resposta?.ordem ?? 0);
  const [campos, setCampos] = useState<RespostaRapidaCampo[]>(resposta?.campos ?? []);
  const [erro, setErro] = useState<string | null>(null);

  const nomesAutomaticos = useMemo(
    () => new Set((tagsAutomaticas ?? []).map((t) => t.nome.toLowerCase())),
    [tagsAutomaticas],
  );

  // As variáveis manuais SÃO as tags do texto que não são automáticas: em vez de o usuário
  // declarar campos e o backend recusar por divergência, a lista se deriva do próprio corpo.
  // O tipo escolhido em cada uma é preservado enquanto a tag continuar no texto.
  useEffect(() => {
    if (!tagsAutomaticas) return; // sem o catálogo, ainda não sabemos o que é automático
    const manuais = tagsDoCorpo(corpo).filter((t) => !nomesAutomaticos.has(t.toLowerCase()));

    setCampos((atuais) =>
      manuais.map((nome, i) => {
        const existente = atuais.find((c) => c.nome.toLowerCase() === nome.toLowerCase());
        return existente
          ? { ...existente, nome, ordem: i }
          : { nome, rotulo: null, tipo: 'Texto' as TipoCampoResposta, ordem: i };
      }),
    );
  }, [corpo, nomesAutomaticos, tagsAutomaticas]);

  function inserirTag(nome: string) {
    setCorpo((c) => `${c}{{${nome}}}`);
  }

  async function salvar() {
    setErro(null);
    const payload = {
      titulo: titulo.trim(),
      corpo: corpo.trim(),
      categoria: categoria.trim() || null,
      unidadeId: unidadeId || null,
      ativo,
      ordem,
      campos,
    };
    try {
      if (resposta) await atualizar.mutateAsync({ id: resposta.id, payload });
      else await criar.mutateAsync(payload);
      onFechar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const salvando = criar.isPending || atualizar.isPending;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 p-4">
      <div className="flex max-h-[90vh] w-full max-w-2xl flex-col rounded-lg bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3">
          <h2 className="text-sm font-semibold text-gray-900">
            {resposta ? 'Editar mensagem pronta' : 'Nova mensagem pronta'}
          </h2>
          <button
            type="button"
            onClick={onFechar}
            className="rounded p-1 text-gray-400 hover:bg-gray-100"
            aria-label="Fechar"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="space-y-4 overflow-y-auto p-4">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div className="sm:col-span-2">
              <label className="mb-1 block text-xs font-medium text-gray-600">Título do atalho</label>
              <input
                value={titulo}
                onChange={(e) => setTitulo(e.target.value)}
                placeholder="Confirmação de exame"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
                autoFocus
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-gray-600">Categoria (opcional)</label>
              <input
                value={categoria}
                onChange={(e) => setCategoria(e.target.value)}
                placeholder="Exames"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
              />
            </div>
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-gray-600">Mensagem</label>
            <textarea
              value={corpo}
              onChange={(e) => setCorpo(e.target.value)}
              rows={5}
              placeholder="{{saudacao}}, {{primeironome}}! Seu exame está marcado para {{data_exame}}."
              className="w-full resize-y rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
            />
            <p className="mt-1 text-xs text-gray-500">
              Escreva <code className="rounded bg-gray-100 px-1">{'{{nome_da_variavel}}'}</code> para o
              que muda a cada paciente. As tags abaixo o sistema preenche sozinho; qualquer outra vira
              um campo que o operador digita na hora.
            </p>
            <div className="mt-2 flex flex-wrap gap-1">
              {tagsAutomaticas?.map((t) => (
                <button
                  key={t.nome}
                  type="button"
                  onClick={() => inserirTag(t.nome)}
                  title={t.descricao}
                  className="rounded border border-emerald-200 bg-emerald-50 px-1.5 py-0.5 text-xs text-emerald-700 hover:bg-emerald-100"
                >
                  {`{{${t.nome}}}`}
                </button>
              ))}
            </div>
          </div>

          {campos.length > 0 ? (
            <div>
              <p className="mb-1 text-xs font-medium text-gray-600">
                Campos que o operador vai preencher
              </p>
              <div className="space-y-2 rounded-md border border-gray-200 p-2">
                {campos.map((c, i) => (
                  <div key={c.nome} className="flex items-center gap-2">
                    <code className="w-40 shrink-0 truncate rounded bg-gray-100 px-1.5 py-1 text-xs text-gray-700">
                      {`{{${c.nome}}}`}
                    </code>
                    <input
                      value={c.rotulo ?? ''}
                      onChange={(e) =>
                        setCampos((arr) =>
                          arr.map((x, j) => (j === i ? { ...x, rotulo: e.target.value || null } : x)),
                        )
                      }
                      placeholder="Rótulo no formulário (opcional)"
                      className="flex-1 rounded-md border border-gray-300 px-2 py-1 text-sm outline-none focus:border-primary-400"
                    />
                    <select
                      value={c.tipo}
                      onChange={(e) =>
                        setCampos((arr) =>
                          arr.map((x, j) =>
                            j === i ? { ...x, tipo: e.target.value as TipoCampoResposta } : x,
                          ),
                        )
                      }
                      className="rounded-md border border-gray-300 px-2 py-1 text-sm outline-none focus:border-primary-400"
                    >
                      {TIPOS.map((t) => (
                        <option key={t} value={t}>
                          {ROTULO_TIPO_CAMPO[t]}
                        </option>
                      ))}
                    </select>
                  </div>
                ))}
              </div>
              <p className="mt-1 text-xs text-gray-400">
                A lista vem do texto: tirou a tag da mensagem, o campo some.
              </p>
            </div>
          ) : null}

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div className="sm:col-span-2">
              <label className="mb-1 block text-xs font-medium text-gray-600">Quem enxerga</label>
              <select
                value={unidadeId}
                onChange={(e) => setUnidadeId(e.target.value)}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
              >
                <option value="">Todas as unidades</option>
                {unidades?.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.nome}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-gray-600">Ordem na lista</label>
              <input
                type="number"
                value={ordem}
                onChange={(e) => setOrdem(Number(e.target.value) || 0)}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
              />
            </div>
          </div>

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} />
            Ativa (aparece no painel do chat)
          </label>

          {erro ? <p className="text-xs text-red-600">{erro}</p> : null}
        </div>

        <div className="flex justify-end gap-2 border-t border-gray-200 px-4 py-3">
          <button
            type="button"
            onClick={onFechar}
            className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={() => void salvar()}
            disabled={salvando || !titulo.trim() || !corpo.trim()}
            className="rounded-md bg-primary-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50"
          >
            {salvando ? 'Salvando…' : 'Salvar'}
          </button>
        </div>
      </div>
    </div>
  );
}
