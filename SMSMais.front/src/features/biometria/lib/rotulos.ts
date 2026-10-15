import { formatarInstante } from '@/shared/lib/datas';
import type {
  MeioVerificacaoMotorista,
  MomentoVerificacaoFacial,
  VerificacaoFacial,
} from '@/features/biometria/types';

export const MOMENTOS_VERIFICACAO: Record<MomentoVerificacaoFacial, string> = {
  Login: 'Entrada',
  InicioTranslado: 'Antes do translado',
  PrimeiroAcesso: 'Primeiro acesso',
};

export const MEIOS_VERIFICACAO: Record<MeioVerificacaoMotorista, string> = {
  Rosto: 'Rosto',
  CodigoCentral: 'Código da central',
};

export function rotuloMomento(momento: string): string {
  return MOMENTOS_VERIFICACAO[momento as MomentoVerificacaoFacial] ?? momento;
}

export function rotuloMeio(meio: string): string {
  return MEIOS_VERIFICACAO[meio as MeioVerificacaoMotorista] ?? meio;
}

/** Nota de semelhança com 2 casas, no formato brasileiro (0,94). */
export function formatarNota(valor: number | null): string {
  if (valor == null) return '—';
  return valor.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/** O servidor manda o JPEG em base64 puro; a tag <img> precisa do prefixo. */
export function urlDaFoto(base64: string): string {
  return base64.startsWith('data:') ? base64 : `data:image/jpeg;base64,${base64}`;
}

/** Link do mapa (OpenStreetMap) na posição da conferência — o mesmo das Ocorrências. */
export function linkMapaConferencia(v: VerificacaoFacial): string | null {
  if (v.latitude == null || v.longitude == null) return null;
  return `https://www.openstreetmap.org/?mlat=${v.latitude}&mlon=${v.longitude}#map=17/${v.latitude}/${v.longitude}`;
}

/** "15/10/2026, 08:42" em Brasília. */
export function dataHoraConferencia(iso: string): string {
  return formatarInstante(iso, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
