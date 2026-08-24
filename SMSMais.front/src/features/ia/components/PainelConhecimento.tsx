import { useState } from 'react';
import {
  AlertTriangle,
  DatabaseZap,
  FileText,
  Loader2,
  Lock,
  Pencil,
  Plus,
  Sparkles,
  Trash2,
} from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { usePermissao } from '@/shared/auth/authStore';
import { EditorDocumento } from '@/features/ia/components/EditorDocumento';
import {
  useDocumentosConhecimento,
  useExtrairModelo,
  useGerarEmbeddings,
  useRemoverDocumento,
} from '@/features/ia/api/conhecimentoQueries';
import type {
  EmbeddingsBackfillResultado,
  ExtracaoModeloResultado,
  FonteConfig,
} from '@/features/ia/types';

type Props = {
  fonte: FonteConfig;
  onFechar: () => void;
};

export function PainelConhecimento({ fonte, onFechar }: Props) {
  const podeEditar = usePermissao('InteligenciaConfiguracao', 'Edicao');
  const docs = useDocumentosConhecimento(fonte.id);
  const extrair = useExtrairModelo(fonte.id);
  const embeddar = useGerarEmbeddings(fonte.id);
  const remover = useRemoverDocumento(fonte.id);

  const [editor, setEditor] = useState<{ docId: string | null; soLeitura: boolean } | null>(null);
  const [maxTabelas, setMaxTabelas] = useState('2000');
  const [erro, setErro] = useState<string | null>(null);
  const [extracao, setExtracao] = useState<ExtracaoModeloResultado | null>(null);
  const [embeddings, setEmbeddings] = useState<EmbeddingsBackfillResultado | null>(null);

  async function aoExtrair() {
    setErro(null);
    setExtracao(null);
    try {
      setExtracao(await extrair.mutateAsync(Number(maxTabelas) || 2000));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoEmbeddar() {
    setErro(null);
    setEmbeddings(null);
    try {
      setEmbeddings(await embeddar.mutateAsync());
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoRemover(docId: string, caminho: string) {
    if (!window.confirm(`Remover o documento "${caminho}"?`)) return;
    setErro(null);
    try {
      await remover.mutateAsync(docId);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const lista = docs.data ?? [];
  const modelo = lista.filter((d) => d.caminho.startsWith('modelo/'));
  const manuais = lista.filter((d) => d.caminho.startsWith('manual/'));
  const doRepo = lista.filter((d) => d.doRepo && !d.caminho.startsWith('modelo/'));

  return (
    <Modal
      aberto
      aoFechar={onFechar}
      titulo={`Conhecimento — ${fonte.nome}`}
      largura="lg"
    >
      <div className="space-y-5">
        {/* ── Levantar estrutura ─────────────────────────────────────── */}
        <section className="rounded-xl border border-gray-200 bg-gray-50 p-4">
          <div className="flex items-start gap-3">
            <DatabaseZap className="mt-0.5 h-5 w-5 shrink-0 text-primary-600" />
            <div className="min-w-0 flex-1">
              <h3 className="text-sm font-semibold text-gray-900">Levantar estrutura da base</h3>
              <p className="mt-0.5 text-xs text-gray-500">
                Lê o schema direto do banco ({fonte.dialeto}) e gera um documento por tabela, com
                colunas e relacionamentos, mais um catálogo geral.
                {fonte.viaAgente && (
                  <>
                    {' '}
                    <span className={fonte.agenteConectado ? 'text-emerald-600' : 'text-red-600'}>
                      Agente {fonte.agenteConectado ? 'conectado' : 'desconectado'}.
                    </span>
                  </>
                )}
              </p>
              {podeEditar && (
                <div className="mt-3 flex items-center gap-2">
                  <div className="w-28">
                    <Input
                      value={maxTabelas}
                      onChange={(e) => setMaxTabelas(e.target.value)}
                      title="Máximo de tabelas a documentar"
                    />
                  </div>
                  <span className="text-xs text-gray-400">tabelas (teto)</span>
                  <Button
                    onClick={aoExtrair}
                    disabled={extrair.isPending || (fonte.viaAgente && !fonte.agenteConectado)}
                  >
                    {extrair.isPending ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <DatabaseZap className="h-4 w-4" />
                    )}
                    Levantar estrutura
                  </Button>
                </div>
              )}
              {extracao && (
                <div className="mt-3 rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-xs text-emerald-800">
                  <p>
                    {extracao.totalTabelas} tabelas · {extracao.totalViews} views ·{' '}
                    {extracao.documentadas} documentadas · {extracao.totalFks} relacionamentos ·{' '}
                    {extracao.documentosGerados} documentos.
                  </p>
                  {extracao.aviso && (
                    <p className="mt-1 flex items-start gap-1 text-amber-700">
                      <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                      {extracao.aviso}
                    </p>
                  )}
                </div>
              )}
            </div>
          </div>
        </section>

        {/* ── Embeddings (RAG) ───────────────────────────────────────── */}
        <section className="rounded-xl border border-gray-200 bg-gray-50 p-4">
          <div className="flex items-start gap-3">
            <Sparkles className="mt-0.5 h-5 w-5 shrink-0 text-primary-600" />
            <div className="min-w-0 flex-1">
              <h3 className="text-sm font-semibold text-gray-900">Embeddings (RAG)</h3>
              <p className="mt-0.5 text-xs text-gray-500">
                Gera os vetores dos documentos pra busca por similaridade. Necessário em bases
                grandes (a IA recupera só os trechos relevantes em vez do modelo inteiro). Roda
                mesmo com o RAG desligado e pode ser repetido — só processa o que falta.
              </p>
              {podeEditar && (
                <div className="mt-3">
                  <Button onClick={aoEmbeddar} disabled={embeddar.isPending} variante="secundaria">
                    {embeddar.isPending ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Sparkles className="h-4 w-4" />
                    )}
                    Gerar embeddings
                  </Button>
                </div>
              )}
              {embeddings && (
                <div className="mt-3 rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-xs text-emerald-800">
                  <p>
                    {embeddings.gerados} gerados agora · {embeddings.jaTinham} já tinham ·{' '}
                    {embeddings.totalChunks} chunks no total · {embeddings.restantes} restantes.
                  </p>
                  {embeddings.aviso && (
                    <p className="mt-1 flex items-start gap-1 text-amber-700">
                      <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                      {embeddings.aviso}
                    </p>
                  )}
                </div>
              )}
            </div>
          </div>
        </section>

        {erro && (
          <p className="flex items-start gap-2 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            {erro}
          </p>
        )}

        {/* ── Documentos ─────────────────────────────────────────────── */}
        <section>
          <div className="mb-2 flex items-center justify-between">
            <h3 className="text-sm font-semibold text-gray-900">
              Documentos {docs.data ? `(${lista.length})` : ''}
            </h3>
            {podeEditar && (
              <Button
                variante="secundaria"
                tamanho="sm"
                onClick={() => setEditor({ docId: null, soLeitura: false })}
              >
                <Plus className="h-4 w-4" />
                Novo documento
              </Button>
            )}
          </div>

          {docs.isLoading ? (
            <div className="flex items-center justify-center py-8 text-gray-400">
              <Loader2 className="h-5 w-5 animate-spin" />
            </div>
          ) : lista.length === 0 ? (
            <p className="rounded-lg border border-dashed border-gray-200 p-6 text-center text-sm text-gray-400">
              Nenhum documento ainda. Levante a estrutura da base ou crie um documento manual.
            </p>
          ) : (
            <div className="space-y-4">
              {grupo('Modelo (tabelas)', modelo, editar, aoRemover, podeEditar)}
              {grupo('Manuais (regras, relacionamentos, exemplos)', manuais, editar, aoRemover, podeEditar)}
              {grupo('Do repositório (só leitura)', doRepo, editar, aoRemover, false)}
            </div>
          )}
        </section>
      </div>

      {editor && (
        <EditorDocumento
          fonteId={fonte.id}
          docId={editor.docId}
          soLeitura={editor.soLeitura}
          onFechar={() => setEditor(null)}
        />
      )}
    </Modal>
  );

  function editar(docId: string, soLeitura: boolean) {
    setEditor({ docId, soLeitura });
  }
}

function grupo(
  titulo: string,
  docs: {
    id: string;
    caminho: string;
    versao: number;
    tamanho: number;
    chunks: number;
    doRepo: boolean;
  }[],
  editar: (docId: string, soLeitura: boolean) => void,
  remover: (docId: string, caminho: string) => void,
  podeEditar: boolean,
) {
  if (docs.length === 0) return null;
  return (
    <div>
      <p className="mb-1 text-xs font-medium uppercase tracking-wide text-gray-400">{titulo}</p>
      <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200">
        {docs.map((d) => (
          <li key={d.id} className="flex items-center gap-3 px-3 py-2">
            {d.doRepo ? (
              <Lock className="h-3.5 w-3.5 shrink-0 text-gray-400" />
            ) : (
              <FileText className="h-3.5 w-3.5 shrink-0 text-gray-400" />
            )}
            <div className="min-w-0 flex-1">
              <div className="truncate font-mono text-xs text-gray-800">{d.caminho}</div>
              <div className="text-[11px] text-gray-400">
                v{d.versao} · {d.chunks} trechos · {(d.tamanho / 1000).toFixed(1)} KB
              </div>
            </div>
            <button
              type="button"
              onClick={() => editar(d.id, d.doRepo)}
              title={d.doRepo ? 'Ver' : 'Editar'}
              className="rounded p-1.5 text-gray-400 transition hover:bg-gray-100 hover:text-gray-700"
            >
              <Pencil className="h-3.5 w-3.5" />
            </button>
            {podeEditar && !d.doRepo && (
              <button
                type="button"
                onClick={() => remover(d.id, d.caminho)}
                title="Remover"
                className="rounded p-1.5 text-red-400 transition hover:bg-red-50 hover:text-red-600"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
