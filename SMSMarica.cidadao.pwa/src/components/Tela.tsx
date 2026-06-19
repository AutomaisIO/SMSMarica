import clsx from 'clsx';

export function Tela({
  titulo,
  acao,
  children,
}: {
  titulo: string;
  acao?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="min-h-full flex flex-col">
      <header className="bg-marica text-white px-4 py-4 flex items-center justify-between shadow">
        <h1 className="text-lg font-semibold">{titulo}</h1>
        {acao}
      </header>
      <main className="flex-1 px-4 py-5 max-w-xl w-full mx-auto">{children}</main>
      <footer className="px-4 py-4 text-center text-xs text-neutral-500">
        <a className="text-marica underline" href="/privacidade/">
          Privacidade
        </a>
        {' · '}
        <a className="text-marica underline" href="/termos/">
          Termos
        </a>
        <div className="mt-1">Secretaria Municipal de Saúde de Maricá</div>
      </footer>
    </div>
  );
}

export function Botao({
  className,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      className={clsx(
        'w-full rounded-xl bg-marica px-4 py-3 font-semibold text-white',
        'disabled:opacity-50 active:bg-marica-escuro transition',
        className,
      )}
      {...props}
    />
  );
}
