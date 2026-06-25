import { FileScan, Plus, ScanLine } from 'lucide-react';
import { AppShell } from '@/components/AppShell';
import { InstalarApp } from '@/components/InstalarApp';
import { Card, PrimaryButton } from '@/components/ui';
import type { SessaoValida } from '@/lib/api';

/**
 * Tela inicial após validar o token: mostra para quem é o envio (nome do paciente)
 * e o botão grande "Novo Documento".
 */
export function Home({ sessao, aoNovo }: { sessao: SessaoValida; aoNovo: () => void }) {
  return (
    <AppShell subtitulo="Envio de exames">
      <div className="space-y-5">
        <InstalarApp />

        <Card className="p-5">
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-marica">Paciente</p>
          <p className="mt-1 font-display text-xl font-semibold text-tinta">{sessao.paciente.nome}</p>
          {sessao.solicitacao.resumo && (
            <p className="mt-1 text-sm text-tinta-mute">{sessao.solicitacao.resumo}</p>
          )}
        </Card>

        <div className="flex items-start gap-3 rounded-2xl border border-areia bg-white/60 p-4">
          <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-lagoa-claro text-lagoa">
            <FileScan className="h-5 w-5" />
          </span>
          <div>
            <p className="font-display text-sm font-semibold text-tinta">Como funciona</p>
            <p className="text-sm text-tinta-mute">
              Fotografe folha por folha do exame antigo. O app recorta, melhora a imagem e monta um
              único arquivo PDF para o profissional revisar.
            </p>
          </div>
        </div>

        <PrimaryButton onClick={aoNovo}>
          <Plus className="h-5 w-5" /> Novo Documento
        </PrimaryButton>

        <p className="flex items-center justify-center gap-1.5 text-center text-xs text-tinta-mute">
          <ScanLine className="h-3.5 w-3.5" /> As fotos são processadas no seu aparelho.
        </p>
      </div>
    </AppShell>
  );
}
