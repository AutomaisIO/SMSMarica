import ExcelJS from 'exceljs';
import { renderizarPizza, type FatiaPizza } from '@/features/indicadores/lib/graficoPizza';
import type { AbaIndicador, IndicadorResumo, MetaOperador } from '@/features/indicadores/types';

/** Uma aba (submenu) com seus indicadores já apurados para o período. */
export type AbaExportacao = {
  aba: AbaIndicador;
  rotulo: string;
  itens: IndicadorResumo[];
};

export type DadosExportacao = {
  abas: AbaExportacao[];
  unidadeNome: string;
  inicio: string; // yyyy-MM-dd
  fim: string; // yyyy-MM-dd
};

// Paleta da marca (ARGB, exigido pelo exceljs).
const VERMELHO = 'FFC8102E';
const VERMELHO_ESCURO = 'FFA80C27';
const BRANCO = 'FFFFFFFF';
const CINZA_TEXTO = 'FF52525B';
const CINZA_SUAVE = 'FFF4F4F5';
const CINZA_BORDA = 'FFE4E4E7';
const VERDE = 'FF16A34A';

// Cores CSS para as pizzas (canvas).
const COR_ATINGIDO = '#16A34A';
const COR_NAO_ATINGIDO = '#C8102E';
const CORES_ABA = ['#C8102E', '#E03C52', '#A80C27', '#EE6B7B', '#6C0819', '#F2A0AB'];

const COLS_ABA = ['Nº', 'Indicador', 'Meta', 'Peso', 'Numerador', 'Denominador', 'Resultado', 'Pontuação'];

type AgregadoAba = {
  aba: AbaExportacao;
  nomePlanilha: string;
  linhaTotal: number; // linha do total dentro da planilha da aba
  pontosPossiveis: number;
  pontosAlcancados: number;
  avaliaveis: number;
  atingidos: number;
};

function ehAgrupador(i: IndicadorResumo, filhosPorPai: Map<string, IndicadorResumo[]>): boolean {
  return i.tipoResultado === 'Agrupador' || (filhosPorPai.get(i.id)?.length ?? 0) > 0;
}

function mapaFilhos(itens: IndicadorResumo[]): Map<string, IndicadorResumo[]> {
  const m = new Map<string, IndicadorResumo[]>();
  for (const i of itens) {
    if (!i.indicadorPaiId) continue;
    const atual = m.get(i.indicadorPaiId) ?? [];
    atual.push(i);
    m.set(i.indicadorPaiId, atual);
  }
  return m;
}

/** Espelha `pesoDe`/`pontosDe` da TabelaIndicadores para os cartões do resumo e as pizzas. */
function agregar(itens: IndicadorResumo[]): {
  pontosPossiveis: number;
  pontosAlcancados: number;
  avaliaveis: number;
  atingidos: number;
} {
  const filhos = mapaFilhos(itens);
  const topo = itens.filter((i) => !i.indicadorPaiId);

  const pesoDe = (i: IndicadorResumo) => {
    const fs = filhos.get(i.id);
    if (fs?.length) return fs.reduce((s, f) => s + (f.pontuacao ?? 0), 0);
    return i.pontuacao ?? 0;
  };
  const pontosDe = (i: IndicadorResumo) => {
    const fs = filhos.get(i.id);
    if (fs?.length) return fs.reduce((s, f) => s + (f.resultado?.pontuacaoApurada ?? 0), 0);
    return i.resultado?.pontuacaoApurada ?? 0;
  };

  const folhas = itens.filter((i) => !ehAgrupador(i, filhos));
  return {
    pontosPossiveis: topo.reduce((s, i) => s + pesoDe(i), 0),
    pontosAlcancados: topo.reduce((s, i) => s + pontosDe(i), 0),
    avaliaveis: folhas.filter((f) => f.metaOperador != null).length,
    atingidos: folhas.filter((f) => (f.resultado?.pontuacaoApurada ?? 0) > 0).length,
  };
}

