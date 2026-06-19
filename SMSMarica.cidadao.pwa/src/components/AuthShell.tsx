import { HeartPulse } from 'lucide-react';

/** Moldura das telas pré-login (CPF / código). Hero civismo com a marca centralizada,
 * folha de "papel" que recebe a logo oficial de Maricá e o conteúdo — tudo no eixo
 * central, com cara de app (não de formulário web). */
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
      {/* Hero — faixa guilloché com a marca da Saúde centralizada. */}
      <div className="guilloche bg-gradient-to-br from-vinho to-marica px-6 pb-20 pt-12 text-white">
        <div className="flex flex-col items-center text-center">
          <span className="grid h-16 w-16 place-items-center rounded-[1.25rem] bg-white/15 ring-1 ring-white/25">
            <HeartPulse className="h-8 w-8" />
          </span>
          <span className="mt-3 text-[11px] font-semibold uppercase tracking-[0.3em] text-white/85">
            App do Cidadão
          </span>
        </div>
      </div>

      {/* Folha de papel: logo + conteúdo, centralizados no espaço restante. */}
      <div className="-mt-12 flex flex-1 flex-col rounded-t-[2rem] bg-papel px-6 pb-8 pt-9 shadow-[0_-14px_44px_-26px_rgba(110,19,34,0.55)]">
        <div className="flex flex-1 flex-col justify-center pb-6">
          <div className="animate-rise">
            <img
              src="/marica_logo.png"
              alt="Prefeitura de Maricá — cidade que cuida, transforma e inspira"
              className="mx-auto mb-8 h-auto w-[188px]"
            />
            <h1 className="text-center font-display text-[26px] font-semibold leading-tight text-tinta">
              {titulo}
            </h1>
            <p className="mx-auto mb-8 mt-2 max-w-[19rem] text-center text-[15px] leading-relaxed text-tinta-mute">
              {subtitulo}
            </p>
            {children}
          </div>
        </div>
      </div>

      <footer className="px-6 pb-6 text-center text-xs text-tinta-mute">
        <a className="underline" href="/privacidade/">Privacidade</a>
        {' · '}
        <a className="underline" href="/termos/">Termos</a>
      </footer>
    </div>
  );
}
