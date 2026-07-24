/** Helper único de prefers-reduced-motion para animações feitas em JS. */
export function prefereMenosMovimento(): boolean {
  return (
    typeof window !== 'undefined' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches
  );
}