/** Nome de planilha válido no Excel: sem `\ / ? * [ ] :` e no máximo 31 caracteres. */
function nomePlanilha(rotulo: string, usados: Set<string>): string {
  let base = rotulo.replace(/[\\/?*[\]:]/g, ' ').trim().slice(0, 31) || 'Aba';
  let nome = base;
  let n = 2;
  while (usados.has(nome.toLowerCase())) {
    base = base.slice(0, 28);
    nome = `${base} ${n++}`;
  }
  usados.add(nome.toLowerCase());
  return nome;
}

function formatarData(iso: string): string {
  const [a, m, d] = iso.split('-');
  return d && m && a ? `${d}/${m}/${a}` : iso;
}

/** Condição de meta (tudo-ou-nada) como expressão de fórmula referenciando a célula do resultado. */
function condicaoMeta(
  op: MetaOperador,
  ref: string,
  valor: number,
  valorMax: number | null,
): string {
  const v = String(valor);
  switch (op) {
    case 'MenorOuIgual':
      return `${ref}<=${v}`;
    case 'MaiorOuIgual':
      return `${ref}>=${v}`;
    case 'Menor':
      return `${ref}<${v}`;
    case 'Maior':
      return `${ref}>${v}`;
    case 'Igual':
      return `${ref}=${v}`;
    case 'Entre':
      return `AND(${ref}>=${v},${ref}<=${String(valorMax ?? valor)})`;
    default:
      return 'FALSE';
  }
}

function borda(): Partial<ExcelJS.Borders> {
  const linha: Partial<ExcelJS.Border> = { style: 'thin', color: { argb: CINZA_BORDA } };
  return { top: linha, bottom: linha, left: linha, right: linha };
}

async function carregarLogoBase64(): Promise<string | null> {
  try {
    const resp = await fetch('/marica_logo.png');
    if (!resp.ok) return null;
    const buf = new Uint8Array(await resp.arrayBuffer());
    let bin = '';
    for (const b of buf) bin += String.fromCharCode(b);
    return btoa(bin);
  } catch {
    return null;
  }
}

function cabecalho(
  ws: ExcelJS.Worksheet,
  logoId: number | undefined,
  ultimaCol: string,
  titulo: string,
  subtitulo: string,
): void {
  ws.getRow(1).height = 34;
  if (logoId !== undefined) {
    ws.addImage(logoId, { tl: { col: 0.15, row: 0.15 }, ext: { width: 168, height: 44 } });
  }
  ws.mergeCells(`A3:${ultimaCol}3`);
  const t = ws.getCell('A3');
  t.value = titulo;
  t.font = { bold: true, size: 14, color: { argb: VERMELHO } };
  ws.mergeCells(`A4:${ultimaCol}4`);
  const s = ws.getCell('A4');
  s.value = subtitulo;
  s.font = { size: 10, color: { argb: CINZA_TEXTO } };
}

function estiloCabecalhoTabela(ws: ExcelJS.Worksheet, linha: number, colunas: string[]): void {
  const row = ws.getRow(linha);
  colunas.forEach((titulo, idx) => {
    const c = row.getCell(idx + 1);
    c.value = titulo;
    c.font = { bold: true, color: { argb: BRANCO }, size: 11 };
    c.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: VERMELHO } };
    c.alignment = { vertical: 'middle', horizontal: idx <= 1 ? 'left' : idx === 2 ? 'left' : 'right' };
    c.border = borda();
  });
  row.height = 22;
}

