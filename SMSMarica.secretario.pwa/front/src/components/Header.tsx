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
 * Header fino: marca + título, relógio ao vivo, pill de frescor e o filete de ECG.
 * O tick de 1 s vive AQUI (único consumidor) — o resto da árvore não
 * re-renderiza a cada segundo com o painel aberto em TV.
 */
export function Header({ dados, usandoMock }: Props) {
  const agora = useRelogio();
  const frescor = dados ? calcularFrescor(dados.geradoEm, dados.oracle, agora) : 'desconectado';
  const hora = horaMinutoSegundo(agora);

  return (
    <header className="sticky top-0 z-40 border-b border-linha bg-papel/95 backdrop-blur">
      <div className="mx-auto flex max-w-pagina flex-wrap items-center gap-x-4 gap-y-2 px-4 pb-2 pt-3 sm:px-6">
        <img
          src="/marica_logo.png"
          alt="Prefeitura de Maricá"
          className="h-9 w-auto shrink-0 sm:h-10"
        />
        <div className="min-w-0 flex-1">
          <p className="eyebrow leading-tight">Secretaria de Saúde · Maricá</p>
          <h1 className="font-display text-lg font-bold leading-tight tracking-tight text-tinta sm:text-xl">
            Painel da Saúde
          </h1>
        </div>
        <div className="flex items-center gap-3">
          <time
            className="tnum font-display text-base font-semibold text-tinta sm:text-lg"
            dateTime={agora.toISOString()}
          >
            {hora.slice(0, 5)}
            <span className="text-grafite">{hora.slice(5)}</span>
          </time>
          {dados && (
            <PillFrescor
              geradoEm={dados.geradoEm}
              oracle={dados.oracle}
              agora={agora}
              usandoMock={usandoMock}
            />
          )}
        </div>
      </div>
      <FileteEcg pausado={frescor !== 'vivo'} />
    </header>
  );
}
