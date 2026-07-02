import { useCallback, useRef, useState } from 'react';
import Cropper, { type Area } from 'react-easy-crop';
import { Camera, Images, RotateCcw } from 'lucide-react';
import { arquivoParaDataUrl, recortarParaBase64 } from '@/lib/imagem';
import { Avatar, GhostButton, PrimaryButton } from '@/components/ui';

const TAMANHO_FINAL = 512;

/**
 * Avatar grande com botão de câmera. Ao escolher um arquivo, abre uma folha de
 * recorte (pan/zoom, máscara redonda) e devolve o base64 quadrado 512×512.
 */
export function AvatarUploader({
  valor,
  nome,
  aoMudar,
  salvando,
}: {
  valor: string | null;
  nome: string;
  aoMudar: (base64: string) => void;
  salvando?: boolean;
}) {
  const [src, setSrc] = useState<string | null>(null);
  const [crop, setCrop] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [areaPx, setAreaPx] = useState<Area | null>(null);
  const [processando, setProcessando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [menu, setMenu] = useState(false);
  const galeriaRef = useRef<HTMLInputElement>(null);
  const cameraRef = useRef<HTMLInputElement>(null);

  const aoCompletar = useCallback((_: Area, px: Area) => setAreaPx(px), []);

  async function escolher(e: React.ChangeEvent<HTMLInputElement>) {
    setMenu(false);
    const arquivo = e.target.files?.[0];
    e.target.value = '';
    if (!arquivo) return;
    if (!arquivo.type.startsWith('image/')) return setErro('Selecione uma imagem.');
    if (arquivo.size > 10 * 1024 * 1024) return setErro('Imagem muito grande (máx. 10 MB).');
    setErro(null);
    try {
      setSrc(await arquivoParaDataUrl(arquivo));
      setCrop({ x: 0, y: 0 });
      setZoom(1);
    } catch {
      setErro('Não foi possível abrir a imagem.');
    }
  }

  function fechar() {
    setSrc(null);
    setAreaPx(null);
  }

  async function aplicar() {
    if (!src || !areaPx) return;
    setProcessando(true);
    try {
      aoMudar(await recortarParaBase64(src, areaPx, TAMANHO_FINAL));
      fechar();
    } catch {
      setErro('Falha ao recortar a foto.');
    } finally {
      setProcessando(false);
    }
  }

  return (
    <div className="flex flex-col items-center">
      <div className="relative">
        <Avatar src={valor} nome={nome} size={104} className="shadow-carta ring-4 ring-white" />
        <button
          type="button"
          onClick={() => setMenu((v) => !v)}
          disabled={salvando}
          aria-label="Trocar foto"
          className="absolute -bottom-1 -right-1 grid h-10 w-10 place-items-center rounded-full bg-marica text-white shadow-carta ring-4 ring-papel transition active:scale-95 disabled:opacity-50"
        >
          <Camera className="h-5 w-5" />
        </button>

        {menu && (
          <>
            <button
              type="button"
              aria-label="Fechar"
              onClick={() => setMenu(false)}
              className="fixed inset-0 z-40 cursor-default"
            />
            <div className="absolute left-1/2 top-full z-50 mt-3 w-56 -translate-x-1/2 overflow-hidden rounded-2xl border border-areia bg-white shadow-2xl">
              <button
                type="button"
                onClick={() => cameraRef.current?.click()}
                className="flex w-full items-center gap-3 px-4 py-3.5 text-left text-[15px] font-medium text-tinta transition active:bg-papel"
              >
                <Camera className="h-5 w-5 text-marica" /> Tirar foto
              </button>
              <button
                type="button"
                onClick={() => galeriaRef.current?.click()}
                className="flex w-full items-center gap-3 border-t border-areia px-4 py-3.5 text-left text-[15px] font-medium text-tinta transition active:bg-papel"
              >
                <Images className="h-5 w-5 text-lagoa" /> Escolher da galeria
              </button>
            </div>
          </>
        )}
      </div>
      {/* Câmera: capture aciona a câmera frontal direto no celular; galeria abre o seletor de arquivos. */}
      <input ref={cameraRef} type="file" accept="image/*" capture="user" className="hidden" onChange={escolher} />
      <input ref={galeriaRef} type="file" accept="image/*" className="hidden" onChange={escolher} />
      {erro && <p className="mt-3 text-sm text-marica">{erro}</p>}

      {src && (
        <div className="fixed inset-0 z-50 mx-auto flex h-[100dvh] max-w-[460px] flex-col bg-tinta">
          {/* Controles no TOPO: em alguns celulares a barra do navegador cobre o rodapé e o botão some. */}
          <div className="space-y-4 bg-papel px-5 pb-4 pt-[calc(env(safe-area-inset-top)+1rem)]">
            <div className="flex gap-3">
              <GhostButton className="flex-1" onClick={fechar}>
                Cancelar
              </GhostButton>
              <PrimaryButton className="flex-1" onClick={aplicar} carregando={processando}>
                Usar foto
              </PrimaryButton>
            </div>
            <div className="flex items-center gap-3">
              <Camera className="h-4 w-4 text-tinta-mute" />
              <input
                type="range"
                min={1}
                max={4}
                step={0.02}
                value={zoom}
                onChange={(e) => setZoom(Number(e.target.value))}
                className="flex-1 accent-marica"
                aria-label="Zoom"
              />
              <button
                type="button"
                onClick={() => {
                  setCrop({ x: 0, y: 0 });
                  setZoom(1);
                }}
                aria-label="Resetar"
                className="text-tinta-mute"
              >
                <RotateCcw className="h-4 w-4" />
              </button>
            </div>
          </div>
          <div className="relative flex-1">
            <Cropper
              image={src}
              crop={crop}
              zoom={zoom}
              aspect={1}
              cropShape="round"
              showGrid={false}
              onCropChange={setCrop}
              onZoomChange={setZoom}
              onCropComplete={aoCompletar}
            />
          </div>
        </div>
      )}
    </div>
  );
}