/** Monta a planilha de uma aba com as fórmulas vivas. Devolve a linha do total. */
function montarPlanilhaAba(ws: ExcelJS.Worksheet, logoId: number | undefined, dados: DadosExportacao, aba: AbaExportacao): number {
  const larguras = [7, 50, 22, 8, 14, 14, 15, 12];
  larguras.forEach((w, i) => (ws.getColumn(i + 1).width = w));

  cabecalho(
    ws,
    logoId,
    'H',
    `Indicadores contratuais · HMCML — ${aba.rotulo}`,
    `Unidade: ${dados.unidadeNome}   ·   Período: ${formatarData(dados.inicio)} a ${formatarData(dados.fim)}`,
  );

  const linhaHeader = 6;
  estiloCabecalhoTabela(ws, linhaHeader, COLS_ABA);
  ws.views = [{ state: 'frozen', ySplit: linhaHeader }];

  const itens = aba.itens;
  const filhos = mapaFilhos(itens);
  const linhaInicial = linhaHeader + 1;
  const linhaPorId = new Map<string, number>();
  itens.forEach((it, idx) => linhaPorId.set(it.id, linhaInicial + idx));

  const percentDe = (i: IndicadorResumo) => i.unidadeMedida === '%';

  itens.forEach((it, idx) => {
    const r = linhaInicial + idx;
    const row = ws.getRow(r);
    const agr = ehAgrupador(it, filhos);
    const res = it.resultado;
    const filhosDoItem = filhos.get(it.id) ?? [];

    // A · Nº
    row.getCell(1).value = it.numero;
    // B · Indicador (filhos indentados)
    const nome = row.getCell(2);
    nome.value = it.nome;
    if (it.indicadorPaiId) nome.alignment = { indent: 2 };

    // C · Meta (texto)
    row.getCell(3).value = it.meta ?? '';

    // D · Peso
    const peso = row.getCell(4);
    if (agr && filhosDoItem.length) {
      peso.value = { formula: `SUM(${filhosDoItem.map((f) => `D${linhaPorId.get(f.id)}`).join(',')})` };
    } else if (!agr) {
      peso.value = it.pontuacao ?? null;
    }
    peso.numFmt = '#,##0.##';

    // E/F · Numerador / Denominador (valores declarados)
    const num = row.getCell(5);
    const den = row.getCell(6);
    if (!agr) {
      if (it.tipoResultado === 'Media') {
        // Média: valor pronto no SQL; denominador = amostra.
        den.value = res?.denominador ?? null;
      } else {
        num.value = res?.numerador ?? null;
        if (it.tipoResultado !== 'Absoluto') den.value = res?.denominador ?? null;
      }
    }
    num.numFmt = '#,##0.##';
    den.numFmt = '#,##0.##';

    // G · Resultado (fórmula sempre que derivável de num/den)
    const resultado = row.getCell(7);
    if (!agr) {
      switch (it.tipoResultado) {
        case 'Razao':
          resultado.value = { formula: `IF(F${r}=0,"",E${r}/F${r})` };
          break;
        case 'Densidade':
          resultado.value = { formula: `IF(F${r}=0,"",E${r}/F${r}*${it.fatorDensidade ?? 1000})` };
          break;
        case 'Absoluto':
          resultado.value = { formula: `IF(E${r}="","",E${r})` };
          break;
        case 'Media':
          resultado.value = res?.valor ?? null;
          break;
        default:
          // Distribuicao: não há num÷den; deixa vazio (o detalhe fica na tela).
          break;
      }
    }
    resultado.numFmt = percentDe(it) ? '0.0%' : '#,##0.00';
    resultado.font = { bold: true };

    // H · Pontuação (fórmula tudo-ou-nada / soma dos filhos)
    const pont = row.getCell(8);
    if (agr && filhosDoItem.length) {
      pont.value = { formula: `SUM(${filhosDoItem.map((f) => `H${linhaPorId.get(f.id)}`).join(',')})` };
    } else if (!agr && it.metaOperador && it.metaValor != null) {
      const cond = condicaoMeta(it.metaOperador, `G${r}`, it.metaValor, it.metaValorMaximo);
      pont.value = { formula: `IF(G${r}="","",IF(${cond},D${r},0))` };
    }
    pont.numFmt = '#,##0.##';

    // Estilo da linha
    for (let c = 1; c <= 8; c++) {
      const cell = row.getCell(c);
      cell.border = borda();
      if (c >= 4) cell.alignment = { ...cell.alignment, horizontal: 'right' };
      if (c === 1) cell.alignment = { ...cell.alignment, horizontal: 'center' };
    }
    if (agr) {
      row.font = { bold: true };
      for (let c = 1; c <= 8; c++) {
        row.getCell(c).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: CINZA_SUAVE } };
      }
    }
  });

  // Linha de total (soma só o topo — não conta agrupador + filhos duas vezes)
  const topo = itens.filter((i) => !i.indicadorPaiId);
  const linhaTotal = linhaInicial + itens.length;
  const total = ws.getRow(linhaTotal);
  total.getCell(2).value = 'Total de pontos';
  const refsTopoD = topo.map((i) => `D${linhaPorId.get(i.id)}`).join(',');
  const refsTopoH = topo.map((i) => `H${linhaPorId.get(i.id)}`).join(',');
  if (topo.length) {
    total.getCell(4).value = { formula: `SUM(${refsTopoD})` };
    total.getCell(8).value = { formula: `SUM(${refsTopoH})` };
  }
  total.getCell(4).numFmt = '#,##0.##';
  total.getCell(8).numFmt = '#,##0.##';
  for (let c = 1; c <= 8; c++) {
    const cell = total.getCell(c);
    cell.font = { bold: true, color: { argb: VERMELHO_ESCURO } };
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: CINZA_SUAVE } };
    cell.border = { ...borda(), top: { style: 'medium', color: { argb: VERMELHO } } };
    if (c >= 4) cell.alignment = { horizontal: 'right' };
  }

  return linhaTotal;
}

