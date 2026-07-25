import type { Painel } from '@/types/painel';
import { horaMinutoSegundo } from '@/lib/formatos';
import { useRelogio } from '@/lib/useRelogio';
import { FileteEcg } from '@/components/FileteEcg';
import { PillFrescor, calcularFrescor } from '@/components/PillFrescor';

interface Props {
  dados: Painel | null;
  usandoMock: boolean;
}

/**
 * Faixa vermelha da marca: no celular ela sobe até a barra de status (safe-area +
 * status bar translúcida no iOS), então o app instalado abre com o topo vermelho
 * em vez da moldura do navegador.
 *
 * O tick de 1 s vive AQUI (único consumidor) — o resto da árvore não re-renderiza
 * a cada segundo com o painel aberto em TV.
 */
export function Header({ dados, usandoMock }: Props) {
  const agora = useRelogio();
  const frescor = dados ? calcularFrescor(dados.geradoEm, dados.status, agora) : 'desconectado';
  const hora = horaMinutoSegundo(agora);

  return (
    <header className="sticky top-0 z-40 bg-vermelho-marica text-white shadow-[0_2px_14px_-6px_rgba(19,26,34,0.45)]">
      <div className="mx-auto flex max-w-pagina flex-wrap items-center gap-x-3 gap-y-2 px-4 pb-2.5 pt-[calc(env(safe-area-inset-top)+0.75rem)] sm:gap-x-4 sm:px-6">
        {/* Logo da Prefeitura em branco (fundo vermelho) — o PNG é vermelho sobre
            transparente, então invertemos para o negativo da marca. */}
        <img
          src="/marica_logo.png"
          alt="Prefeitura de Maricá"
          className="h-8 w-auto shrink-0 brightness-0 invert sm:h-10"
        />
        {/* min-w garante que, faltando espaço, quem quebra de linha é o relógio —
            não o título virando torre de uma palavra por linha. */}
        <div className="min-w-[132px] flex-1">
          <p className="truncate text-[10px] font-semibold uppercase leading-tight tracking-[0.14em] text-white/75 sm:text-[11px]">
            Secretaria de Saúde · Maricá
          </p>
          <h1 className="truncate font-display text-[17px] font-bold leading-tight tracking-tight sm:text-xl">
            Painel da Saúde
          </h1>
        </div>
        <div className="ml-auto flex shrink-0 items-center gap-2.5 sm:gap-3">
          <time
            className="tnum font-display text-[15px] font-semibold sm:text-lg"
            dateTime={agora.toISOString()}
          >
            {hora.slice(0, 5)}
            <span className="hidden text-white/60 sm:inline">{hora.slice(5)}</span>
          </time>
          {dados && (
            <PillFrescor
              geradoEm={dados.geradoEm}
              status={dados.status}
              agora={agora}
              usandoMock={usandoMock}
            />
          )}
        </div>
      </div>
      {/* Respiro de vermelho abaixo do traçado: sem ele o ECG encosta na borda e
          o corte contra o conteúdo claro fica duro. */}
      <div className="pb-3">
        <FileteEcg pausado={frescor !== 'vivo'} />
      </div>
    </header>
  );
}
