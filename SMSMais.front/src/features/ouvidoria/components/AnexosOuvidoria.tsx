import { useRef, useState } from 'react';
import { FileText, Loader2, Paperclip, X } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { notificar } from '@/shared/ui/Notificacoes';
import { urlMidiaAbsoluta } from '@/shared/api/midiaApi';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { enviarAnexo } from '@/features/ouvidoria/api/ouvidoriaApi';
import type { AnexoRef } from '@/features/ouvidoria/types';

const LIMITE_MB = 6;
const ACEITA = 'image/*,application/pdf';

type Props = {
  anexos: AnexoRef[];
  aoMudar: (anexos: AnexoRef[]) => void;
  disabled?: boolean;
  id?: string;
};

/** Seleciona imagens/PDF (≤ 6 MB), envia a `POST ouvidoria/anexos` e mantém a lista de referências. */
export function AnexosOuvidoriaInput({ anexos, aoMudar, disabled, id }: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [enviando, setEnviando] = useState(false);

  async function aoSelecionar(e: React.ChangeEvent<HTMLInputElement>) {
    const arquivos = Array.from(e.target.files ?? []);
    e.target.value = '';
    if (arquivos.length === 0) return;

    setEnviando(true);
    try {
      // Commit incremental: cada upload bem-sucedido entra na hora — falha num arquivo seguinte
      // não descarta os anteriores (evita mídia órfã no servidor).
      let atuais = anexos;
      for (const arq of arquivos) {
        if (arq.size > LIMITE_MB * 1024 * 1024) {
          notificar(`"${arq.name}" passa de ${LIMITE_MB} MB.`, 'erro');
          continue;
        }
        try {
          const ref = await enviarAnexo(arq);
          atuais = [...atuais, ref];
          aoMudar(atuais);
        } catch (erro) {
          notificar(`Falha ao anexar "${arq.name}": ${extrairMensagemDeErro(erro)}`, 'erro');
        }
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="space-y-2">
      <input
        id={id}
        ref={inputRef}
        type="file"
        accept={ACEITA}
        multiple
        hidden
        onChange={aoSelecionar}
        aria-label="Selecionar anexos"
      />
      <Button type="button" variante="outline" tamanho="sm" disabled={disabled || enviando} onClick={() => inputRef.current?.click()}>
        {enviando ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Paperclip className="h-4 w-4" aria-hidden="true" />}
        {enviando ? 'Enviando…' : 'Anexar arquivo'}
      </Button>
      <p className="text-xs text-slate-500">Imagens ou PDF, até {LIMITE_MB} MB cada.</p>

      {anexos.length > 0 && (
        <ul className="flex flex-wrap gap-2">
          {anexos.map((a) => (
            <li key={a.midiaId} className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-slate-50 px-2 py-1 text-xs text-slate-700">
              <FileText className="h-3.5 w-3.5 text-slate-400" aria-hidden="true" />
              <span className="max-w-[14rem] truncate">{a.nomeArquivo}</span>
              <button
                type="button"
                onClick={() => aoMudar(anexos.filter((x) => x.midiaId !== a.midiaId))}
                className="rounded p-0.5 text-slate-500 hover:bg-red-50 hover:text-red-600"
                aria-label={`Remover ${a.nomeArquivo}`}
                disabled={disabled}
              >
                <X className="h-3.5 w-3.5" />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** Lista só-leitura: cada anexo abre em nova aba. */
export function AnexosOuvidoriaLista({ anexos }: { anexos: { midiaId: string; nomeArquivo: string }[] }) {
  if (anexos.length === 0) return null;
  return (
    <ul className="flex flex-wrap gap-2">
      {anexos.map((a) => (
        <li key={a.midiaId}>
          <a
            href={urlMidiaAbsoluta(a.midiaId)}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-2 py-1 text-xs text-slate-700 hover:border-red-300 hover:text-red-700"
            title={`Abrir ${a.nomeArquivo}`}
          >
            <FileText className="h-3.5 w-3.5 text-slate-400" aria-hidden="true" />
            <span className="max-w-[16rem] truncate">{a.nomeArquivo}</span>
          </a>
        </li>
      ))}
    </ul>
  );
}
