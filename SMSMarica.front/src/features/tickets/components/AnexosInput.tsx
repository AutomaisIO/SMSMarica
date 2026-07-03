import { useRef, useState } from 'react';
import { ImagePlus, Loader2, X } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { notificar } from '@/shared/ui/Notificacoes';
import { urlMidiaAbsoluta } from '@/shared/api/midiaApi';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { enviarAnexo } from '@/features/tickets/api/ticketsApi';
import type { AnexoRef } from '@/features/tickets/types';

type Props = {
  anexos: AnexoRef[];
  aoMudar: (anexos: AnexoRef[]) => void;
  disabled?: boolean;
};

/** Seleciona imagens (prints), envia ao backend e mantém a lista de referências. */
export function AnexosInput({ anexos, aoMudar, disabled }: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [enviando, setEnviando] = useState(false);

  async function aoSelecionar(e: React.ChangeEvent<HTMLInputElement>) {
    const arquivos = Array.from(e.target.files ?? []);
    e.target.value = ''; // permite reenviar o mesmo arquivo
    if (arquivos.length === 0) return;

    setEnviando(true);
    try {
      const novos: AnexoRef[] = [];
      for (const arq of arquivos) {
        if (!arq.type.startsWith('image/')) {
          notificar(`"${arq.name}" não é uma imagem.`, 'erro');
          continue;
        }
        novos.push(await enviarAnexo(arq));
      }
      if (novos.length) aoMudar([...anexos, ...novos]);
    } catch (erro) {
      notificar(extrairMensagemDeErro(erro), 'erro');
    } finally {
      setEnviando(false);
    }
  }

  function remover(midiaId: string) {
    aoMudar(anexos.filter((a) => a.midiaId !== midiaId));
  }

  return (
    <div className="space-y-2">
      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        multiple
        hidden
        onChange={aoSelecionar}
      />
      <Button
        type="button"
        variante="outline"
        tamanho="sm"
        disabled={disabled || enviando}
        onClick={() => inputRef.current?.click()}
      >
        {enviando ? <Loader2 className="h-4 w-4 animate-spin" /> : <ImagePlus className="h-4 w-4" />}
        {enviando ? 'Enviando…' : 'Anexar imagem'}
      </Button>

      {anexos.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {anexos.map((a) => (
            <div key={a.midiaId} className="group relative">
              <img
                src={urlMidiaAbsoluta(a.midiaId)}
                alt={a.nomeArquivo}
                className="h-20 w-20 rounded-lg object-cover ring-1 ring-slate-200"
              />
              <button
                type="button"
                onClick={() => remover(a.midiaId)}
                className="absolute -right-1.5 -top-1.5 rounded-full bg-slate-800 p-0.5 text-white opacity-90 hover:bg-red-600"
                title="Remover"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

/** Galeria só-leitura de anexos (miniaturas que abrem em nova aba). */
export function AnexosGaleria({ anexos }: { anexos: { midiaId: string; nomeArquivo: string }[] }) {
  if (anexos.length === 0) return null;
  return (
    <div className="flex flex-wrap gap-2">
      {anexos.map((a) => (
        <a
          key={a.midiaId}
          href={urlMidiaAbsoluta(a.midiaId)}
          target="_blank"
          rel="noreferrer"
          title={a.nomeArquivo}
        >
          <img
            src={urlMidiaAbsoluta(a.midiaId)}
            alt={a.nomeArquivo}
            className="h-24 w-24 rounded-lg object-cover ring-1 ring-slate-200 transition hover:ring-2 hover:ring-red-400"
          />
        </a>
      ))}
    </div>
  );
}
