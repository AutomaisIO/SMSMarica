/**
 * Técnico regulador das Notificações do SER/SERNIT: quem incluiu a solicitação no sistema
 * externo (usuário do primeiro "Solicitar" da trilha). A chave vem do servidor já normalizada —
 * nome em maiúsculas — então a mesma pessoa tem a mesma chave nas duas telas, e a mesma cor.
 */

/** Técnico do filtro com as notificações pendentes de solicitações que ele incluiu. */
export type TecnicoNotificacao = {
  chave: string;
  pendentes: number;
};

/** Chave do grupo "sem técnico identificado" (histórico ainda não lido). Espelha o servidor. */
export const SEM_TECNICO = '__SEM_TECNICO__';

export function rotuloTecnico(chave: string): string {
  return chave === SEM_TECNICO ? 'Sem técnico identificado' : chave;
}

/** Partículas que não viram inicial: "MARIA DA SILVA" é MS, não MD. */
const PARTICULAS = new Set(['DA', 'DE', 'DO', 'DAS', 'DOS', 'E', 'D']);

export function iniciaisTecnico(chave: string): string {
  if (chave === SEM_TECNICO) return '?';
  const partes = chave
    .trim()
    .split(/\s+/)
    .filter((p) => p && !PARTICULAS.has(p.toUpperCase()));
  if (partes.length === 0) return '?';
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase();
  return (partes[0][0] + partes[partes.length - 1][0]).toUpperCase();
}

/**
 * Cor fixa por técnico, derivada do nome (não da posição na lista): a cor não muda quando um
 * técnico novo aparece, e é a mesma em qualquer máquina. O hash espalha o matiz pelo círculo
 * com o ângulo de ouro, e a luminosidade alterna em três faixas — com dezenas de técnicos, dois
 * matizes vizinhos ainda se distinguem pelo tom. Luminosidade baixa para o texto branco ler.
 */
export function corTecnico(chave: string): string {
  if (chave === SEM_TECNICO) return 'hsl(215 14% 55%)';
  let h = 2166136261;
  for (let i = 0; i < chave.length; i++) {
    h ^= chave.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  const n = h >>> 0;
  const matiz = Math.round((n * 137.508) % 360);
  const luz = [34, 42, 50][(n >>> 8) % 3];
  return `hsl(${matiz} 62% ${luz}%)`;
}
