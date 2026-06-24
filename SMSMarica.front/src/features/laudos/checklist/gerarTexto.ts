import type {
  CategoriaBiRads,
  EstruturaChecklist,
  ItemChecklist,
  RespostaItem,
  RespostasChecklist,
} from './types';
import { condutaBiRads, rotuloBiRads, sugerirBiRads } from './birads';

function escaparHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

/** Preenche `{chave}` com o valor do campo (+ sufixo); vazio vira `____`. */
export function preencherTexto(item: ItemChecklist, campos: Record<string, string>): string {
  return item.texto.replace(/\{(\w+)\}/g, (_, chave: string) => {
    const valor = (campos[chave] ?? '').trim();
    if (!valor) return '____';
    const def = item.campos?.find((c) => c.chave === chave);
    return def?.sufixo ? `${valor} ${def.sufixo}` : valor;
  });
}

/** Itens marcados (na ordem da estrutura) com seus campos. */
function itensMarcados(
  estrutura: EstruturaChecklist,
  marcados: Record<string, RespostaItem[]>,
  secaoId: string,
): Array<{ item: ItemChecklist; campos: Record<string, string> }> {
  const secao = estrutura.secoes.find((s) => s.id === secaoId);
  if (!secao) return [];
  const respostas = marcados[secaoId] ?? [];
  return secao.itens
    .filter((it) => respostas.some((r) => r.itemId === it.id))
    .map((item) => ({
      item,
      campos: respostas.find((r) => r.itemId === item.id)?.campos ?? {},
    }));
}

/** Coleta as contribuições BI-RADS de todos os itens marcados (para o cálculo). */
export function coletarContribuicoes(
  estrutura: EstruturaChecklist,
  marcados: Record<string, RespostaItem[]>,
): string[] {
  const contribs: string[] = [];
  for (const secao of estrutura.secoes) {
    for (const { item } of itensMarcados(estrutura, marcados, secao.id)) {
      if (item.birads) contribs.push(item.birads);
    }
  }
  return contribs;
}

/** BI-RADS efetivo do laudo: override da profissional, ou o sugerido pelo cálculo. */
export function biRadsEfetivo(respostas: RespostasChecklist): CategoriaBiRads | null {
  if (respostas.biRadsFinal) return respostas.biRadsFinal;
  return sugerirBiRads(coletarContribuicoes(respostas.estrutura, respostas.marcados));
}

/**
 * Gera o HTML do laudo a partir das respostas. Cada seção com itens marcados vira
 * um `<h3>` + parágrafos; a seção `tipo: 'birads'` (AVALIAÇÃO) imprime a categoria
 * efetiva e a conduta correspondente.
 */
export function gerarHtmlLaudo(respostas: RespostasChecklist): string {
  const { estrutura, marcados } = respostas;
  const final = biRadsEfetivo(respostas);
  const blocos: string[] = [];

  for (const secao of estrutura.secoes) {
    const partes: string[] = [];

    if (secao.tipo === 'birads') {
      if (!final) continue;
      partes.push(`<h3>${escaparHtml(secao.titulo)}</h3>`);
      partes.push(`<p>Categoria ${escaparHtml(final)} (BI-RADS)</p>`);
      const conduta = condutaBiRads(final);
      if (conduta) partes.push(`<p>${escaparHtml(conduta)}</p>`);
    } else {
      const itens = itensMarcados(estrutura, marcados, secao.id);
      if (itens.length === 0) continue;

      partes.push(`<h3>${escaparHtml(secao.titulo)}</h3>`);
      for (const { item, campos } of itens) {
        partes.push(`<p>${escaparHtml(preencherTexto(item, campos))}</p>`);
      }
    }

    if (partes.length > 0) blocos.push(partes.join('\n'));
  }

  // Linha vazia (parágrafo em branco) separando uma seção da outra.
  return blocos.join('\n<p></p>\n');
}

export { rotuloBiRads };
