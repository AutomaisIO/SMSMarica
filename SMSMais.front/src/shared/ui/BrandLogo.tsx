import { cn } from '@/shared/lib/cn';
import { instituicao, urlLogo } from '@/shared/tema/instituicao';

type Props = {
  className?: string;
  alt?: string;
  compact?: boolean;
};

/**
 * Logotipo da instituição desta instância (ADR-0043).
 *
 * A imagem vem do que foi cadastrado em Sistema → Instituição. Sem logo cadastrado, o
 * fallback é NEUTRO (a inicial do nome curto) — nunca a marca de outro município, nem por
 * um frame (ADR-0046).
 */
export function BrandLogo({ className, alt, compact = false }: Props) {
  const inst = instituicao();
  const rotulo = alt ?? inst.nome;

  if (compact) {
    // Inicial do nome curto, não um "M" fixo: em outra prefeitura o M seria da cidade errada.
    const inicial = (inst.nomeCurto || inst.nome || '?').trim().charAt(0).toUpperCase();
    return (
      <div
        className={cn(
          'flex h-10 w-10 items-center justify-center rounded-md bg-white text-primary-700 font-bold shadow-marca',
          className,
        )}
        title={rotulo}
      >
        {inicial}
      </div>
    );
  }

  const src = urlLogo();
  if (!src) {
    // Sem logo no banco: bloco neutro com a inicial — mesmo desenho do modo compacto.
    const inicial = (inst.nomeCurto || inst.nome || '?').trim().charAt(0).toUpperCase();
    return (
      <div
        className={cn(
          'flex h-10 min-w-10 items-center justify-center rounded-md bg-white px-2 text-primary-700 font-bold shadow-marca',
          className,
        )}
        title={rotulo}
      >
        {inicial}
      </div>
    );
  }

  return <img src={src} alt={rotulo} className={cn('object-contain', className)} />;
}
