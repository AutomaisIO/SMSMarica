import { useCallback, useRef, useState } from 'react';
import Cropper, { type Area } from 'react-easy-crop';
import { Check, Loader2, PenTool, RotateCcw, Trash2, Upload } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { arquivoParaDataUrl, recortarRetangularParaBase64 } from '@/shared/lib/imagem';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import {
  FORMATOS_ASSINATURA,
  MODOS_ASSINATURA,
  type FormatoAssinaturaMedico,
  type ModoAssinaturaMedico,
} from '@/features/medicos/assinatura/types';
import {
  useAssinaturaMedico,
  useDefinirModoAssinaturaMedico,
  useModoAssinaturaMedico,
  useRemoverAssinaturaMedico,
  useSalvarAssinaturaMedico,
} from '@/features/medicos/assinatura/api';

type Props = { medicoId: string };

function dimsDoFormato(f: FormatoAssinaturaMedico) {
  return FORMATOS_ASSINATURA.find((x) => x.id === f) ?? FORMATOS_ASSINATURA[0];
}

/**
 * Rubrica visual (imagem de assinatura) do médico. Escolhe o formato (faixa 2:1
 * ou quadrada 1:1), enquadra a imagem na máscara correspondente e salva
 * padronizada (PNG 800×400 / 800×800). O formato é guardado para orientar o
 * carimbo no PDF do laudo. Salva de forma independente do restante do cadastro.
 */
