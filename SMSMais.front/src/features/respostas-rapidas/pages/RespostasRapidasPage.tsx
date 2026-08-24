import { useState } from 'react';
import { Pencil, Plus, Trash2, Zap } from 'lucide-react';
import {
  useExcluirRespostaRapida,
  useRespostasRapidas,
} from '@/features/respostas-rapidas/api/queries';
import { RespostaRapidaFormDialog } from '@/features/respostas-rapidas/components/RespostaRapidaFormDialog';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import type { RespostaRapida } from '@/features/respostas-rapidas/types';

/**
 * Cadastro das mensagens prontas do chat. Quem atende usa os atalhos pelo painel da conversa;
 * aqui é onde o texto e as variáveis são definidos.
 */
export function RespostasRapidasPage() {
  const { data: respostas, isLoading } = useRespostasRapidas(true);
  const excluir = useExcluirRespostaRapida();
  const [editando, setEditando] = useState<RespostaRapida | null>(null);
  const [criando, setCriando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function aoExcluir(r: RespostaRapida) {
    setErro(null);
    try {
      await excluir.mutateAsync(r.id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-xl font-semibold text-gray-900">
            <Zap className="h-5 w-5 text-amber-500" /> Mensagens prontas
          </h1>
          <p className="text-sm text-gray-500">
            Respostas rápidas do chat. O operador clica no atalho, o texto cai no campo de digitação
            já com o nome do paciente — e só então ele envia.
          </p>
        </div>
        <button
          type="button"
          onClick={() => setCriando(true)}
          className="flex items-center gap-1.5 rounded-md bg-primary-600 px-3 py-2 text-sm font-medium text-white hover:bg-primary-700"
        >
          <Plus className="h-4 w-4" /> Nova mensagem
        </button>
      </div>

      {erro ? <p className="mb-3 text-sm text-red-600">{erro}</p> : null}

      <div className="overflow-hidden rounded-lg border border-gray-200 bg-white">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500">
            <tr>
              <th className="px-4 py-2 font-medium">Título</th>
              <th className="px-4 py-2 font-medium">Mensagem</th>
              <th className="px-4 py-2 font-medium">Variáveis</th>
              <th className="px-4 py-2 font-medium">Visibilidade</th>
              <th className="px-4 py-2" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading ? (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-gray-500">
                  Carregando…
                </td>
              </tr>
            ) : null}

            {!isLoading && (respostas?.length ?? 0) === 0 ? (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-gray-500">
                  Nenhuma mensagem pronta cadastrada.
                </td>
              </tr>
            ) : null}

            {respostas?.map((r) => (
              <tr key={r.id} className={r.ativo ? '' : 'bg-gray-50 text-gray-400'}>
                <td className="px-4 py-2">
                  <span className="font-medium text-gray-900">{r.titulo}</span>
                  {r.categoria ? (
                    <span className="ml-2 rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-500">
                      {r.categoria}
                    </span>
                  ) : null}
                  {!r.ativo ? <span className="ml-2 text-xs">(inativa)</span> : null}
                </td>
                <td className="max-w-md px-4 py-2">
                  <span className="line-clamp-2 text-xs text-gray-600">{r.corpo}</span>
                </td>
                <td className="px-4 py-2 text-xs text-gray-600">
                  {r.campos.length === 0 && r.tagsAutomaticas.length === 0 ? (
                    <span className="text-gray-400">só texto</span>
                  ) : (
                    [...r.tagsAutomaticas, ...r.campos.map((c) => c.nome)]
                      .map((t) => `{{${t}}}`)
                      .join(' ')
                  )}
                </td>
                <td className="px-4 py-2 text-xs text-gray-600">{r.unidadeNome ?? 'Todas as unidades'}</td>
                <td className="whitespace-nowrap px-4 py-2 text-right">
                  <button
                    type="button"
                    onClick={() => setEditando(r)}
                    className="rounded p-1.5 text-gray-400 hover:bg-gray-100 hover:text-primary-700"
                    aria-label="Editar"
                    title="Editar"
                  >
                    <Pencil className="h-4 w-4" />
                  </button>
                  <button
                    type="button"
                    onClick={() => void aoExcluir(r)}
                    disabled={excluir.isPending}
                    className="rounded p-1.5 text-gray-400 hover:bg-gray-100 hover:text-red-700 disabled:opacity-50"
                    aria-label="Excluir"
                    title="Excluir"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {criando || editando ? (
        <RespostaRapidaFormDialog
          resposta={editando}
          onFechar={() => {
            setCriando(false);
            setEditando(null);
          }}
        />
      ) : null}
    </div>
  );
}
