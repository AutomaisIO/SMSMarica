import { ChevronLeft } from 'lucide-react';

/**
 * Moldura do app (phone-first, tema da instância via env de build (ADR-0046)). Sem menu/navegação: o fluxo
 * é linear (Início → Novo Documento → Scanner → Sucesso). Cabeçalho fixo com título
 * e, quando aplicável, botão de voltar; rodapé com a versão (para suporte).
 */
export function AppShell({
  children,
  subtitulo,
  aoVoltar,
}: {
  children: React.ReactNode;
  subtitulo?: string;
  aoVoltar?: () => void;
}) {
  return (
    <div className="mx-auto flex min-h-dvh max-w-[460px] flex-col bg-papel shadow-2xl">
      <header className="sticky top-0 z-30 flex items-center gap-2 bg-marica px-3 pb-3 pt-[calc(env(safe-area-inset-top)+0.75rem)] text-white shadow-topo">
        {aoVoltar ? (
          <button
            type="button"
            onClick={aoVoltar}
            aria-label="Voltar"
            className="grid h-11 w-11 place-items-center rounded-xl transition active:bg-white/15"
          >
            <ChevronLeft className="h-6 w-6" />
          </button>
        ) : (
          <span className="grid h-11 w-11 place-items-center">
            <img src="/icon-192.png" alt="" className="h-8 w-8 rounded-lg" />
          </span>
        )}
        <div className="flex flex-1 flex-col leading-tight">
          <span className="font-display text-[15px] font-semibold tracking-tight">
            {import.meta.env.VITE_APP_NOME || 'Arquivos Saúde'}
          </span>
          <span className="text-[11px] font-medium text-white/80">
            {subtitulo ?? 'Envio de exames'}
          </span>
        </div>
      </header>

      <main className="flex-1 px-4 pb-10 pt-5">{children}</main>

      <footer className="px-4 pb-[calc(env(safe-area-inset-bottom)+0.75rem)] pt-2">
        <p className="text-center text-[11px] font-medium text-tinta-mute/70">
          {import.meta.env.VITE_APP_RODAPE || 'Saúde'} · versão {__APP_VERSION__}
        </p>
      </footer>
    </div>
  );
}
