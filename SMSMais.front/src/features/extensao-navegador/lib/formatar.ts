import type { Artefato, Canal, Dispositivo, Pacote } from '@/features/extensao-navegador/types';

export const ROTULO_ARTEFATO: Record<Artefato, string> = {
  Extensao: 'Extensão',
  Atualizador: 'Atualizador',
};

export const ROTULO_CANAL: Record<Canal, string> = {
  Teste: 'Teste',
  Prod: 'Produção',
};

/** O que o computador diz sobre a extensão no Chrome → rótulo e se pede atenção. */
export function situacaoDoChrome(valor: string | null): { rotulo: string; atencao: boolean } {
  switch (valor) {
    case 'carregada':
      return { rotulo: 'Carregada', atencao: false };
    case 'nao-carregada':
      return { rotulo: 'Chrome aberto sem a extensão', atencao: true };
    case 'desativada':
      return { rotulo: 'Desativada no Chrome', atencao: true };
    case 'modo-dev-desligado':
      return { rotulo: 'Modo desenvolvedor desligado', atencao: true };
    case 'fechado':
      return { rotulo: 'Chrome fechado', atencao: false };
    case 'sem-perfil':
      return { rotulo: 'Sem Chrome neste usuário', atencao: false };
    default:
      return { rotulo: '—', atencao: false };
  }
}

/** Compara versões "1.2.3" número a número (parte ausente vale zero) — igual ao backend e ao atualizador. */
export function compararVersoes(a: string, b: string): number {
  const x = a.split('.').map((p) => Number.parseInt(p, 10) || 0);
  const y = b.split('.').map((p) => Number.parseInt(p, 10) || 0);
  for (let i = 0; i < Math.max(x.length, y.length); i++) {
    const diferenca = (x[i] ?? 0) - (y[i] ?? 0);
    if (diferenca !== 0) return diferenca;
  }
  return 0;
}

/** A versão que um canal recebe agora, de um artefato (nulo = nada publicado para ele). */
export function versaoEmVigor(pacotes: Pacote[], artefato: Artefato, canal: Canal): string | null {
  const emVigor = pacotes.find(
    (p) => p.artefato === artefato && (canal === 'Teste' ? p.atualEmTeste : p.atualEmProd),
  );
  return emVigor?.versao ?? null;
}

/** O computador falou com a plataforma nos últimos 30 minutos? (ele consulta de 10 em 10) */
export function emContato(dispositivo: Dispositivo, agora: number = Date.now()): boolean {
  if (!dispositivo.ultimoContatoEm) return false;
  return agora - new Date(dispositivo.ultimoContatoEm).getTime() < 30 * 60_000;
}

export function formatarTamanho(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return '—';
  const kb = bytes / 1024;
  return kb < 1024 ? `${kb.toFixed(0)} KB` : `${(kb / 1024).toFixed(1)} MB`;
}
