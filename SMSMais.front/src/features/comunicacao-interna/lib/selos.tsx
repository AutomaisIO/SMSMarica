import type { ReactNode } from 'react';
import {
  Ambulance,
  Ban,
  BedDouble,
  Check,
  CheckCheck,
  Coffee,
  Eye,
  HeartHandshake,
  Hourglass,
  Phone,
  Search,
  Siren,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/shared/lib/cn';

/**
 * Selos — as "figurinhas" corporativas da Comunicação interna (ADR-0079 §8). Viajam no texto como
 * `:selo:<chave>:` e o front desenha; assim continuam buscáveis e não exigem tabela. Chave nova
 * entra no FIM e nunca é renomeada: mensagem antiga com chave desconhecida aparece como texto cru.
 */
export type Selo = { chave: string; rotulo: string; icone: LucideIcon; cor: string };

export const SELOS: Selo[] = [
  { chave: 'recebido', rotulo: 'Recebido', icone: Check, cor: 'bg-emerald-50 text-emerald-700 ring-emerald-200' },
  { chave: 'ciente', rotulo: 'Ciente', icone: Eye, cor: 'bg-sky-50 text-sky-700 ring-sky-200' },
  { chave: 'em-analise', rotulo: 'Em análise', icone: Search, cor: 'bg-indigo-50 text-indigo-700 ring-indigo-200' },
  { chave: 'aguardando', rotulo: 'Aguardando', icone: Hourglass, cor: 'bg-amber-50 text-amber-800 ring-amber-200' },
  { chave: 'urgente', rotulo: 'Urgente', icone: Siren, cor: 'bg-red-600 text-white ring-red-700' },
  { chave: 'resolvido', rotulo: 'Resolvido', icone: CheckCheck, cor: 'bg-green-600 text-white ring-green-700' },
  { chave: 'obrigado', rotulo: 'Obrigado!', icone: HeartHandshake, cor: 'bg-pink-50 text-pink-700 ring-pink-200' },
  { chave: 'me-liga', rotulo: 'Me liga', icone: Phone, cor: 'bg-violet-50 text-violet-700 ring-violet-200' },
  { chave: 'paciente-a-caminho', rotulo: 'Paciente a caminho', icone: Ambulance, cor: 'bg-orange-50 text-orange-700 ring-orange-200' },
  { chave: 'vaga-confirmada', rotulo: 'Vaga confirmada', icone: BedDouble, cor: 'bg-teal-50 text-teal-700 ring-teal-200' },
  { chave: 'sem-vaga', rotulo: 'Sem vaga', icone: Ban, cor: 'bg-gray-100 text-gray-700 ring-gray-300' },
  { chave: 'bom-plantao', rotulo: 'Bom plantão!', icone: Coffee, cor: 'bg-yellow-50 text-yellow-800 ring-yellow-200' },
];

const POR_CHAVE = new Map(SELOS.map((s) => [s.chave, s]));

export function codigoSelo(chave: string): string {
  return `:selo:${chave}:`;
}

const RE_SELO = /:selo:([a-z0-9-]+):/g;

/** O texto é SÓ um selo (desenha grande, sem bolha de texto em volta)? */
export function seloUnico(texto: string | null): Selo | null {
  if (!texto) return null;
  const m = /^:selo:([a-z0-9-]+):$/.exec(texto.trim());
  return m ? (POR_CHAVE.get(m[1]) ?? null) : null;
}

/** Prévia legível (lista, notificação): troca o código pelo rótulo. */
export function textoLegivel(texto: string | null): string {
  if (!texto) return '';
  return texto.replace(RE_SELO, (cru, chave: string) => {
    const s = POR_CHAVE.get(chave);
    return s ? `[${s.rotulo}]` : cru;
  });
}

export function SeloVisual({ selo, grande = false, className }: { selo: Selo; grande?: boolean; className?: string }) {
  const Icone = selo.icone;
  return (
    <span
      className={cn(
        'inline-flex select-none items-center rounded-full font-semibold ring-1',
        grande ? 'gap-2 px-4 py-2 text-base shadow-sm' : 'mx-0.5 gap-1 px-2 py-0.5 align-middle text-xs',
        selo.cor,
        className,
      )}
    >
      <Icone className={grande ? 'h-5 w-5' : 'h-3.5 w-3.5'} />
      {selo.rotulo}
    </span>
  );
}

/** Texto com os selos desenhados inline (o resto vai como texto, preservando quebras de linha). */
export function TextoComSelos({ texto }: { texto: string }) {
  const partes: ReactNode[] = [];
  let ultimo = 0;
  for (const m of texto.matchAll(RE_SELO)) {
    const selo = POR_CHAVE.get(m[1]);
    if (!selo) continue;
    const ini = m.index ?? 0;
    if (ini > ultimo) partes.push(texto.slice(ultimo, ini));
    partes.push(<SeloVisual key={`${ini}-${selo.chave}`} selo={selo} />);
    ultimo = ini + m[0].length;
  }
  if (ultimo < texto.length) partes.push(texto.slice(ultimo));
  return <>{partes}</>;
}