/** Monta a planilha "Resumo Geral" com os cartões, a tabela por aba e as pizzas. */
function montarResumo(
  ws: ExcelJS.Worksheet,
  wb: ExcelJS.Workbook,
  logoId: number | undefined,
  dados: DadosExportacao,
  agregados: AgregadoAba[],
): void {
  [10, 26, 26, 14, 20, 18].forEach((w, i) => (ws.getColumn(i + 1).width = w));
  cabecalho(
    ws,
    logoId,
    'F',
    'Resumo Geral · Indicadores contratuais HMCML',
    `Unidade: ${dados.unidadeNome}   ·   Período: ${formatarData(dados.inicio)} a ${formatarData(dados.fim)}`,
  );

  const cols = ['Aba', 'Pontos possíveis', 'Pontos alcançados', '% alcançado', 'Indicadores atingidos', 'Avaliáveis'];
  const linhaHeader = 6;
  estiloCabecalhoTabela(ws, linhaHeader, cols);

  const linhaInicial = linhaHeader + 1;
  agregados.forEach((ag, idx) => {
    const r = linhaInicial + idx;
    const row = ws.getRow(r);
    const nome = ag.nomePlanilha.replace(/'/g, "''");
    row.getCell(1).value = ag.aba.rotulo;
    row.getCell(2).value = { formula: `'${nome}'!D${ag.linhaTotal}` };
    row.getCell(3).value = { formula: `'${nome}'!H${ag.linhaTotal}` };
    row.getCell(4).value = { formula: `IF(B${r}=0,"",C${r}/B${r})` };
    row.getCell(5).value = ag.atingidos;
    row.getCell(6).value = ag.avaliaveis;
    row.getCell(2).numFmt = '#,##0.##';
    row.getCell(3).numFmt = '#,##0.##';
    row.getCell(4).numFmt = '0.0%';
    for (let c = 1; c <= 6; c++) {
      const cell = row.getCell(c);
      cell.border = borda();
      if (c >= 2) cell.alignment = { horizontal: 'right' };
    }
  });

  const linhaTotal = linhaInicial + agregados.length;
  const totalRow = ws.getRow(linhaTotal);
  totalRow.getCell(1).value = 'Total geral';
  const ini = linhaInicial;
  const fim = linhaTotal - 1;
  if (agregados.length) {
    totalRow.getCell(2).value = { formula: `SUM(B${ini}:B${fim})` };
    totalRow.getCell(3).value = { formula: `SUM(C${ini}:C${fim})` };
    totalRow.getCell(4).value = { formula: `IF(B${linhaTotal}=0,"",C${linhaTotal}/B${linhaTotal})` };
    totalRow.getCell(5).value = { formula: `SUM(E${ini}:E${fim})` };
    totalRow.getCell(6).value = { formula: `SUM(F${ini}:F${fim})` };
  }
  totalRow.getCell(2).numFmt = '#,##0.##';
  totalRow.getCell(3).numFmt = '#,##0.##';
  totalRow.getCell(4).numFmt = '0.0%';
  for (let c = 1; c <= 6; c++) {
    const cell = totalRow.getCell(c);
    cell.font = { bold: true, color: { argb: VERMELHO_ESCURO } };
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: CINZA_SUAVE } };
    cell.border = { ...borda(), top: { style: 'medium', color: { argb: VERMELHO } } };
    if (c >= 2) cell.alignment = { horizontal: 'right' };
  }

  // Barras de dados na coluna de %.
  if (agregados.length) {
    try {
      ws.addConditionalFormatting({
        ref: `D${linhaInicial}:D${fim}`,
        rules: [
          {
            type: 'dataBar',
            cfvo: [
              { type: 'num', value: 0 },
              { type: 'num', value: 1 },
            ],
            color: { argb: VERDE },
          } as unknown as ExcelJS.ConditionalFormattingRule,
        ],
      });
    } catch {
      /* barra de dados é enfeite: se a versão do exceljs não aceitar, segue sem. */
    }
  }

  // Pizzas (imagens). Valores calculados agora (foto do momento).
  const totPossiveis = agregados.reduce((s, a) => s + a.pontosPossiveis, 0);
  const totAlcancados = agregados.reduce((s, a) => s + a.pontosAlcancados, 0);
  const naoAlcancados = Math.max(0, totPossiveis - totAlcancados);

  const pizzas: { titulo: string; fatias: FatiaPizza[] }[] = [
    {
      titulo: 'Pontuação alcançada × não alcançada',
      fatias: [
        { rotulo: 'Alcançado', valor: totAlcancados, cor: COR_ATINGIDO },
        { rotulo: 'Não alcançado', valor: naoAlcancados, cor: COR_NAO_ATINGIDO },
      ],
    },
  ];
  if (agregados.length > 1) {
    pizzas.push({
      titulo: 'Pontos alcançados por aba',
      fatias: agregados.map((a, i) => ({
        rotulo: a.aba.rotulo,
        valor: a.pontosAlcancados,
        cor: CORES_ABA[i % CORES_ABA.length],
      })),
    });
  }

  let linhaImg = linhaTotal + 2;
  for (const p of pizzas) {
    const img = renderizarPizza(p.titulo, p.fatias);
    if (!img) continue;
    const id = wb.addImage({ base64: img.base64, extension: 'png' });
    ws.addImage(id, {
      tl: { col: 0.2, row: linhaImg - 0.7 },
      ext: { width: img.largura, height: img.altura },
    });
    linhaImg += 12;
  }
}

