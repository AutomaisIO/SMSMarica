/**
 * Rodapé institucional: fecha a página no carvão, com o filete vermelho da marca
 * no topo ecoando a faixa do header.
 *
 * A primeira linha é uma régua só: `smsmarica.online` à esquerda e a assinatura
 * da Automais à direita, na MESMA altura. O rótulo "desenvolvido por" some no
 * celular — é ele que, por ser largo, empurrava a logo para a linha de baixo.
 */
export function Rodape() {
  const ano = new Date().getFullYear();

  return (
    <footer className="mt-4 bg-carvao text-creme">
      {/* filete da marca — o mesmo vermelho que abre a página */}
      <div className="h-[3px] bg-vermelho-marica" />
      <div className="mx-auto max-w-pagina px-4 py-7 sm:px-6">
        <div className="flex items-center justify-between gap-4">
          <a
            href="https://smsmarica.online"
            className="font-display text-[15px] font-bold tracking-tight text-creme transition-colors hover:text-white"
          >
            smsmarica<span className="text-vermelho-marica">.</span>online
          </a>

          <a
            href="https://automais.io"
            className="group flex shrink-0 items-center gap-3"
            aria-label="Automais — desenvolvimento"
          >
            <span className="hidden text-[10.5px] font-medium uppercase tracking-[0.18em] text-creme/40 sm:inline">
              Desenvolvido por
            </span>
            <img
              src="/automais-offwhite.png"
              alt="Automais"
              className="h-[26px] w-auto opacity-80 transition-opacity group-hover:opacity-100"
            />
          </a>
        </div>

        <p className="mt-1.5 text-[12.5px] text-creme/55">
          Secretaria de Saúde de Maricá · {ano}
        </p>
      </div>
    </footer>
  );
}
