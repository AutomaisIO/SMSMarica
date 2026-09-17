import { corTecnico, iniciaisTecnico, rotuloTecnico } from '@/shared/regulacao/tecnicos';

type Props = {
  chave: string;
  tamanho?: 'xs' | 'sm';
  className?: string;
};

const TAMANHOS = {
  xs: 'size-5 text-[9px]',
  sm: 'size-7 text-[11px]',
};

/** Bolinha com as iniciais e a cor do técnico regulador — é o que o técnico procura rolando a
 * fila para achar o que é dele. O nome inteiro fica no title. */
export function AvatarTecnico({ chave, tamanho = 'sm', className = '' }: Props) {
  return (
    <span
      title={`Técnico regulador: ${rotuloTecnico(chave)}`}
      style={{ backgroundColor: corTecnico(chave) }}
      className={`inline-flex shrink-0 select-none items-center justify-center rounded-full font-semibold text-white ${TAMANHOS[tamanho]} ${className}`}
    >
      {iniciaisTecnico(chave)}
    </span>
  );
}
