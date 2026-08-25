import type {
  ClassificacaoDmo,
  ColunaTabela,
  EscalaDmo,
  EstruturaChecklist,
  RespostaItem,
  RespostasChecklist,
} from './types';

/**
 * Classificação da densidade mineral óssea segundo os critérios da OMS e as
 * posições oficiais da SBDens / ISCD.
 *
 * Regra-mãe: o parâmetro é o **menor valor** entre os sítios elegíveis (coluna
 * lombar e fêmur — colo ou total). O rádio 33% só entra quando marcado como
 * diagnóstico na estrutura, porque só vale em condições específicas.
 *
 * A escala depende da paciente:
 * - **T-score** (perimenopausa em diante, homens ≥ 50): ≥ −1,0 normal;
 *   entre −1,0 e −2,5 osteopenia; ≤ −2,5 osteoporose.
 * - **Z-score** (crianças, pré-menopausa, homens < 50): ≤ −2,0 é "abaixo da
 *   faixa esperada para a idade" — nesta escala **não se usam** os rótulos
 *   osteopenia/osteoporose.
 *
 * O resultado é sempre uma SUGESTÃO; a profissional pode sobrescrever.
 */

/** Aceita "−2,5", "-2.5", " -2,5 " → -2.5. Retorna null se não for número. */
export function lerNumero(valor?: string | null): number | null {
  if (valor === null || valor === undefined) return null;
  const limpo = valor
    .trim()
    .replace(/−/g, '-') // sinal de menos unicode
    .replace(',', '.');
  if (!limpo) return null;
  const n = Number(limpo);
  return Number.isFinite(n) ? n : null;
}

/** Colunas que carregam o papel pedido (T-score ou Z-score). */
function colunasDoPapel(colunas: ColunaTabela[] | undefined, escala: EscalaDmo): ColunaTabela[] {
  const papel = escala === 'Z' ? 'zscore' : 'tscore';
  return (colunas ?? []).filter((c) => c.papel === papel);
}

/**
 * Menor score entre os sítios elegíveis. Retorna null quando nada foi preenchido
 * (ou quando nenhuma linha diagnóstica tem valor).
 */
export function menorScore(
  estrutura: EstruturaChecklist,
  marcados: Record<string, RespostaItem[]>,
  escala: EscalaDmo,
): { valor: number; sitio: string } | null {
  let melhor: { valor: number; sitio: string } | null = null;

  for (const secao of estrutura.secoes) {
    if (secao.tipo !== 'tabela') continue;
    const cols = colunasDoPapel(secao.colunas, escala);
    if (cols.length === 0) continue;

    const respostas = marcados[secao.id] ?? [];
    for (const linha of secao.linhas ?? []) {
      if (!linha.diagnostica) continue;
      const campos = respostas.find((r) => r.itemId === linha.id)?.campos ?? {};
      for (const col of cols) {
        const n = lerNumero(campos[col.chave]);
        if (n === null) continue;
        if (melhor === null || n < melhor.valor) {
          melhor = { valor: n, sitio: rotuloLinha(secao.colunas, linha.fixos, campos) };
        }
      }
    }
  }

  return melhor;
}

/**
 * Rótulo legível da linha ("Fêmur Proximal · Colo Femoral"). `override` traz os
 * valores digitados no laudo (ex.: sítio livre da coluna lombar) — quando há um
 * override não-vazio para uma coluna fixa, ele prevalece sobre o rótulo-padrão.
 */
export function rotuloLinha(
  colunas: ColunaTabela[] | undefined,
  fixos: Record<string, string>,
  override?: Record<string, string>,
): string {
  return (colunas ?? [])
    .filter((c) => c.fixa)
    .map((c) => ((override?.[c.chave] ?? '').trim() || (fixos[c.chave] ?? '').trim()))
    .filter(Boolean)
    .join(' · ');
}

/** Classifica um score na escala informada. */
export function classificar(valor: number, escala: EscalaDmo): ClassificacaoDmo {
  if (escala === 'Z') return valor <= -2.0 ? 'ABAIXO_ESPERADO' : 'DENTRO_ESPERADO';
  if (valor <= -2.5) return 'OSTEOPOROSE';
  if (valor < -1.0) return 'OSTEOPENIA';
  return 'NORMAL';
}

