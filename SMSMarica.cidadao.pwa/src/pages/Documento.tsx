import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Download, FileCheck2, Clock, Loader2 } from 'lucide-react';
import { http } from '@/lib/httpClient';
import { PrimaryButton } from '@/components/ui';

type Estado = 'carregando' | 'valido' | 'expirado';

/**
 * Página pública (sem login) do link de download enviado ao paciente (ex.: WhatsApp).
 * O token é de uso único: se ainda válido, oferece o download; se já usado/expirado,
 * mostra o aviso e encaminha para o app.
 */
export function Documento() {
  const { token = '' } = useParams();
  const [estado, setEstado] = useState<Estado>('carregando');
  const [descricao, setDescricao] = useState<string | null>(null);

  useEffect(() => {
    let ativo = true;
    http
      .get<{ estado: string; descricao: string | null }>(`/publico/download/${token}/status`)
      .then((r) => {
        if (!ativo) return;
        setDescricao(r.data.descricao);
        setEstado(r.data.estado === 'valido' ? 'valido' : 'expirado');
      })
      .catch(() => {
        if (ativo) setEstado('expirado');
      });
    return () => {
      ativo = false;
    };
  }, [token]);

  const urlDownload = `${http.defaults.baseURL ?? ''}/publico/download/${token}`;

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center bg-areia px-6 py-10">
      <div className="w-full max-w-sm rounded-3xl bg-white p-7 text-center shadow-lg">
        {estado === 'carregando' && (
          <div className="flex flex-col items-center gap-3 py-6 text-tinta-mute">
            <Loader2 className="h-7 w-7 animate-spin text-marica" />
            <p className="text-sm">Carregando…</p>
          </div>
        )}

        {estado === 'valido' && (
          <>
            <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-marica/10">
              <FileCheck2 className="h-7 w-7 text-marica" />
            </div>
            <h1 className="font-display text-xl font-semibold text-tinta">Seu exame está pronto</h1>
            <p className="mt-2 text-sm text-tinta-mute">
              {descricao ? <>Exame: <b>{descricao}</b>.<br /></> : null}
              O download fica disponível <b>uma única vez</b> por este link. Depois, acesse pelo app.
            </p>
            <div className="mt-6">
              <PrimaryButton type="button" onClick={() => window.location.assign(urlDownload)}>
                <span className="inline-flex items-center gap-2">
                  <Download className="h-5 w-5" /> Baixar meu exame
                </span>
              </PrimaryButton>
            </div>
          </>
        )}

        {estado === 'expirado' && (
          <>
            <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-tinta/5">
              <Clock className="h-7 w-7 text-tinta-mute" />
            </div>
            <h1 className="font-display text-xl font-semibold text-tinta">Este link expirou</h1>
            <p className="mt-2 text-sm text-tinta-mute">
              O link de download já foi utilizado ou passou da validade. Para acessar seus exames e
              laudos quando quiser, use o aplicativo Saúde Maricá.
            </p>
            <div className="mt-6">
              <PrimaryButton type="button" onClick={() => window.location.assign('/')}>
                Abrir o app
              </PrimaryButton>
            </div>
          </>
        )}
      </div>

      <p className="mt-6 text-center text-xs text-tinta-mute">Secretaria Municipal de Saúde de Maricá</p>
    </div>
  );
}