export function AssinaturaMedicoSecao({ medicoId }: Props) {
  const atual = useAssinaturaMedico(medicoId);
  const salvar = useSalvarAssinaturaMedico(medicoId);
  const remover = useRemoverAssinaturaMedico(medicoId);
  const modoAtual = useModoAssinaturaMedico(medicoId);
  const definirModo = useDefinirModoAssinaturaMedico(medicoId);
  const [erroModo, setErroModo] = useState<string | null>(null);

  async function escolherModo(modo: ModoAssinaturaMedico) {
    if (modo === modoAtual.data?.modo && modoAtual.data?.configurado) return;
    setErroModo(null);
    try {
      await definirModo.mutateAsync(modo);
    } catch (err) {
      setErroModo(extrairMensagemDeErro(err));
    }
  }

  const [formato, setFormato] = useState<FormatoAssinaturaMedico>('Horizontal');
  const [arquivoSrc, setArquivoSrc] = useState<string | null>(null);
  const [crop, setCrop] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [areaPx, setAreaPx] = useState<Area | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const dims = dimsDoFormato(formato);

  const aoCropCompleto = useCallback((_: Area, pixels: Area) => setAreaPx(pixels), []);

  async function selecionarArquivo(e: React.ChangeEvent<HTMLInputElement>) {
    const arquivo = e.target.files?.[0];
    e.target.value = '';
    if (!arquivo) return;
    if (!arquivo.type.startsWith('image/')) {
      setErro('Selecione um arquivo de imagem.');
      return;
    }
    if (arquivo.size > 10 * 1024 * 1024) {
      setErro('Imagem muito grande (limite 10 MB antes do enquadramento).');
      return;
    }
    setErro(null);
    try {
      const data = await arquivoParaDataUrl(arquivo);
      setArquivoSrc(data);
      setCrop({ x: 0, y: 0 });
      setZoom(1);
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  function fechar() {
    setArquivoSrc(null);
    setAreaPx(null);
    setCrop({ x: 0, y: 0 });
    setZoom(1);
  }

  async function aplicar() {
    if (!arquivoSrc || !areaPx) return;
    setErro(null);
    try {
      const base64 = await recortarRetangularParaBase64(arquivoSrc, areaPx, dims.largura, dims.altura);
      await salvar.mutateAsync({ imagemBase64: base64, contentType: 'image/png', formato });
      fechar();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  const assinatura = atual.data;

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <h3 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
        <PenTool className="h-4 w-4 text-primary-600" />
        Assinatura (rubrica)
      </h3>
      <p className="mt-1 text-xs text-gray-500">
        Imagem usada para carimbar o laudo. Não é a assinatura digital ICP-Brasil — é a rubrica gráfica.
      </p>

      {/* Modo de assinatura do laudo (ADR-0061) — salva na hora, independente da imagem */}
      <div className="mt-3">
        <span className="text-xs font-medium uppercase tracking-wide text-gray-500">
          Como este médico assina o laudo
        </span>
        <div className="mt-1 grid gap-2 sm:grid-cols-3">
          {MODOS_ASSINATURA.map((m) => {
            const ativo = modoAtual.data?.modo === m.id;
            return (
              <button
                key={m.id}
                type="button"
                onClick={() => escolherModo(m.id)}
                disabled={modoAtual.isPending || definirModo.isPending}
                aria-pressed={ativo}
                className={`rounded-md border px-3 py-2 text-left text-xs disabled:opacity-60 ${
                  ativo
                    ? m.id === 'SemCertificado'
                      ? 'border-amber-400 bg-amber-50 text-amber-900 ring-1 ring-amber-300'
                      : 'border-primary-400 bg-primary-50 text-primary-700 ring-1 ring-primary-300'
                    : 'border-gray-300 bg-white text-gray-700 hover:bg-gray-50'
                }`}
              >
                <span className="flex items-center gap-1 font-medium">
                  {ativo ? <Check className="h-3.5 w-3.5" /> : null}
                  {m.rotulo}
                </span>
                <span className="mt-0.5 block text-[11px] text-gray-500">{m.descricao}</span>
              </button>
            );
          })}
        </div>
        {modoAtual.data && !modoAtual.data.configurado ? (
          <p className="mt-1 text-xs text-gray-500">
            Nenhum modo escolhido ainda — vale o padrão, <strong>Assinador no computador</strong>.
          </p>
        ) : null}
        {modoAtual.data?.modo === 'SemCertificado' ? (
          <p className="mt-1 text-xs text-amber-800">
            Laudo sem assinatura digital não tem validade jurídica plena (Resolução CFM 2.299/2021).
            O PDF e a página do QR Code informam isso a quem conferir.
          </p>
        ) : null}
        {modoAtual.data?.modo === 'Nuvem' ? (
          <p className="mt-1 text-xs text-gray-500">
            O CPF do cadastro precisa ser o mesmo do certificado VIDaaS do médico.
          </p>
        ) : null}
        {erroModo ? <p className="mt-1 text-xs text-red-700">{erroModo}</p> : null}
      </div>

      {/* Formato */}
      <div className="mt-3">
        <span className="text-xs font-medium uppercase tracking-wide text-gray-500">Formato</span>
        <div className="mt-1 flex flex-wrap gap-2">
          {FORMATOS_ASSINATURA.map((f) => (
            <button
              key={f.id}
              type="button"
              onClick={() => setFormato(f.id)}
              className={`rounded-md border px-3 py-1.5 text-left text-xs ${
                formato === f.id
                  ? 'border-primary-400 bg-primary-50 text-primary-700 ring-1 ring-primary-300'
                  : 'border-gray-300 bg-white text-gray-700 hover:bg-gray-50'
              }`}
            >
              <span className="block font-medium">{f.rotulo}</span>
              <span className="block text-[11px] text-gray-500">{f.descricao}</span>
            </button>
          ))}
        </div>
      </div>

      {/* Estado atual */}
      <div className="mt-4">
        {atual.isPending ? (
          <div className="flex items-center gap-2 text-sm text-gray-500">
            <Loader2 className="h-4 w-4 animate-spin" /> Carregando…
          </div>
        ) : assinatura ? (
          <div className="flex flex-wrap items-center gap-4">
            <img
              src={assinatura.imagemBase64}
              alt="Assinatura do médico"
              className="max-h-24 rounded-md border border-gray-200 bg-white p-1"
            />
            <div className="text-xs text-gray-500">
              <p>Formato: {dimsDoFormato(assinatura.formato).rotulo}</p>
              <p>Atualizada em {new Date(assinatura.atualizadoEm).toLocaleString('pt-BR')}</p>
            </div>
            <Button
              type="button"
              variante="ghost"
              tamanho="sm"
              onClick={() => remover.mutate()}
              disabled={remover.isPending}
            >
              <Trash2 className="h-4 w-4" /> Remover
            </Button>
          </div>
        ) : (
          <p className="text-sm text-gray-400">Nenhuma assinatura cadastrada.</p>
        )}
      </div>

      <div className="mt-3">
        <input
          ref={inputRef}
          type="file"
          accept="image/*"
          className="hidden"
          onChange={selecionarArquivo}
        />
        <Button type="button" variante="outline" tamanho="sm" onClick={() => inputRef.current?.click()}>
          <Upload className="h-4 w-4" />
          {assinatura ? 'Trocar assinatura' : 'Enviar assinatura'}
        </Button>
        <p className="mt-1 text-xs text-gray-500">
          PNG ou JPEG. A imagem é enquadrada na máscara {dims.rotulo} e salva em {dims.largura}×{dims.altura}.
        </p>
      </div>

      {erro ? <p className="mt-2 text-xs text-red-700">{erro}</p> : null}

      {/* Modal de enquadramento */}
      <Modal
        aberto={Boolean(arquivoSrc)}
        aoFechar={fechar}
        titulo={`Enquadrar assinatura — ${dims.rotulo}`}
        descricao="Arraste e use o zoom para posicionar a assinatura dentro da máscara. O resultado é salvo padronizado."
        largura="lg"
      >
        {arquivoSrc ? (
          <div className="space-y-4">
            <div
              className="relative w-full overflow-hidden rounded-md bg-gray-900"
              style={{ height: 360 }}
            >
              <Cropper
                image={arquivoSrc}
                crop={crop}
                zoom={zoom}
                aspect={dims.aspecto}
                showGrid
                restrictPosition={false}
                onCropChange={setCrop}
                onZoomChange={setZoom}
                onCropComplete={aoCropCompleto}
              />
            </div>

            <div className="flex items-center gap-3">
              <span className="text-xs text-gray-600">Zoom</span>
              <input
                type="range"
                min={0.5}
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
              <Button type="button" variante="ghost" onClick={fechar} disabled={salvar.isPending}>
                Cancelar
              </Button>
              <Button type="button" onClick={aplicar} disabled={!areaPx || salvar.isPending}>
                {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                Salvar assinatura
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </section>
  );
}
