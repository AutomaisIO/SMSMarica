import { HeartPulse } from 'lucide-react';

/** Moldura das telas pré-login (CPF / código). Faixa vermelha com a assinatura,
 * cartão branco com a logo oficial de Maricá e o conteúdo. */
export function AuthShell({
  titulo,
  subtitulo,
  children,
}: {
  titulo: string;
  subtitulo: string;
  children: React.ReactNode;
}) {
  return (
    <div className="mx-auto flex min-h-dvh max-w-[460px] flex-col bg-papel shadow-2xl">
      <div className="guilloche flex items-center gap-2 bg-gradient-to-br from-vinho to-marica px-6 pb-14 pt-10 text-white">
        <span className="grid h-9 w-9 place-items-center rounded-xl bg-white/15 ring-1 ring-white/25">
          <HeartPulse className="h-5 w-5" />
        </span>
        <span className="text-sm font-semibold uppercase tracking-[0.2em] text-white/90">
          App do Cidadão
        </span>
      </div>

      <div className="-mt-8 flex-1 rounded-t-3xl bg-papel px-6 pb-10 pt-7">
        <img
          src="/marica_logo.png"
          alt="Prefeitura de Maricá — cidade que cuida, transforma e inspira"
          className="mx-auto mb-7 h-auto w-[210px]"
        />
        <h1 className="font-display text-2xl font-semibold text-tinta">{titulo}</h1>
        <p className="mt-1 mb-6 text-[15px] text-tinta-mute">{subtitulo}</p>
        {children}
      </div>

      <footer className="px-6 pb-6 text-center text-xs text-tinta-mute">
        <a className="underline" href="/privacidade/">Privacidade</a>
        {' · '}
        <a className="underline" href="/termos/">Termos</a>
      </footer>
    </div>
  );
}
