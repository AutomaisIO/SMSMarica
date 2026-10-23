/** Tamanho legível em pt-BR (vírgula decimal). */
export function tamanhoFmt(bytes: number | null | undefined): string {
  if (bytes == null) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1).replace('.', ',')} MB`;
}

/** mm:ss (duração de áudio, cronômetro da gravação). */
export function duracaoFmt(segundos: number | null | undefined): string {
  const s = Math.max(0, Math.round(segundos ?? 0));
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
}

export function horaFmt(iso: string): string {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

function mesmoDia(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

/** Rótulo do separador de dia na thread: Hoje / Ontem / 21 de outubro de 2026. */
export function diaFmt(iso: string): string {
  const d = new Date(iso);
  const hoje = new Date();
  const ontem = new Date(hoje);
  ontem.setDate(hoje.getDate() - 1);
  if (mesmoDia(d, hoje)) return 'Hoje';
  if (mesmoDia(d, ontem)) return 'Ontem';
  return d.toLocaleDateString('pt-BR', { day: 'numeric', month: 'long', year: 'numeric' });
}

/** Chave do dia (agrupar mensagens). */
export function chaveDia(iso: string): string {
  const d = new Date(iso);
  return `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`;
}

/** Hora na lista: hoje mostra a hora; antes, a data curta. */
export function quandoFmt(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (mesmoDia(d, new Date())) return horaFmt(iso);
  return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
}

/** Iniciais para o avatar ("Ana Paula Souza" → "AS"). */
export function iniciais(nome: string): string {
  const partes = nome.trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return '?';
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase();
  return (partes[0][0] + partes[partes.length - 1][0]).toUpperCase();
}

const CORES_AVATAR = [
  'bg-orange-100 text-orange-700',
  'bg-sky-100 text-sky-700',
  'bg-emerald-100 text-emerald-700',
  'bg-violet-100 text-violet-700',
  'bg-rose-100 text-rose-700',
  'bg-amber-100 text-amber-800',
  'bg-teal-100 text-teal-700',
  'bg-indigo-100 text-indigo-700',
];

/** Cor estável por id (a mesma pessoa tem sempre a mesma cor). */
export function corAvatar(id: string): string {
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) | 0;
  return CORES_AVATAR[Math.abs(h) % CORES_AVATAR.length];
}

/**
 * Tipos que a bolha desenha INLINE. Tudo o mais é "arquivo" (só Baixar): um HTML/SVG aberto como
 * blob herdaria a origem do painel e rodaria script com o token. A decisão é pelo `anexo.mime` do
 * DTO — nunca pelo tipo do blob baixado.
 */
const IMAGENS_INLINE = new Set(['image/png', 'image/jpeg', 'image/gif', 'image/webp']);

export type FormaAnexo = 'imagem' | 'audio' | 'pdf' | 'arquivo';

export function formaDoAnexo(mime: string, ehAudio: boolean): FormaAnexo {
  const m = mime.split(';')[0].trim().toLowerCase();
  if (ehAudio || m.startsWith('audio/')) return 'audio';
  if (IMAGENS_INLINE.has(m)) return 'imagem';
  if (m === 'application/pdf') return 'pdf';
  return 'arquivo';
}
