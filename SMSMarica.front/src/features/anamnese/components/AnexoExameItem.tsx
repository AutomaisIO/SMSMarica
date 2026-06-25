import { Eye, FileText, Loader2, Save, Trash2 } from 'lucide-react';
import { formatarTamanhoBytes } from '@/features/anamnese/lib/anexos';
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

type Props = {
  anexo: AnexoExameDto;
  podeEditar: boolean;
  aoRevisar: () => void;
  aoSalvar: () => void;
  aoExcluir: () => void;
  revisando?: boolean;
  salvando?: boolean;
  excluindo?: boolean;
};

/**
 * Linha de um documento anexado. Reaproveitada no modal do QR (lista que chega
 * em tempo real) e na seção fixa da anamnese. "Salvar"/"Excluir" só aparecem
 * para quem pode editar; "Salvar" só faz sentido quando o status é Pendente.
 */
export function AnexoExameItem({
  anexo,
  podeEditar,
  aoRevisar,
  aoSalvar,
  aoExcluir,
  revisando,
  salvando,
  excluindo,
}: Props) {
  const pendente = anexo.status === 'Pendente';
  return (
    <li className="flex flex-wrap items-center gap-3 rounded-lg border border-gray-200 bg-white p-3">
      <FileText className="h-5 w-5 shrink-0 text-primary-600" />
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="truncate font-medium text-gray-900" title={anexo.nome}>
            {anexo.nome}
          </span>
          <span
            className={
              pendente
                ? 'rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-medium text-amber-700'
                : 'rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-medium text-green-700'
            }
          >
            {pendente ? 'Pendente' : 'Salvo'}
          </span>
        </div>
        {anexo.descricao ? (
          <p className="truncate text-xs text-gray-500" title={anexo.descricao}>
            {anexo.descricao}
          </p>
        ) : null}
        <p className="text-xs text-gray-400">
          {formatarDataHora(anexo.criadoEm)} · {formatarTamanhoBytes(anexo.tamanhoBytes)}
          {anexo.paginas ? ` · ${anexo.paginas} pág.` : ''}
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-1.5">
        <button
          type="button"
          onClick={aoRevisar}
          disabled={revisando}
          className="inline-flex items-center gap-1 rounded border border-gray-200 px-2 py-1 text-xs text-primary-700 hover:bg-primary-50 disabled:opacity-50"
        >
          {revisando ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Eye className="h-3.5 w-3.5" />}
          Revisar
        </button>
        {podeEditar && pendente ? (
          <button
            type="button"
            onClick={aoSalvar}
            disabled={salvando}
            className="inline-flex items-center gap-1 rounded border border-green-200 px-2 py-1 text-xs text-green-700 hover:bg-green-50 disabled:opacity-50"
          >
            {salvando ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Save className="h-3.5 w-3.5" />}
            Salvar
          </button>
        ) : null}
        {podeEditar ? (
          <button
            type="button"
            onClick={aoExcluir}
            disabled={excluindo}
            className="inline-flex items-center gap-1 rounded border border-red-200 px-2 py-1 text-xs text-red-700 hover:bg-red-50 disabled:opacity-50"
          >
            {excluindo ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Trash2 className="h-3.5 w-3.5" />}
            Excluir
          </button>
        ) : null}
      </div>
    </li>
  );
}
