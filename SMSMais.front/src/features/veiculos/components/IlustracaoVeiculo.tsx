import { useMemo } from 'react';
import { cn } from '@/shared/lib/cn';
import { ROTULOS_TIPO_VEICULO, type TipoVeiculo } from '@/features/veiculos/types';
import { svgVeiculo } from '@/features/veiculos/lib/desenhoVeiculo';
import {
  CORES_FANTASIA,
  contornoDaCor,
  resolverCorVeiculo,
} from '@/features/veiculos/lib/corVeiculo';

type Props = {
  tipo: TipoVeiculo;
  modelo?: string | null;
  fabricante?: string | null;
  cor: string;
  /** Selo de acessibilidade. Se omitido, deduzido do nome do modelo ("… ADAPTADA"). */
  adaptado?: boolean;
  className?: string;
};

/** Desenho lateral do veículo, do modelo cadastrado, pintado com a cor cadastrada. */
export function IlustracaoVeiculo({ tipo, modelo, fabricante, cor, adaptado, className }: Props) {
  const svg = useMemo(
    () => svgVeiculo({ tipo, modelo, fabricante, cor, adaptado }),
    [tipo, modelo, fabricante, cor, adaptado],
  );
  const rotulo = [ROTULOS_TIPO_VEICULO[tipo], fabricante, modelo, cor].filter(Boolean).join(' · ');
  return (
    <span
      role="img"
      aria-label={rotulo}
      className={cn('inline-block [&>svg]:block [&>svg]:h-auto [&>svg]:w-full', className)}
      // O SVG é montado só com números e cores calculadas — nenhum texto do cadastro entra nele.
      dangerouslySetInnerHTML={{ __html: svg }}
    />
  );
}

/** Bolinha com a cor do veículo, para acompanhar o nome da cor em texto. */
export function AmostraCorVeiculo({ cor, className }: { cor: string; className?: string }) {
  const r = resolverCorVeiculo(cor);
  return (
    <span
      aria-hidden="true"
      className={cn('inline-block h-3 w-3 shrink-0 rounded-full border', className)}
      style={{
        background: r.fantasia ? `conic-gradient(${CORES_FANTASIA.join(',')},${CORES_FANTASIA[0]})` : r.hex,
        borderColor: contornoDaCor(r.hex),
      }}
    />
  );
}
