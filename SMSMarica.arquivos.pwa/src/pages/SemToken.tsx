import { useState } from 'react';
import { QrCode, ScanLine, Smartphone } from 'lucide-react';
import { AppShell } from '@/components/AppShell';
import { LeitorQr } from '@/components/LeitorQr';
import { PrimaryButton } from '@/components/ui';

/**
 * Tela mostrada quando não há token (ou ele é inválido/expirado/revogado).
 * Acesso: ler o QR code mostrado na clínica — pela câmera nativa (abre a URL `?t=`)
 * ou pelo botão "Escanear QR code", útil quando o PWA já está instalado/aberto.
 */
export function SemToken({ aoLerToken }: { aoLerToken: (token: string) => void }) {
  const [lendo, setLendo] = useState(false);

  if (lendo) {
    return (
      <LeitorQr
        onResultado={(token) => {
          // O App troca de tela (validando → Home) e desmonta este leitor (a câmera para).
          aoLerToken(token);
        }}
        aoFechar={() => setLendo(false)}
      />
    );
  }

  return (
    <AppShell subtitulo="Acesso pelo QR code">
      <div className="flex flex-col items-center text-center">
        <span className="mb-5 grid h-20 w-20 place-items-center rounded-3xl bg-marica/10 text-marica">
          <QrCode className="h-10 w-10" />
        </span>
        <h1 className="font-display text-2xl font-semibold text-tinta">Abra pelo QR code do exame</h1>
        <p className="mt-2 max-w-xs text-sm text-tinta-mute">
          Aponte para o QR code que o profissional de saúde mostra na tela do atendimento. Se o app
          já estiver instalado, use o botão abaixo para escanear sem sair daqui.
        </p>

        <PrimaryButton className="mt-6" onClick={() => setLendo(true)}>
          <ScanLine className="h-5 w-5" /> Escanear QR code
        </PrimaryButton>

        <div className="mt-8 w-full">
          <p className="mb-3 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-marica">
            Ou pela câmera do celular
          </p>
          <ol className="w-full space-y-4 text-left">
            <li className="flex items-start gap-3">
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-marica/10 font-display text-sm font-bold text-marica">
                1
              </span>
              <span className="flex items-center gap-2 pt-1 text-sm text-tinta">
                <Smartphone className="h-4 w-4 text-lagoa" /> Abra a câmera do celular.
              </span>
            </li>
            <li className="flex items-start gap-3">
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-marica/10 font-display text-sm font-bold text-marica">
                2
              </span>
              <span className="flex items-center gap-2 pt-1 text-sm text-tinta">
                <ScanLine className="h-4 w-4 text-lagoa" /> Aponte para o QR code na tela da clínica.
              </span>
            </li>
            <li className="flex items-start gap-3">
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-marica/10 font-display text-sm font-bold text-marica">
                3
              </span>
              <span className="pt-1 text-sm text-tinta">
                Toque no link que aparecer — o envio de exames abre já liberado.
              </span>
            </li>
          </ol>
        </div>

        <p className="mt-6 rounded-xl bg-areia/50 px-3 py-2 text-xs text-tinta-mute">
          O código pode ter expirado — se não funcionar, peça para o profissional gerar de novo.
        </p>
      </div>
    </AppShell>
  );
}
