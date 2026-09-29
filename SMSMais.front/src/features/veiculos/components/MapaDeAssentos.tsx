import { useMemo } from 'react';
import { Accessibility, Armchair, Crown, HeartHandshake, Lock } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import {
  ROTULOS_TIPO_ASSENTO,
  TIPOS_ASSENTO,
  type TipoAssento,
} from '@/features/veiculos/types';
import { montarPlanta, type VeiculoPlanta } from '@/features/veiculos/lib/plantaVeiculo';

export type CelulaAssento = {
  /** Número sequencial dentro da fileira (1..N), da esquerda (lado do motorista) para a direita. */
  numero: number;
  /** Tipo atual do assento. */
  tipo: TipoAssento;
  /** Assento bloqueado operacionalmente (banco quebrado, reservado etc.). */
  bloqueado?: boolean;
};

export type LinhaLayout = {
  ordem: number;
  assentos: CelulaAssento[];
};

type AssentoRender = {
  fileiraOrdem: number;
  numero: number;
  tipo: TipoAssento;
};

type Props = {
  /** Layout do veículo, fileiras ordenadas da frente para trás. */
  linhas: LinhaLayout[];
  /** Veículo dono do layout: o modelo escolhe a planta, a cor pinta a carroceria. */
  veiculo: VeiculoPlanta;
  /**
   * Se definido, clicar no assento chama `onClickAssento` com as coordenadas.
   * Use para marcar tipo, alocar paciente etc. Se ausente, mapa é somente leitura.
   */
  onClickAssento?: (posicao: { fileiraOrdem: number; numero: number }) => void;
  /**
   * Render custom opcional para sobrepor o conteúdo do assento
   * (ex.: iniciais do paciente alocado). Recebe o assento; retornar `null`
   * renderiza o layout padrão.
   */
  renderAssento?: (assento: AssentoRender) => React.ReactNode | null;
  /**
   * Assento destacado — útil para destacar uma seleção ou o assento sob o cursor.
   */
  destacado?: { fileiraOrdem: number; numero: number } | null;
  className?: string;
};

const CORES: Record<TipoAssento, { base: string; hover: string; icone: React.ElementType }> = {
  [TIPOS_ASSENTO.Motorista]: {
    base: 'bg-amber-100 border-amber-400 text-amber-800',
    hover: 'hover:bg-amber-200',
    icone: Crown,
  },
  [TIPOS_ASSENTO.Passageiro]: {
    base: 'bg-emerald-50 border-emerald-400 text-emerald-800',
    hover: 'hover:bg-emerald-100',
    icone: Armchair,
  },
  [TIPOS_ASSENTO.Acompanhante]: {
    base: 'bg-sky-50 border-sky-400 text-sky-800',
    hover: 'hover:bg-sky-100',
    icone: HeartHandshake,
  },
  [TIPOS_ASSENTO.Cadeirante]: {
    base: 'bg-violet-50 border-violet-400 text-violet-800',
    hover: 'hover:bg-violet-100',
    icone: Accessibility,
  },
};

const COR_BLOQUEADO = {
  base: 'bg-gray-100 border-gray-300 text-gray-400',
  hover: 'hover:bg-gray-200',
  icone: Lock,
};

const pct = (valor: number, origem: number, total: number) => `${((valor - origem) / total) * 100}%`;

/**
 * Mapa de assentos desenhado na PLANTA do veículo: vista de cima, frente à direita, teto tirado.
 * Cada banco fica onde fica no carro de verdade (ver `plantaVeiculo.ts`); o encosto é a borda
 * grossa, do lado de trás. Os bancos são botões por cima do desenho.
 */