/** Sugestão de diagnóstico a partir da tabela preenchida. null = sem dados. */
export function sugerirDmo(
  estrutura: EstruturaChecklist,
  marcados: Record<string, RespostaItem[]>,
  escala: EscalaDmo,
): { classificacao: ClassificacaoDmo; valor: number; sitio: string } | null {
  const menor = menorScore(estrutura, marcados, escala);
  if (!menor) return null;
  return { classificacao: classificar(menor.valor, escala), valor: menor.valor, sitio: menor.sitio };
}

export const CLASSIFICACOES_DMO: ClassificacaoDmo[] = [
  'NORMAL',
  'OSTEOPENIA',
  'OSTEOPOROSE',
  'DENTRO_ESPERADO',
  'ABAIXO_ESPERADO',
];

/** Classificações válidas na escala — a lista do seletor muda com T/Z. */
export function classificacoesDaEscala(escala: EscalaDmo): ClassificacaoDmo[] {
  return escala === 'Z'
    ? ['DENTRO_ESPERADO', 'ABAIXO_ESPERADO']
    : ['NORMAL', 'OSTEOPENIA', 'OSTEOPOROSE'];
}

const ROTULOS: Record<ClassificacaoDmo, string> = {
  NORMAL: 'Densidade mineral óssea normal',
  OSTEOPENIA: 'Baixa densidade mineral óssea — osteopenia',
  OSTEOPOROSE: 'Osteoporose',
  DENTRO_ESPERADO: 'Dentro da faixa esperada para a idade',
  ABAIXO_ESPERADO: 'Abaixo da faixa esperada para a idade',
};

export function rotuloDmo(c?: string | null): string {
  return c && c in ROTULOS ? ROTULOS[c as ClassificacaoDmo] : '';
}

/** Frase que vai para o laudo, no formato do modelo em uso. */
export function fraseDmo(c?: string | null): string {
  switch (c) {
    case 'NORMAL':
      return 'O(A) paciente apresenta DENSIDADE MINERAL ÓSSEA (DMO) NORMAL.';
    case 'OSTEOPENIA':
      return 'O(A) paciente apresenta BAIXA DENSIDADE MINERAL ÓSSEA - OSTEOPENIA.';
    case 'OSTEOPOROSE':
      return 'O(A) paciente apresenta OSTEOPOROSE.';
    case 'DENTRO_ESPERADO':
      return 'O(A) paciente apresenta DENSIDADE MINERAL ÓSSEA DENTRO DA FAIXA ESPERADA PARA A IDADE.';
    case 'ABAIXO_ESPERADO':
      return 'O(A) paciente apresenta DENSIDADE MINERAL ÓSSEA ABAIXO DA FAIXA ESPERADA PARA A IDADE.';
    default:
      return '';
  }
}

/** Cor de destaque por gravidade (badges na UI). */
export function corDmo(c?: string | null): string {
  switch (c) {
    case 'NORMAL':
    case 'DENTRO_ESPERADO':
      return 'bg-emerald-50 text-emerald-800 border-emerald-200';
    case 'OSTEOPENIA':
      return 'bg-amber-50 text-amber-800 border-amber-200';
    case 'OSTEOPOROSE':
    case 'ABAIXO_ESPERADO':
      return 'bg-red-50 text-red-800 border-red-200';
    default:
      return 'bg-gray-100 text-gray-600 border-gray-200';
  }
}

/** Escala efetiva do laudo (T é o padrão quando o blob é antigo/omisso). */
export function escalaDe(respostas: RespostasChecklist): EscalaDmo {
  return respostas.escalaDmo ?? 'T';
}

/** Diagnóstico efetivo: override da profissional, ou o sugerido pelo cálculo. */
export function dmoEfetivo(respostas: RespostasChecklist): ClassificacaoDmo | null {
  if (respostas.dmoFinal) return respostas.dmoFinal;
  const s = sugerirDmo(respostas.estrutura, respostas.marcados, escalaDe(respostas));
  return s?.classificacao ?? null;
}