/** Gera o arquivo .xlsx (Blob) com o resumo + uma planilha por aba. */
export async function gerarXlsxIndicadores(dados: DadosExportacao): Promise<Blob> {
  const abas = dados.abas.filter((a) => a.itens.length > 0);
  const wb = new ExcelJS.Workbook();
  wb.creator = 'SMSMarica';
  wb.created = new Date();

  const logoBase64 = await carregarLogoBase64();
  const logoId = logoBase64 ? wb.addImage({ base64: logoBase64, extension: 'png' }) : undefined;

  const wsResumo = wb.addWorksheet('Resumo Geral', { views: [{ showGridLines: false }] });

  const usados = new Set<string>(['resumo geral']);
  const agregados: AgregadoAba[] = [];
  for (const aba of abas) {
    const nome = nomePlanilha(aba.rotulo, usados);
    const ws = wb.addWorksheet(nome, { views: [{ showGridLines: false }] });
    const linhaTotal = montarPlanilhaAba(ws, logoId, dados, aba);
    const ag = agregar(aba.itens);
    agregados.push({ aba, nomePlanilha: nome, linhaTotal, ...ag });
  }

  montarResumo(wsResumo, wb, logoId, dados, agregados);

  const buffer = await wb.xlsx.writeBuffer();
  return new Blob([buffer], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
}

/** Dispara o download de um Blob no navegador. */
export function baixarBlob(blob: Blob, nomeArquivo: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = nomeArquivo;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
