import { CodigoCopiavel } from '@/shared/ui/CodigoCopiavel';

/** Formata um telefone brasileiro para exibição: (DD) 9XXXX-XXXX / (DD) XXXX-XXXX. */
export function formatarTelefoneBR(valor: string): string {
  let n = (valor ?? '').replace(/\D/g, '');
  // Descarta o código do país (55) quando presente.
  if ((n.length === 12 || n.length === 13) && n.startsWith('55')) n = n.slice(2);
  if (n.length === 11) return `(${n.slice(0, 2)}) ${n.slice(2, 7)}-${n.slice(7)}`;
  if (n.length === 10) return `(${n.slice(0, 2)}) ${n.slice(2, 6)}-${n.slice(6)}`;
  return valor; // formato desconhecido: mostra como veio
}

/**
 * Telefone exibido como texto (fora de input) com botão de copiar — copia SÓ os
 * dígitos (sem traços/parênteses). Mostra "—" quando vazio. Use em qualquer lugar
 * que hoje só imprime o número.
 */
export function TelefoneCopiavel({
  numero,
  className,
}: {
  numero?: string | null;
  className?: string;
}) {
  const bruto = (numero ?? '').trim();
  if (!bruto) return <span className="text-gray-400">—</span>;
  const digitos = bruto.replace(/\D/g, '');
  return (
    <span className={className}>
      <CodigoCopiavel
        codigo={formatarTelefoneBR(bruto)}
        valorCopiar={digitos}
        dica="Copiar telefone (só números)"
      />
    </span>
  );
}
