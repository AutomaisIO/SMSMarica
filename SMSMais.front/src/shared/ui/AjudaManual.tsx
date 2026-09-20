import { Link, useLocation } from 'react-router-dom';
import { HelpCircle } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { artigoPorSlug } from '@/features/manual/registro';

type Props = {
  /** Slug do artigo no manual (ex.: `confirmacoes`). */
  artigo: string;
  /** Âncora de uma seção específica do artigo (ex.: `desfechos`). */
  secao?: string;
  className?: string;
};

/**
 * O "?" ao lado do título de uma tela: leva ao artigo do manual sobre ela, já sabendo voltar.
 *
 * Some sozinho quando o artigo ainda não existe — um "?" que leva a lugar nenhum ensina a pessoa a
 * não clicar no "?", e aí o manual inteiro morre. Enquanto a tela não estiver documentada, nada
 * aparece; no dia em que o artigo entra no registro, o ícone aparece sem mexer na tela.
 *
 * Para distinguir do <AjudaCampo />: aquele explica UM campo num modal; este abre a documentação da
 * TELA. Os dois podem conviver.
 */
export function AjudaManual({ artigo, secao, className }: Props) {
  const { pathname, search } = useLocation();
  if (!artigoPorSlug(artigo)) return null;

  const de = encodeURIComponent(`${pathname}${search}`);
  const destino = `/app/manual/${artigo}?de=${de}${secao ? `#${secao}` : ''}`;

  return (
    <Link
      to={destino}
      title="Como funciona esta tela (manual)"
      aria-label="Abrir o manual desta tela"
      className={cn(
        'inline-flex h-6 w-6 items-center justify-center rounded-full text-gray-400 transition-colors hover:bg-primary-50 hover:text-primary-700 focus:outline-none focus:ring-2 focus:ring-primary-500',
        className,
      )}
    >
      <HelpCircle className="h-4 w-4" />
    </Link>
  );
}
