/**
 * Mostra um texto cortado em `max` caracteres, com "…" no fim quando estoura, e o texto
 * INTEIRO no tooltip (title) ao passar o mouse. Para descrições longas em listas.
 */
export function TextoLimitado({
  texto,
  max = 40,
  className,
}: {
  texto?: string | null;
  max?: number;
  className?: string;
}) {
  const t = (texto ?? '').trim();
  if (!t) return <span className={className}>—</span>;
  const estoura = t.length > max;
  const exibido = estoura ? `${t.slice(0, max).trimEnd()}…` : t;
  return (
    <span className={className} title={estoura ? t : undefined}>
      {exibido}
    </span>
  );
}
