import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Download, Eye, FileText, Image as ImageIcon, Loader2, X } from 'lucide-react';
import { baixarArquivo } from '@/shared/acervo/api';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { caminhoAnexo } from '@/features/comunicacao-interna/api/comunicacaoInternaApi';
import { formaDoAnexo, tamanhoFmt } from '@/features/comunicacao-interna/lib/formatar';
import type { AnexoInterno as Anexo } from '@/features/comunicacao-interna/types';
import { PlayerAudioInterno } from '@/features/comunicacao-interna/components/PlayerAudioInterno';

/** Miniatura só até 8 MB: cada miniatura é um download com o token (vem como blob). */
const LIMITE_MINIATURA_BYTES = 8 * 1024 * 1024;

/**
 * Baixa o anexo como blob e devolve a blob URL, já com o tipo do DTO (não o que o servidor mandou:
 * fora da lista segura ele responde octet-stream). Só é chamado para formas seguras (imagem raster,
 * áudio, PDF); "arquivo" nunca vira URL aberta — só download.
 */
function useBlobUrl(mensagemId: string, mime: string, habilitado: boolean) {
  const q = useQuery({
    queryKey: ['interno', 'anexo', mensagemId],
    queryFn: () => baixarArquivo(caminhoAnexo(mensagemId)),
    enabled: habilitado,
    // O arquivo de uma mensagem não muda (nada se edita — ADR-0079 §3).
    staleTime: Infinity,
    gcTime: 10 * 60_000,
    retry: false,
  });
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!q.data) return;
    const u = URL.createObjectURL(new Blob([q.data.blob], { type: mime }));
    setUrl(u);
    return () => URL.revokeObjectURL(u);
  }, [q.data, mime]);
  return { url, carregando: q.isLoading, erro: q.isError };
}

/** Download pelo navegador (`<a download>`): o conteúdo nunca é aberto numa aba. */
async function baixarParaDisco(mensagemId: string, nome: string) {
  const r = await baixarArquivo(caminhoAnexo(mensagemId));
  const u = URL.createObjectURL(new Blob([r.blob], { type: 'application/octet-stream' }));
  const a = document.createElement('a');
  a.href = u;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(u), 10_000);
}

