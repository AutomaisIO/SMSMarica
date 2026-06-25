import { QrCode, ScanLine, Smartphone } from 'lucide-react';
import { AppShell } from '@/components/AppShell';

/**
 * Tela mostrada quando não há token (ou ele é inválido/expirado/revogado).
 * Não há login: o único acesso é abrir o app lendo o QR code mostrado na clínica.
 */
export function SemToken() {
  return (
    <AppShell subtitulo="Acesso pelo QR code">
      <div className="flex flex-col items-center text-center">
        <span className="mb-5 grid h-20 w-20 place-items-center rounded-3xl bg-marica/10 text-marica">
          <QrCode className="h-10 w-10" />
        </span>
        <h1 className="font-display text-2xl font-semibold text-tinta">Abra pelo QR code do exame</h1>
        <p className="mt-2 max-w-xs text-sm text-tinta-mute">
          Este aplicativo abre automaticamente quando você lê o QR code que o profissional de saúde
          mostra na tela do atendimento. O código pode ter expirado — peça para gerar de novo.
        </p>

        <ol className="mt-8 w-full space-y-4 text-left">
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
    </AppShell>
  );
}
