import { useCallback, useRef, useState } from 'react';
import Cropper, { type Area } from 'react-easy-crop';
import { Camera, RotateCcw, Trash2 } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { arquivoParaDataUrl, recortarParaBase64 } from '@/shared/lib/imagem';
import { Avatar } from '@/shared/ui/Avatar';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';

type Props = {
  valor: string | null;
  aoMudar: (base64: string | null) => void;
  nome?: string | null;
  desabilitado?: boolean;
  tamanho?: 'md' | 'lg' | 'xl';
  className?: string;
};

const TAMANHO_FINAL = 512;

/**
 * Avatar editável: clique abre o picker, escolhe arquivo, abre modal de
 * crop quadrado com pan/zoom. Salva base64 JPEG redimensionado para 512px
 * (qualidade 85). Botão "Remover" permite limpar.
 */
export function UploadFoto({
  valor,
  aoMudar,
  nome,
  desabilitado,
  tamanho = 'xl',
  className,
}: Props) {
  const [arquivoSrc, setArquivoSrc] = useState<string | null>(null);
  const [crop, setCrop] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [areaPx, setAreaPx] = useState<Area | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const aoCropCompleto = useCallback((_: Area, pixels: Area) => {
    setAreaPx(pixels);
  }, []);

  async function selecionarArquivo(e: React.ChangeEvent<HTMLInputElement>) {
    const arquivo = e.target.files?.[0];
    e.target.value = '';
    if (!arquivo) return;

    if (!arquivo.type.startsWith('image/')) {
      setErro('Selecione um arquivo de imagem.');
      return;
    }
    if (arquivo.size > 10 * 1024 * 1024) {
      setErro('Imagem muito grande (limite 10 MB antes do recorte).');
      return;
    }

    setErro(null);
    try {
      const data = await arquivoParaDataUrl(arquivo);
      setArquivoSrc(data);
      setCrop({ x: 0, y: 0 });
      setZoom(1);
    } catch (err) {
      setErro(err instanceof Error ? err.message : 'Falha ao ler imagem.');
    }
  }

  function fechar() {
    setArquivoSrc(null);
    setAreaPx(null);
    setCrop({ x: 0, y: 0 });
    setZoom(1);
    setErro(null);
  }

  async function aplicar() {
    if (!arquivoSrc || !areaPx) return;
    setSalvando(true);
    setErro(null);
    try {
      const base64 = await recortarParaBase64(arquivoSrc, areaPx, TAMANHO_FINAL);
      aoMudar(base64);
      fechar();
    } catch (err) {
      setErro(err instanceof Error ? err.message : 'Falha ao recortar.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <div className={cn('flex items-center gap-4', className)}>
      <Avatar src={valor} nome={nome} tamanho={tamanho} />

      <div className="flex flex-col gap-2">
        <input
          ref={inputRef}
          type="file"
          accept="image/*"
          className="hidden"
          onChange={selecionarArquivo}
          disabled={desabilitado}
        />
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variante="outline"
            tamanho="sm"
            onClick={() => inputRef.current?.click()}
            disabled={desabilitado}
          >
            <Camera className="h-4 w-4" />
            {valor ? 'Trocar foto' : 'Adicionar foto'}
          </Button>
          {valor ? (
            <Button
              type="button"
              variante="ghost"
              tamanho="sm"
              onClick={() => aoMudar(null)}
              disabled={desabilitado}
            >
              <Trash2 className="h-4 w-4" />
              Remover
            </Button>
          ) : null}
        </div>
        <p className="text-xs text-gray-500">
          JPEG ou PNG. A imagem é recortada em quadrado e redimensionada para 512×512.
        </p>
        {erro ? <p className="text-xs text-red-700">{erro}</p> : null}
      </div>

      <Modal
        aberto={Boolean(arquivoSrc)}
        aoFechar={fechar}
        titulo="Recortar foto"
        descricao="Arraste para posicionar e use o zoom para enquadrar o rosto. O resultado é salvo em quadrado 512×512."
        largura="md"
      >
        {arquivoSrc ? (
          <div className="space-y-4">
            <div className="relative h-80 w-full overflow-hidden rounded-md bg-gray-900">
              <Cropper
                image={arquivoSrc}
                crop={crop}
                zoom={zoom}
                aspect={1}
                cropShape="round"
                showGrid={false}
                onCropChange={setCrop}
                onZoomChange={setZoom}
                onCropComplete={aoCropCompleto}
              />
            </div>

            <div className="flex items-center gap-3">
              <span className="text-xs text-gray-600">Zoom</span>
              <input
                type="range"
                min={1}
                max={4}
                step={0.05}
                value={zoom}
                onChange={(e) => setZoom(Number(e.target.value))}
                className="flex-1 accent-red-600"
              />
              <button
                type="button"
                onClick={() => {
                  setCrop({ x: 0, y: 0 });
                  setZoom(1);
                }}
                className="inline-flex items-center gap-1 text-xs text-gray-600 hover:text-gray-900"
                title="Resetar"
              >
                <RotateCcw className="h-3.5 w-3.5" /> Resetar
              </button>
            </div>

            {erro ? (
              <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {erro}
              </div>
            ) : null}

            <div className="flex justify-end gap-2 pt-2">
              <Button type="button" variante="ghost" onClick={fechar} disabled={salvando}>
                Cancelar
              </Button>
              <Button type="button" onClick={aplicar} disabled={!areaPx || salvando}>
                {salvando ? 'Aplicando…' : 'Aplicar'}
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}
