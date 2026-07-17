import { CheckCircle2, FileWarning, Loader2, RefreshCw, XCircle } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useExecucoesImportacao } from '@/features/importacao-sisreg/api/queries';
import type { ImportacaoExecucao, StatusImportacaoArquivo } from '@/features/importacao-sisreg/types';

function fmt(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

const BADGE: Record<StatusImportacaoArquivo, { texto: string; cor: string; icone: JSX.Element }> = {
  Concluida: { texto: 'Concluída', cor: 'bg-emerald-50 text-emerald-700', icone: <CheckCircle2 className="h-3.5 w-3.5" /> },
  EmExecucao: { texto: 'Processando', cor: 'bg-blue-50 text-blue-700', icone: <Loader2 className="h-3.5 w-3.5 animate-spin" /> },
  Pendente: { texto: 'Na fila', cor: 'bg-gray-100 text-gray-600', icone: <Loader2 className="h-3.5 w-3.5" /> },
  ArquivoIncompativel: { texto: 'Incompatível', cor: 'bg-amber-50 text-amber-700', icone: <FileWarning className="h-3.5 w-3.5" /> },
  Erro: { texto: 'Erro', cor: 'bg-red-50 text-red-700', icone: <XCircle className="h-3.5 w-3.5" /> },
  Cancelada: { texto: 'Cancelada', cor: 'bg-gray-100 text-gray-500', icone: <XCircle className="h-3.5 w-3.5" /> },
};

/** Histórico de importações: uma linha por arquivo (quando, quem, válidos, inválidos). */
export function RastreioImportacao({ aoVerErros }: { aoVerErros: () => void }) {
  const execucoes = useExecucoesImportacao();
  const lista = execucoes.data ?? [];

  return (
    <section className="rounded-lg border border-gray-200 bg-white shadow-sm">
      <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
        <div>
          <h2 className="text-sm font-semibold text-gray-900">Rastreio de importações</h2>
          <p className="text-xs text-gray-500">Cada arquivo importado, com quem enviou e o resultado.</p>
        </div>
        <Button variante="outline" onClick={() => execucoes.refetch()} disabled={execucoes.isFetching}>
          {execucoes.isFetching ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
          Atualizar
        </Button>
      </div>

      {execucoes.isError ? (
        <div className="px-4 py-3 text-sm text-red-700">{extrairMensagemDeErro(execucoes.error)}</div>
      ) : null}

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500">
            <tr>
              <th className="px-3 py-2">Quando</th>
              <th className="px-3 py-2">Arquivo</th>
              <th className="px-3 py-2">Quem</th>
              <th className="px-3 py-2 text-center">Válidos</th>
              <th className="px-3 py-2 text-center">Inválidos</th>
              <th className="px-3 py-2">Situação</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {lista.map((e) => (
              <LinhaExecucao key={e.id} exec={e} aoVerErros={aoVerErros} />
            ))}
            {lista.length === 0 && !execucoes.isLoading ? (
              <tr>
                <td colSpan={6} className="px-3 py-8 text-center text-sm text-gray-400">
                  Nenhuma importação ainda.
                </td>
              </tr>
            ) : null}
            {execucoes.isLoading ? (
              <tr>
                <td colSpan={6} className="px-3 py-8 text-center text-gray-400">
                  <Loader2 className="inline h-4 w-4 animate-spin" /> Carregando…
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function LinhaExecucao({ exec, aoVerErros }: { exec: ImportacaoExecucao; aoVerErros: () => void }) {
  const badge = BADGE[exec.status];
  return (
    <tr>
      <td className="px-3 py-2 text-xs text-gray-600">{fmt(exec.iniciadoEm)}</td>
      <td className="px-3 py-2">
        <div className="font-medium text-gray-800">{exec.nomeArquivo}</div>
        {exec.caminhoNoZip ? <div className="text-xs text-gray-400">{exec.caminhoNoZip}</div> : null}
        {exec.mensagem ? <div className="text-xs text-amber-700">{exec.mensagem}</div> : null}
      </td>
      <td className="px-3 py-2 text-gray-700">{exec.criadoPorNome ?? '—'}</td>
      <td className="px-3 py-2 text-center">
        <span className="font-semibold text-emerald-700">{exec.validos}</span>
        {exec.jaExistiam > 0 ? (
          <span className="ml-1 text-xs text-gray-400" title="já existiam (não duplicados)">
            ({exec.jaExistiam} já tinha)
          </span>
        ) : null}
      </td>
      <td className="px-3 py-2 text-center">
        {exec.invalidos > 0 ? (
          <button type="button" className="font-semibold text-red-700 underline" onClick={aoVerErros}>
            {exec.invalidos}
          </button>
        ) : (
          <span className="text-gray-400">0</span>
        )}
      </td>
      <td className="px-3 py-2">
        <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ${badge.cor}`}>
          {badge.icone} {badge.texto}
        </span>
      </td>
    </tr>
  );
}
