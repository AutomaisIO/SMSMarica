import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { urlDaFoto } from '@/features/biometria/lib/rotulos';

/**
 * Foto de rosto carregada sob demanda (cadastro do motorista ou selfie de uma conferência).
 * `base64 === null` = o servidor respondeu que não há foto (404) — mostra `textoSemFoto`.
 */
export function QuadroFoto({
  carregando,
  erro,
  base64,
  textoSemFoto,
  alt,
}: {
  carregando: boolean;
  erro: unknown;
  base64: string | null | undefined;
  textoSemFoto: string;
  alt: string;
}) {
  if (carregando) {
    return (
      <div className="flex h-64 items-center justify-center rounded-md bg-gray-50 text-sm text-gray-500">
        Carregando foto…
      </div>
    );
  }
  if (erro) {
    return (
      <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {extrairMensagemDeErro(erro)}
      </div>
    );
  }
  if (!base64) {
    return (
      <div className="flex h-40 items-center justify-center rounded-md border border-dashed border-gray-300 bg-gray-50 px-4 text-center text-sm text-gray-600">
        {textoSemFoto}
      </div>
    );
  }
  return (
    <div className="flex justify-center rounded-md bg-gray-900 p-2">
      <img src={urlDaFoto(base64)} alt={alt} className="max-h-[60vh] w-auto rounded object-contain" />
    </div>
  );
}
