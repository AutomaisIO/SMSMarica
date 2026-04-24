import { useMemo } from 'react';
import { Armchair, Crown, HeartHandshake, Lock } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import {
  ROTULOS_TIPO_ASSENTO,
  TIPOS_ASSENTO,
  type TipoAssento,
} from '@/features/veiculos/types';

export type CelulaAssento = {
  /** Número sequencial dentro da fileira (1..N). */
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
  /** Maior nº de assentos em uma fileira. Se não informado, calculado. */
  colunas?: number;
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
};

const COR_BLOQUEADO = {
  base: 'bg-gray-100 border-gray-300 text-gray-400',
  hover: 'hover:bg-gray-200',
  icone: Lock,
};

export function MapaDeAssentos({
  linhas,
  onClickAssento,
  renderAssento,
  destacado,
  colunas,
  className,
}: Props) {
  const totalColunas = useMemo(() => {
    if (colunas && colunas > 0) return colunas;
    return Math.max(1, ...linhas.map((l) => l.assentos.length));
  }, [colunas, linhas]);

  const linhasOrdenadas = useMemo(
    () => [...linhas].sort((a, b) => a.ordem - b.ordem),
    [linhas],
  );

  const clicavel = Boolean(onClickAssento);

  return (
    <div className={cn('flex flex-col items-center gap-3', className)}>
      <FrenteDoVeiculo colunas={totalColunas} />

      <div className="flex w-full flex-col gap-2">
        {linhasOrdenadas.map((linha) => (
          <div key={linha.ordem} className="flex items-center gap-2">
            <span className="w-6 shrink-0 text-center text-xs font-semibold text-gray-500">
              F{linha.ordem}
            </span>
            <div
              className="grid flex-1 gap-2"
              style={{ gridTemplateColumns: `repeat(${totalColunas}, minmax(0, 1fr))` }}
            >
              {Array.from({ length: totalColunas }).map((_, colIdx) => {
                const assento = linha.assentos[colIdx];
                if (!assento) {
                  return <div key={`vazio-${colIdx}`} className="h-12" />;
                }
                const cor = assento.bloqueado ? COR_BLOQUEADO : CORES[assento.tipo];
                const destacadoAqui =
                  destacado?.fileiraOrdem === linha.ordem &&
                  destacado?.numero === assento.numero;
                const conteudoCustom =
                  renderAssento?.({
                    fileiraOrdem: linha.ordem,
                    numero: assento.numero,
                    tipo: assento.tipo,
                  }) ?? null;
                const Icone = cor.icone;
                return (
                  <button
                    key={assento.numero}
                    type="button"
                    disabled={!clicavel}
                    onClick={() =>
                      onClickAssento?.({
                        fileiraOrdem: linha.ordem,
                        numero: assento.numero,
                      })
                    }
                    title={`${ROTULOS_TIPO_ASSENTO[assento.tipo]} — F${linha.ordem}·${assento.numero}`}
                    className={cn(
                      'flex h-12 w-full flex-col items-center justify-center rounded-md border-2 text-[10px] font-semibold leading-tight transition',
                      cor.base,
                      clicavel && cor.hover,
                      clicavel && 'cursor-pointer',
                      !clicavel && 'cursor-default',
                      destacadoAqui && 'ring-2 ring-offset-1 ring-red-500',
                    )}
                  >
                    {conteudoCustom ?? (
                      <>
                        <Icone className="h-3.5 w-3.5" />
                        <span>{assento.numero}</span>
                      </>
                    )}
                  </button>
                );
              })}
            </div>
          </div>
        ))}
      </div>

      <Legenda />
    </div>
  );
}

function FrenteDoVeiculo({ colunas }: { colunas: number }) {
  return (
    <div className="flex w-full items-center gap-2">
      <span className="w-6" />
      <div
        className="grid flex-1"
        style={{ gridTemplateColumns: `repeat(${colunas}, minmax(0, 1fr))` }}
      >
        <div
          className="col-span-full flex h-6 items-center justify-center rounded-t-2xl border-2 border-b-0 border-gray-300 bg-gray-50 text-[10px] font-semibold uppercase tracking-wide text-gray-500"
        >
          Frente do veículo
        </div>
      </div>
    </div>
  );
}

function Legenda() {
  const itens: { tipo: TipoAssento; bloqueado?: boolean }[] = [
    { tipo: TIPOS_ASSENTO.Motorista },
    { tipo: TIPOS_ASSENTO.Passageiro },
    { tipo: TIPOS_ASSENTO.Acompanhante },
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
