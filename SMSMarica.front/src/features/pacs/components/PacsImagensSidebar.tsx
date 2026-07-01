import { useState } from 'react';
import { Image as ImageIcon, Loader2 } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { useImagemRendered } from '@/features/pacs/lib/rendered';

export type ImagemLista = { imageId: string; rotulo: string };

type Props = {
  imagens: ImagemLista[];
  /** imageId carregado no quadrado em foco (destaca a miniatura). */
  imageIdFocado: string | null;
  aoSelecionar: (imageId: string) => void;
  /**
   * Duplo-clique na miniatura: limpa o cache local desta imagem (thumb, preview e
   * frame diagnóstico) e a recria. Corrige imagem que abre em branco por cache
   * corrompido. O pai cuida do backend + do recarregamento no viewport; deve
   * resolver a Promise para a miniatura atualizar em seguida.
   */
  aoRecriar?: (imageId: string) => Promise<void> | void;
  /** Metadados ainda carregando (lista pode estar vazia momentaneamente). */
  carregando?: boolean;
  /** Há um estudo aberto? (controla a mensagem de vazio.) */
  temEstudo: boolean;
};

/**
 * Lista plana de todas as imagens do estudo (não agrupada por série). Foco
 * atual: mamografia (single-frame). Clicar carrega a imagem no quadrado em foco;
 * duplo-clique recria o cache da imagem.
 */
export function PacsImagensSidebar({
  imagens,
  imageIdFocado,
  aoSelecionar,
  aoRecriar,
  carregando,
  temEstudo,
}: Props) {
  // Versão por imagem: incrementar força a miniatura a rebuscar (fura o cache
  // imutável do browser). recriandoId marca a que está sendo recriada (spinner).
  const [versoes, setVersoes] = useState<Record<string, number>>({});
  const [recriandoId, setRecriandoId] = useState<string | null>(null);

  async function recriar(imageId: string) {
    if (recriandoId) return; // evita cliques concorrentes
    setRecriandoId(imageId);
    try {
      await aoRecriar?.(imageId);
      setVersoes((v) => ({ ...v, [imageId]: (v[imageId] ?? 0) + 1 }));
    } finally {
      setRecriandoId(null);
    }
  }

  return (
    <aside className="flex w-56 flex-shrink-0 flex-col border-r border-gray-700 bg-gray-900 text-gray-100">
      <div className="border-b border-gray-700 px-3 py-2 text-[11px] font-semibold uppercase tracking-wide text-gray-400">
        Imagens
      </div>
      <div className="flex-1 overflow-y-auto p-2">
        {!temEstudo ? (
          <p className="px-2 py-4 text-sm text-gray-500">Busque e selecione um exame.</p>
        ) : carregando && imagens.length === 0 ? (
          <div className="flex items-center gap-2 px-2 py-4 text-sm text-gray-400">
            <Loader2 className="h-4 w-4 animate-spin" /> Carregando imagens...
          </div>
        ) : imagens.length === 0 ? (
          <p className="px-2 py-4 text-sm text-gray-500">Nenhuma imagem neste estudo.</p>
        ) : (
          <ul className="space-y-1.5">
            {imagens.map((img, i) => {
              const ativa = img.imageId === imageIdFocado;
              return (
                <li key={img.imageId}>
                  <button
                    type="button"
                    onClick={() => aoSelecionar(img.imageId)}
                    onDoubleClick={() => void recriar(img.imageId)}
                    title="Duplo-clique para limpar o cache e recriar esta imagem"
                    className={cn(
                      'flex w-full items-center gap-2 rounded-md p-1.5 text-left transition-colors',
                      ativa ? 'bg-primary-600/20 ring-2 ring-primary-500' : 'hover:bg-gray-800',
                    )}
                  >
                    <ThumbnailImagem
                      imageId={img.imageId}
                      ativa={ativa}
                      versao={versoes[img.imageId] ?? 0}
                      recriando={recriandoId === img.imageId}
                    />
                    <span className="min-w-0 flex-1 text-xs">
                      <span className="block truncate font-semibold text-gray-200">
                        {img.rotulo || `Imagem ${i + 1}`}
                      </span>
                      <span className="block text-[10px] text-gray-500">#{i + 1}</span>
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </aside>
  );
}

const TAMANHO_THUMB = 72;

/**
 * Miniatura via WADO-RS /rendered (JPEG ~2,5KB já reduzido pelo servidor) — não
 * baixa a imagem DICOM crua (~53MB). Antes usava loadImageToCanvas, que forçava
 * o download da resolução total de TODAS as imagens só para montar a lista.
 */
function ThumbnailImagem({
  imageId,
  ativa,
  versao,
  recriando,
}: {
  imageId: string;
  ativa: boolean;
  versao: number;
  recriando: boolean;
}) {
  const url = useImagemRendered(imageId, 160, versao);

  return (
    <div
      className={cn(
        'relative flex flex-shrink-0 items-center justify-center overflow-hidden rounded border bg-black',
        ativa ? 'border-primary-400' : 'border-gray-700',
      )}
      style={{ width: TAMANHO_THUMB, height: TAMANHO_THUMB }}
    >
      {url ? (
        <img src={url} alt="" className="h-full w-full object-contain" draggable={false} />
      ) : (
        <ImageIcon className="h-5 w-5 text-gray-700" />
      )}
      {recriando ? (
        <div className="absolute inset-0 flex items-center justify-center bg-black/60">
          <Loader2 className="h-5 w-5 animate-spin text-primary-300" />
        </div>
      ) : null}
    </div>
  );
}
