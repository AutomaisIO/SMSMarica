import { useState } from 'react';
import { Eye, FileText, Loader2, Paperclip } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useAnexosExamePaciente } from '@/features/pacientes/api/queries';
import { abrirPdfAnexo, formatarTamanhoBytes } from '@/features/anamnese/lib/anexos';
import type { AnexoExameDto } from '@/features/anamnese/types';

function formatarDataHora(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Histórico de exames digitalizados do paciente (DocumentoExame status=Salvo),
 * agregado via SolicitacaoExame. Cada documento abre o PDF (stream autenticado).
 */
export function SecaoExamesAnexados({ pacienteId }: { pacienteId: string }) {
  const q = useAnexosExamePaciente(pacienteId);
  const [revisandoId, setRevisandoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  async function abrir(a: AnexoExameDto) {
    setErro(null);
    setRevisandoId(a.id);
    try {
      await abrirPdfAnexo(a.id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setRevisandoId(null);
    }
  }

  const colunas: Coluna<AnexoExameDto>[] = [
    {
      chave: 'nome',
      cabecalho: 'Documento',
      render: (a) => (
        <div className="flex items-center gap-2">
          <FileText className="h-4 w-4 shrink-0 text-primary-600" />
          <div className="min-w-0">
            <span className="block truncate font-medium text-gray-900" title={a.nome}>
              {a.nome}
            </span>
            {a.descricao ? (
              <span className="block truncate text-xs text-gray-500" title={a.descricao}>
                {a.descricao}
              </span>
            ) : null}
          </div>
        </div>
      ),
    },
    { chave: 'data', cabecalho: 'Data', render: (a) => formatarDataHora(a.criadoEm) },
    {
      chave: 'tamanho',
      cabecalho: 'Tamanho',
      render: (a) => (
        <span className="text-gray-600">
          {formatarTamanhoBytes(a.tamanhoBytes)}
          {a.paginas ? ` · ${a.paginas} pág.` : ''}
        </span>
      ),
    },
    {
      chave: 'acao',
      cabecalho: '',
      className: 'text-right',
      render: (a) => (
        <button
          type="button"
          onClick={() => abrir(a)}
          disabled={revisandoId === a.id}
          className="inline-flex items-center gap-1 rounded border border-gray-200 px-2 py-1 text-xs text-primary-700 hover:bg-primary-50 disabled:opacity-50"
        >
          {revisandoId === a.id ? (
            <Loader2 className="h-3.5 w-3.5 animate-spin" />
          ) : (
            <Eye className="h-3.5 w-3.5" />
          )}
          Visualizar
        </button>
      ),
    },
  ];

  return (
    <>
      <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-gray-900">
        <Paperclip className="h-4 w-4" /> Documentos / Exames anexados
      </div>
      {erro ? (
        <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}
      <Tabela
        colunas={colunas}
        dados={q.data ?? []}
        chaveLinha={(a) => a.id}
        carregando={q.isLoading}
        vazio={
          !q.isLoading && (q.data?.length ?? 0) === 0
            ? 'Nenhum exame digitalizado para este paciente.'
            : undefined
        }
      />
    </>
  );
}