function BotaoBaixar({ mensagemId, nome, claro }: { mensagemId: string; nome: string; claro?: boolean }) {
  const [baixando, setBaixando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  return (
    <>
      <button
        type="button"
        onClick={async () => {
          setErro(null);
          setBaixando(true);
          try {
            await baixarParaDisco(mensagemId, nome);
          } catch (e) {
            setErro(extrairMensagemDeErro(e));
          } finally {
            setBaixando(false);
          }
        }}
        disabled={baixando}
        className={
          claro
            ? 'flex items-center gap-1 rounded-md bg-white/15 px-2.5 py-1.5 text-xs font-medium text-white hover:bg-white/25'
            : 'flex items-center gap-1 rounded-md bg-black/5 px-2 py-1 text-xs font-medium text-gray-700 hover:bg-black/10 disabled:opacity-50'
        }
      >
        {baixando ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Download className="h-3.5 w-3.5" />} Baixar
      </button>
      {erro && <span className="text-[11px] text-red-600">{erro}</span>}
    </>
  );
}

/** Visualização grande (imagem ou PDF) por cima de tudo, com Baixar e Fechar. */
function Visualizador({
  mensagemId,
  anexo,
  aoFechar,
}: {
  mensagemId: string;
  anexo: Anexo;
  aoFechar: () => void;
}) {
  const forma = formaDoAnexo(anexo.mime, anexo.ehAudio);
  const { url, carregando, erro } = useBlobUrl(mensagemId, forma === 'pdf' ? 'application/pdf' : anexo.mime, true);
  useEffect(() => {
    const t = (e: KeyboardEvent) => e.key === 'Escape' && aoFechar();
    document.addEventListener('keydown', t);
    return () => document.removeEventListener('keydown', t);
  }, [aoFechar]);

  return (
    <div className="fixed inset-0 z-[70] flex flex-col bg-black/85" role="dialog" aria-label={anexo.nome}>
      <div className="flex items-center gap-2 px-4 py-2 text-white">
        <span className="min-w-0 flex-1 truncate text-sm" title={anexo.nome}>
          {anexo.nome} <span className="text-white/60">· {tamanhoFmt(anexo.tamanhoBytes)}</span>
        </span>
        <BotaoBaixar mensagemId={mensagemId} nome={anexo.nome} claro />
        <button type="button" onClick={aoFechar} className="rounded-md p-1.5 hover:bg-white/15" aria-label="Fechar">
          <X className="h-5 w-5" />
        </button>
      </div>
      <div className="flex min-h-0 flex-1 items-center justify-center p-4" onClick={aoFechar}>
        {carregando || !url ? (
          erro ? (
            <p className="text-sm text-white/80">Não foi possível abrir o arquivo.</p>
          ) : (
            <Loader2 className="h-8 w-8 animate-spin text-white/70" />
          )
        ) : forma === 'pdf' ? (
          <iframe
            src={url}
            title={anexo.nome}
            className="h-full w-full max-w-5xl rounded bg-white"
            onClick={(e) => e.stopPropagation()}
          />
        ) : (
          <img
            src={url}
            alt={anexo.nome}
            className="max-h-full max-w-full rounded object-contain shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          />
        )}
      </div>
    </div>
  );
}

/**
 * O anexo dentro da bolha. Só imagem raster, áudio e PDF são desenhados; qualquer outro tipo
 * (inclusive SVG e HTML) é "arquivo": ícone, nome, tamanho e Baixar — nunca aberto no navegador.
 */
export function AnexoInterno({ mensagemId, anexo }: { mensagemId: string; anexo: Anexo }) {
  const forma = formaDoAnexo(anexo.mime, anexo.ehAudio);
  const [vendo, setVendo] = useState(false);
  const miniatura = forma === 'imagem' && anexo.tamanhoBytes <= LIMITE_MINIATURA_BYTES;
  const img = useBlobUrl(mensagemId, anexo.mime, miniatura);
  const audio = useBlobUrl(mensagemId, anexo.mime.startsWith('audio/') ? anexo.mime : 'audio/webm', forma === 'audio');

  if (forma === 'audio') {
    return (
      <PlayerAudioInterno
        mensagemId={mensagemId}
        anexo={anexo}
        url={audio.url}
        carregando={audio.carregando}
        erroDownload={audio.erro}
        botaoBaixar={<BotaoBaixar mensagemId={mensagemId} nome={anexo.nome} />}
      />
    );
  }

  if (miniatura) {
    return (
      <>
        <button
          type="button"
          onClick={() => setVendo(true)}
          className="block overflow-hidden rounded-lg bg-black/5"
          title="Ver a imagem"
        >
          {img.url ? (
            <img src={img.url} alt={anexo.nome} className="max-h-56 max-w-[16rem] object-cover" />
          ) : (
            <span className="flex h-32 w-48 items-center justify-center text-gray-400">
              {img.erro ? <ImageIcon className="h-6 w-6" /> : <Loader2 className="h-5 w-5 animate-spin" />}
            </span>
          )}
        </button>
        {vendo && <Visualizador mensagemId={mensagemId} anexo={anexo} aoFechar={() => setVendo(false)} />}
      </>
    );
  }

  const Icone = forma === 'imagem' ? ImageIcon : FileText;
  return (
    <div className="flex min-w-[13rem] max-w-[17rem] flex-col gap-1.5 rounded-lg bg-black/5 p-2">
      <div className="flex items-center gap-2">
        <Icone className="h-8 w-8 shrink-0 text-gray-400" />
        <div className="min-w-0">
          <p className="truncate text-xs font-medium text-gray-800" title={anexo.nome}>
            {anexo.nome}
          </p>
          <p className="text-[11px] text-gray-500">{tamanhoFmt(anexo.tamanhoBytes)}</p>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-1.5">
        {(forma === 'pdf' || forma === 'imagem') && (
          <button
            type="button"
            onClick={() => setVendo(true)}
            className="flex items-center gap-1 rounded-md bg-black/5 px-2 py-1 text-xs font-medium text-gray-700 hover:bg-black/10"
          >
            <Eye className="h-3.5 w-3.5" /> Visualizar
          </button>
        )}
        <BotaoBaixar mensagemId={mensagemId} nome={anexo.nome} />
      </div>
      {vendo && <Visualizador mensagemId={mensagemId} anexo={anexo} aoFechar={() => setVendo(false)} />}
    </div>
  );
}
