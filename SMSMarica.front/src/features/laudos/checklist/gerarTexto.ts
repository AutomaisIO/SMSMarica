import type {
  CategoriaBiRads,
  ColunaTabela,
  EstruturaChecklist,
  ItemChecklist,
  RespostaItem,
  RespostasChecklist,
  SecaoChecklist,
} from './types';
import { condutaBiRads, rotuloBiRads, sugerirBiRads } from './birads';
import { dmoEfetivo, escalaDe, fraseDmo, sugerirDmo } from './densitometria';

/**
 * Classe que marca a tabela como "de dados" para o renderizador de PDF: grade
 * contínua, cabeçalho destacado/repetido e larguras de coluna. Tabela SEM a
 * classe (o cabeçalho institucional logo|texto|logo) segue no caminho de layout.
 * Espelha `LaudoPdfRenderer.ClasseTabelaDados` — mudou aqui, muda lá.
 */
export const CLASSE_TABELA_DADOS = 'laudo-tabela';

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

/**
 * Largura de referência da tabela, em px, para converter a largura em % (unidade
 * de autoria) para o `colwidth` do TipTap (unidade em px).
 */
const LARGURA_TABELA_PX = 700;

/**
 * Largura da coluna em DOIS formatos, de propósito:
 *
 * - `style="width:N%"` — lido pelo PDF e pelo CSS, mas o TipTap **descarta** no
 *   round-trip (style não é atributo do nó `tableHeader`);
 * - `colwidth="Npx"` — é atributo do nó, então sobrevive a abrir e salvar o
 *   laudo no editor. É também o que o TipTap grava quando a médica arrasta a
 *   borda da coluna.
 *
 * O renderizador de PDF prefere o style e cai no colwidth — o que dá o mesmo
 * resultado antes e depois de passar pelo editor.
 */
function atributosLargura(coluna: ColunaTabela): string {
  if (!coluna.larguraPct) return '';
  const px = Math.round((coluna.larguraPct / 100) * LARGURA_TABELA_PX);
  return ` colwidth="${px}" style="width: ${coluna.larguraPct}%"`;
}

/** Valor exibido numa célula: o preenchido (+ sufixo) ou um traço. */
function celulaTexto(coluna: ColunaTabela, valor: string | undefined): string {
  const v = (valor ?? '').trim();
  if (!v) return '—';
  return coluna.sufixo ? `${v} ${coluna.sufixo}` : v;
}

/**
 * Seção `tabela` → `<table class="laudo-tabela">`. As células levam `<p>` porque
 * é o que o TipTap espera dentro de td/th (conteúdo de bloco) — sem isso o
 * round-trip pelo editor reescreve a tabela.
 *
 * Linha sem NENHUM valor preenchido é omitida: um sítio não medido não deve
 * aparecer no laudo como uma fileira de traços.
 */
export function gerarHtmlTabela(secao: SecaoChecklist, respostas: RespostaItem[]): string {
  const colunas = secao.colunas ?? [];
  const linhas = secao.linhas ?? [];
  if (colunas.length === 0 || linhas.length === 0) return '';

  const preenchidas = linhas.filter((linha) => {
    const campos = respostas.find((r) => r.itemId === linha.id)?.campos ?? {};
    return colunas.some((c) => !c.fixa && (campos[c.chave] ?? '').trim() !== '');
  });
  if (preenchidas.length === 0) return '';

  const cabecalho = colunas.map((c) => `<th${atributosLargura(c)}><p>${escaparHtml(c.titulo)}</p></th>`).join('');

  const corpo = preenchidas
    .map((linha) => {
      const campos = respostas.find((r) => r.itemId === linha.id)?.campos ?? {};
      const celulas = colunas
        .map((c) => {
          const bruto = c.fixa ? linha.fixos[c.chave] : campos[c.chave];
          return `<td><p>${escaparHtml(celulaTexto(c, bruto))}</p></td>`;
        })
        .join('');
      return `<tr>${celulas}</tr>`;
    })
    .join('');

  return `<table class="${CLASSE_TABELA_DADOS}"><tbody><tr>${cabecalho}</tr>${corpo}</tbody></table>`;
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
  const dmo = dmoEfetivo(respostas);
  const blocos: string[] = [];

  for (const secao of estrutura.secoes) {
    const partes: string[] = [];

    if (secao.tipo === 'birads') {
      if (!final) continue;
      partes.push(`<h3>${escaparHtml(secao.titulo)}</h3>`);
      partes.push(`<p>Categoria ${escaparHtml(final)} (BI-RADS)</p>`);
      const conduta = condutaBiRads(final);
      if (conduta) partes.push(`<p>${escaparHtml(conduta)}</p>`);
    } else if (secao.tipo === 'tabela') {
      const tabela = gerarHtmlTabela(secao, marcados[secao.id] ?? []);
      if (!tabela) continue;
      partes.push(`<h3>${escaparHtml(secao.titulo)}</h3>`);
      partes.push(tabela);
    } else if (secao.tipo === 'texto-fixo') {
      const frases = secao.itens.filter((it) => it.texto.trim() !== '');
      if (frases.length === 0) continue;
      partes.push(`<h3>${escaparHtml(secao.titulo)}</h3>`);
      for (const item of frases) {
        partes.push(`<p>${escaparHtml(item.texto)}</p>`);
      }
    } else if (secao.tipo === 'oms-dmo') {
      if (!dmo) continue;
      partes.push(`<h3>${escaparHtml(secao.titulo)}</h3>`);
      partes.push('<p>Segundo os critérios da OMS e posições oficiais da SBDens / ISCD:</p>');
      partes.push(`<p>${escaparHtml(fraseDmo(dmo))}</p>`);

      // Rastreabilidade do cálculo: qual sítio e qual score decidiram.
      const escala = escalaDe(respostas);
      const sugerido = sugerirDmo(estrutura, marcados, escala);
      if (sugerido) {
        partes.push(
          `<p>Parâmetro: ${escaparHtml(escala)}-score de ${escaparHtml(
            sugerido.valor.toFixed(1).replace('.', ','),
          )} em ${escaparHtml(sugerido.sitio)}.</p>`,
        );
      }
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