export function MapaDeAssentos({
  linhas,
  veiculo,
  onClickAssento,
  renderAssento,
  destacado,
  className,
}: Props) {
  const { tipo, modelo, fabricante, cor } = veiculo;
  const planta = useMemo(
    () => montarPlanta({ tipo, modelo, fabricante, cor }, linhas),
    [tipo, modelo, fabricante, cor, linhas],
  );
  const [vx, vy, vw, vh] = planta.caixa;
  const clicavel = Boolean(onClickAssento);
  // Largura na tela proporcional ao comprimento real: um Onix não fica do tamanho de uma Sprinter.
  // O mínimo só evita banco ilegível; abaixo dele a planta rola — e rola a partir da frente.
  const largura = {
    minWidth: `${Math.round(planta.comprimento * 40)}px`,
    maxWidth: `${Math.round(planta.comprimento * 150)}px`,
  };

  return (
    <div className={cn('flex flex-col gap-3', className)}>
      <div className="w-full overflow-x-auto pb-1" dir="rtl">
        <div className="mx-auto" style={largura} dir="ltr">
          <div className="relative" style={{ aspectRatio: `${vw} / ${vh}` }}>
            <div
              aria-hidden="true"
              className="absolute inset-0 [&>svg]:h-full [&>svg]:w-full"
              // SVG montado só com números e cores calculadas — nenhum texto do cadastro.
              dangerouslySetInnerHTML={{ __html: planta.svg }}
            />
            {planta.assentos.map((a) => {
              const estilo = a.bloqueado ? COR_BLOQUEADO : CORES[a.tipo];
              const destacadoAqui =
                destacado?.fileiraOrdem === a.fileiraOrdem && destacado?.numero === a.numero;
              const conteudoCustom =
                renderAssento?.({ fileiraOrdem: a.fileiraOrdem, numero: a.numero, tipo: a.tipo }) ?? null;
              const Icone = estilo.icone;
              const rotulo = `${a.bloqueado ? 'Bloqueado' : ROTULOS_TIPO_ASSENTO[a.tipo]} — F${a.fileiraOrdem}·${a.numero}`;
              return (
                <button
                  key={`${a.fileiraOrdem}-${a.numero}`}
                  type="button"
                  disabled={!clicavel}
                  onClick={() => onClickAssento?.({ fileiraOrdem: a.fileiraOrdem, numero: a.numero })}
                  title={rotulo}
                  aria-label={rotulo}
                  className={cn(
                    'absolute flex flex-col items-center justify-center gap-0.5 overflow-hidden rounded-[28%] border-2 border-l-[5px] text-[10px] font-semibold leading-none shadow-sm transition',
                    estilo.base,
                    clicavel && estilo.hover,
                    clicavel ? 'cursor-pointer' : 'cursor-default',
                    destacadoAqui && 'ring-2 ring-red-500 ring-offset-1',
                  )}
                  style={{
                    left: pct(a.cx - a.profundidade / 2, vx, vw),
                    top: pct(a.cy - a.largura / 2, vy, vh),
                    width: pct(a.profundidade, 0, vw),
                    height: pct(a.largura, 0, vh),
                  }}
                >
                  {conteudoCustom ?? (
                    <>
                      <Icone className="h-3 w-3 shrink-0" />
                      <span>{a.numero}</span>
                    </>
                  )}
                </button>
              );
            })}
          </div>
          <div className="relative h-4 text-[10px] font-semibold text-gray-500">
            {planta.fileiras.map((f) => (
              <span
                key={f.ordem}
                className="absolute -translate-x-1/2"
                style={{ left: pct(f.cx, vx, vw) }}
              >
                F{f.ordem}
              </span>
            ))}
            <span className="absolute right-0 top-0 font-medium uppercase tracking-wide text-gray-400">
              frente ›
            </span>
          </div>
        </div>
      </div>

      <Legenda />
    </div>
  );
}

function Legenda() {
  const itens: { tipo: TipoAssento; bloqueado?: boolean }[] = [
    { tipo: TIPOS_ASSENTO.Motorista },
    { tipo: TIPOS_ASSENTO.Passageiro },
    { tipo: TIPOS_ASSENTO.Acompanhante },
    { tipo: TIPOS_ASSENTO.Cadeirante },
    { tipo: TIPOS_ASSENTO.Passageiro, bloqueado: true },
  ];
  const rotulos: Record<string, string> = {
    ...ROTULOS_TIPO_ASSENTO,
    bloqueado: 'Bloqueado',
  };
  return (
    <div className="flex flex-wrap items-center gap-3 pt-1 text-xs text-gray-600">
      {itens.map(({ tipo, bloqueado }, i) => {
        const cor = bloqueado ? COR_BLOQUEADO : CORES[tipo];
        const Icone = cor.icone;
        const rotulo = bloqueado ? 'Bloqueado' : rotulos[tipo];
        return (
          <span key={i} className="inline-flex items-center gap-1.5">
            <span className={cn('inline-flex h-5 w-5 items-center justify-center rounded border-2', cor.base)}>
              <Icone className="h-3 w-3" />
            </span>
            {rotulo}
          </span>
        );
      })}
    </div>
  );
}
