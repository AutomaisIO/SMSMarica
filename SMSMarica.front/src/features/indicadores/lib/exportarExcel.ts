import ExcelJS from 'exceljs';
import { formatarInstante, paraDataPlanilha } from '@/shared/lib/datas';
import { renderizarPizza, type FatiaPizza } from '@/features/indicadores/lib/graficoPizza';
import type {
  AbaIndicador,
  AnaliticoIndicador,
  IndicadorResumo,
  MetaOperador,
  SituacaoIndicador,
  TipoResultadoIndicador,
} from '@/features/indicadores/types';

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
  /** Evidência linha a linha por indicador (chave = id). Ausente = analítico não foi pedido. */
  analiticos?: Map<string, AnaliticoIndicador>;
};

// Paleta da marca (ARGB, exigido pelo exceljs).
const VERMELHO = 'FFC8102E';
const VERMELHO_ESCURO = 'FFA80C27';
const BRANCO = 'FFFFFFFF';
const CINZA_TEXTO = 'FF52525B';
const CINZA_SUAVE = 'FFF4F4F5';
const CINZA_BORDA = 'FFE4E4E7';
const VERDE = 'FF16A34A';
const ROSA_SUAVE = 'FFFDF2F3';
const AMBAR = 'FFB45309';
const AZUL_LINK = 'FF0563C1';

// Cores CSS para as pizzas (canvas).
const COR_ATINGIDO = '#16A34A';
const COR_NAO_ATINGIDO = '#C8102E';
const CORES_ABA = ['#C8102E', '#E03C52', '#A80C27', '#EE6B7B', '#6C0819', '#F2A0AB'];

/**
 * Colunas da planilha de cada aba. A ordem existe para o número nunca aparecer sozinho: os
 * insumos (Numerador, Denominador, Fator) vêm antes do Resultado, e logo depois vem a regra que
 * os transforma nele. Quem lê a linha da esquerda para a direita reconstrói a conta.
 */
const COLS_ABA = [
  'Nº',
  'Indicador',
  'Meta',
  'Peso',
  'Numerador',
  'Denominador',
  'Fator',
  'Resultado',
  'Unidade',
  'Como o resultado é calculado',
  'Pontuação',
  'Situação',
  'Observação',
  'Evidência',
];
const LARGURAS_ABA = [7, 46, 20, 7, 13, 13, 9, 13, 9, 34, 11, 14, 42, 16];
const ULTIMA_COL_ABA = 'N';

// Índices (1-based) das colunas da aba — nomeados porque as fórmulas dependem deles.
const C_NUMERO = 1;
const C_NOME = 2;
const C_META = 3;
const C_PESO = 4;
const C_NUMERADOR = 5;
const C_DENOMINADOR = 6;
const C_FATOR = 7;
const C_RESULTADO = 8;
const C_UNIDADE = 9;
const C_CALCULO = 10;
const C_PONTUACAO = 11;
const C_SITUACAO = 12;
const C_OBSERVACAO = 13;
const C_EVIDENCIA = 14;

/** Notas nos cabeçalhos: a planilha tem que se explicar sem depender de quem a gerou. */
const NOTAS_CABECALHO_ABA: Record<number, string> = {
  [C_RESULTADO]:
    'O número apurado no período. Ele nunca está sozinho: os insumos estão nas colunas ' +
    'Numerador, Denominador e Fator, a regra que os combina está em "Como o resultado é ' +
    'calculado", e a memória de cálculo pactuada + o SQL executado estão na planilha ' +
    '"Memória de cálculo". A célula é fórmula viva — mexeu no numerador, o resultado acompanha.',
  [C_PONTUACAO]:
    'Tudo-ou-nada, como no contrato: bateu a meta leva o peso cheio, não bateu leva zero. ' +
    'É fórmula — a condição da meta está escrita dentro da célula.',
  [C_EVIDENCIA]:
    'Relatório analítico: a lista dos registros que entraram na conta e dos que foram ' +
    'excluídos, com o motivo. Só existe para indicadores com SQL analítico cadastrado.',
  [C_OBSERVACAO]:
    'Ressalva do indicador (limitação conhecida do dado) e erro da apuração, quando houver. ' +
    'Célula vazia aqui significa apuração limpa.',
};

const SITUACAO_ROTULO: Record<SituacaoIndicador, string> = {
  Validado: 'Validado',
  NaoValidado: 'Não validado',
  SemMotor: 'Sem motor',
  ForaDoBanco: 'Fora do banco',
};

const TIPO_ROTULO: Record<TipoResultadoIndicador, string> = {
  Razao: 'Razão',
  Densidade: 'Densidade',
  Absoluto: 'Absoluto',
  Distribuicao: 'Distribuição',
  Agrupador: 'Agrupador',
  Media: 'Média',
};

/**
 * Sigla da aba para o nome da planilha de evidência. O Excel corta nome de planilha em 31
 * caracteres, e o número do indicador se repete entre as abas — "Ev. 2 TEMPO MÉDIO PARA
 * CLASSIFICAÇÃO…" colidiria entre Adulto e Pediátrico e sairia desambiguado por um "2" no fim,
 * que não diz nada. Com a sigla na frente, cada aba se identifica no primeiro olhar.
 */
const SIGLA_ABA: Record<AbaIndicador, string> = {
  Adulto: 'Ad',
  Pediatrico: 'Ped',
  MaternoInfantil: 'MI',
  PerfilEpidemiologico: 'Epi',
  Institucional: 'Inst',
};

const OPERADOR_SIMBOLO: Record<MetaOperador, string> = {
  MenorOuIgual: '≤',
  MaiorOuIgual: '≥',
  Menor: '<',
  Maior: '>',
  Igual: '=',
  Entre: 'entre',
};

type AgregadoAba = {
  aba: AbaExportacao;
  nomePlanilha: string;
  linhaTotal: number; // linha do total dentro da planilha da aba
  pontosPossiveis: number;
  pontosAlcancados: number;
  avaliaveis: number;
  atingidos: number;
};

/** Um indicador com o nome da planilha de evidência que o acompanha (quando existir). */
type Evidencia = {
  item: IndicadorResumo;
  aba: AbaExportacao;
  analitico: AnaliticoIndicador;
  nomePlanilha: string;
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

/**
 * Nome de planilha válido no Excel: sem `\ / ? * [ ] :` e no máximo 31 caracteres. Aspas também
 * saem — o nome vai dentro de um `HYPERLINK("#'nome'!A1")` e quebraria a fórmula.
 */
function nomePlanilha(rotulo: string, usados: Set<string>): string {
  let base = rotulo.replace(/[\\/?*[\]:'"]/g, ' ').replace(/\s+/g, ' ').trim().slice(0, 31) || 'Aba';
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

/** Sempre no fuso de Brasília fixo, como manda a regra única de data/hora do sistema. */
function formatarDataHora(iso: string | null | undefined): string {
  if (!iso) return '';
  const t = formatarInstante(iso, { dateStyle: 'short', timeStyle: 'medium' });
  return t === '—' ? '' : t;
}

/** Data-só + 1 dia, em UTC para o fuso do navegador não empurrar o resultado um dia. */
function diaSeguinte(iso: string): string {
  const [a, m, d] = iso.split('-').map(Number);
  const seguinte = new Date(Date.UTC(a, m - 1, d + 1));
  return seguinte.toLocaleDateString('pt-BR', { timeZone: 'UTC' });
}

function numeroBr(v: number): string {
  return v.toLocaleString('pt-BR', { maximumFractionDigits: 2 });
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

/** A meta em linguagem de conferência ("≥ 0,85"), separada do texto livre da planilha. */
function regraNumerica(it: IndicadorResumo): string {
  if (!it.metaOperador || it.metaValor === null) return '';
  const simbolo = OPERADOR_SIMBOLO[it.metaOperador];
  if (it.metaOperador === 'Entre') {
    return `entre ${numeroBr(it.metaValor)} e ${numeroBr(it.metaValorMaximo ?? it.metaValor)}`;
  }
  return `${simbolo} ${numeroBr(it.metaValor)}`;
}

/**
 * A regra mecânica que transforma os insumos no resultado. Texto fixo de propósito: é a única
 * coisa da linha que não pode ficar desatualizada se alguém mexer nos números da planilha.
 */
function comoCalculado(it: IndicadorResumo, agrupador: boolean): string {
  if (agrupador) return 'soma dos itens agrupados abaixo';
  switch (it.tipoResultado) {
    case 'Razao':
      return 'numerador ÷ denominador';
    case 'Densidade':
      return `numerador ÷ denominador × ${numeroBr(it.fatorDensidade ?? 1000)}`;
    case 'Absoluto':
      return 'contagem direta (o próprio numerador)';
    case 'Media':
      return 'média calculada na base — o denominador é o tamanho da amostra';
    case 'Distribuicao':
      return 'distribuição por categoria — ver planilha "Distribuições"';
    default:
      return '';
  }
}

/** Ressalva do indicador + erro da apuração, na mesma célula e sem esconder nenhum dos dois. */
function observacao(it: IndicadorResumo): string {
  const partes: string[] = [];
  if (it.ressalva) partes.push(it.ressalva);
  if (it.resultado?.erro) partes.push(`Erro na apuração: ${it.resultado.erro}`);
  if (!it.resultado && it.ativo && it.temMotor) {
    partes.push('Sem apuração gravada para este período.');
  }
  return partes.join(' · ');
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

/** Faixa de texto corrido (aviso, legenda) ocupando a largura toda da planilha. */
function faixa(
  ws: ExcelJS.Worksheet,
  linha: number,
  ultimaCol: string,
  texto: string,
  cor: string,
  negrito = false,
): void {
  if (ultimaCol !== 'A') ws.mergeCells(`A${linha}:${ultimaCol}${linha}`);
  const c = ws.getCell(`A${linha}`);
  c.value = texto;
  c.font = { size: 10, color: { argb: cor }, bold: negrito };
  c.alignment = { vertical: 'middle' };
}

function estiloCabecalhoTabela(
  ws: ExcelJS.Worksheet,
  linha: number,
  colunas: string[],
  alinhamentos?: Record<number, ExcelJS.Alignment['horizontal']>,
  notas?: Record<number, string>,
): void {
  const row = ws.getRow(linha);
  colunas.forEach((titulo, idx) => {
    const c = row.getCell(idx + 1);
    c.value = titulo;
    c.font = { bold: true, color: { argb: BRANCO }, size: 11 };
    c.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: VERMELHO } };
    c.alignment = {
      vertical: 'middle',
      horizontal: alinhamentos?.[idx + 1] ?? 'left',
      wrapText: true,
    };
    c.border = borda();
    const nota = notas?.[idx + 1];
    if (nota) c.note = nota;
  });
  row.height = 30;
}

const ALINHAMENTO_ABA: Record<number, ExcelJS.Alignment['horizontal']> = {
  [C_NUMERO]: 'center',
  [C_PESO]: 'right',
  [C_NUMERADOR]: 'right',
  [C_DENOMINADOR]: 'right',
  [C_FATOR]: 'right',
  [C_RESULTADO]: 'right',
  [C_UNIDADE]: 'center',
  [C_PONTUACAO]: 'right',
  [C_SITUACAO]: 'center',
  [C_EVIDENCIA]: 'center',
};

/** Monta a planilha de uma aba com as fórmulas vivas. Devolve a linha do total. */
function montarPlanilhaAba(
  ws: ExcelJS.Worksheet,
  logoId: number | undefined,
  dados: DadosExportacao,
  aba: AbaExportacao,
  evidencias: Map<string, string>,
): number {
  LARGURAS_ABA.forEach((w, i) => (ws.getColumn(i + 1).width = w));

  cabecalho(
    ws,
    logoId,
    ULTIMA_COL_ABA,
    `Indicadores contratuais · HMCML — ${aba.rotulo}`,
    `Unidade: ${dados.unidadeNome}   ·   Período: ${formatarData(dados.inicio)} a ${formatarData(dados.fim)}`,
  );
  faixa(
    ws,
    5,
    ULTIMA_COL_ABA,
    'Cada linha é uma conta completa: insumos (Numerador · Denominador · Fator) → regra → ' +
      'Resultado → Pontuação. A memória de cálculo pactuada e o SQL executado estão na planilha ' +
      '"Memória de cálculo".',
    CINZA_TEXTO,
  );

  const linhaHeader = 6;
  estiloCabecalhoTabela(ws, linhaHeader, COLS_ABA, ALINHAMENTO_ABA, NOTAS_CABECALHO_ABA);
  ws.views = [{ state: 'frozen', xSplit: 2, ySplit: linhaHeader, showGridLines: false }];

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

    // Colunas que valem para o indicador estando ele ativo ou não — identificação e status.
    row.getCell(C_NUMERO).value = it.numero;
    const nome = row.getCell(C_NOME);
    nome.value = it.nome;
    nome.alignment = { vertical: 'top', wrapText: true, indent: it.indicadorPaiId ? 2 : 0 };
    row.getCell(C_META).value = it.meta ?? '';
    row.getCell(C_META).alignment = { vertical: 'top', wrapText: true };

    const situacao = row.getCell(C_SITUACAO);
    situacao.value = SITUACAO_ROTULO[it.situacao] ?? it.situacao;
    situacao.font = {
      size: 10,
      color: {
        argb: it.situacao === 'Validado' ? VERDE : it.situacao === 'ForaDoBanco' ? VERMELHO : CINZA_TEXTO,
      },
    };

    const obs = row.getCell(C_OBSERVACAO);
    obs.value = observacao(it);
    obs.alignment = { vertical: 'top', wrapText: true };
    if (it.resultado?.erro) obs.font = { size: 10, color: { argb: VERMELHO } };
    else obs.font = { size: 10, color: { argb: CINZA_TEXTO } };

    // Indicadores desabilitados (fora do banco / sem motor) entram só como linha informativa —
    // sem números nem pontuação. Dá visibilidade do status sem contaminar os totais.
    if (!it.ativo) {
      for (let c = 1; c <= COLS_ABA.length; c++) {
        const cell = row.getCell(c);
        cell.border = borda();
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: CINZA_SUAVE } };
        if (c !== C_SITUACAO && c !== C_OBSERVACAO) {
          cell.font = { color: { argb: CINZA_TEXTO }, italic: true };
        }
        const h = ALINHAMENTO_ABA[c];
        if (h) cell.alignment = { ...cell.alignment, horizontal: h };
      }
      row.getCell(C_CALCULO).value = 'indicador desabilitado — não é apurado nem pontua';
      row.getCell(C_CALCULO).alignment = { vertical: 'top', wrapText: true };
      row.getCell(C_EVIDENCIA).value = '—';
      return;
    }

    // D · Peso
    const peso = row.getCell(C_PESO);
    if (agr && filhosDoItem.length) {
      peso.value = { formula: `SUM(${filhosDoItem.map((f) => `D${linhaPorId.get(f.id)}`).join(',')})` };
    } else if (!agr) {
      peso.value = it.pontuacao ?? null;
    }
    peso.numFmt = '#,##0.##';

    // E/F/G · Insumos declarados. G (fator) só existe na densidade — é o multiplicador que a
    // planilha contratual aplica e que, escondido dentro da fórmula, tornaria o resultado
    // impossível de conferir a olho.
    const num = row.getCell(C_NUMERADOR);
    const den = row.getCell(C_DENOMINADOR);
    const fator = row.getCell(C_FATOR);
    if (!agr) {
      if (it.tipoResultado === 'Media') {
        // Média: valor pronto no SQL; denominador = amostra.
        den.value = res?.denominador ?? null;
      } else {
        num.value = res?.numerador ?? null;
        if (it.tipoResultado !== 'Absoluto') den.value = res?.denominador ?? null;
      }
      if (it.tipoResultado === 'Densidade') fator.value = it.fatorDensidade ?? 1000;
    }
    num.numFmt = '#,##0.##';
    den.numFmt = '#,##0.##';
    fator.numFmt = '#,##0.##';

    // H · Resultado (fórmula sempre que derivável dos insumos ao lado)
    const resultado = row.getCell(C_RESULTADO);
    if (!agr) {
      switch (it.tipoResultado) {
        case 'Razao':
          resultado.value = { formula: `IF(F${r}=0,"",E${r}/F${r})` };
          break;
        case 'Densidade':
          resultado.value = { formula: `IF(OR(F${r}=0,G${r}=0),"",E${r}/F${r}*G${r})` };
          break;
        case 'Absoluto':
          resultado.value = { formula: `IF(E${r}="","",E${r})` };
          break;
        case 'Media':
          resultado.value = res?.valor ?? null;
          break;
        default:
          // Distribuicao: não há num÷den — o resultado é a própria tabela, na planilha própria.
          break;
      }
    }
    resultado.numFmt = percentDe(it) ? '0.0%' : '#,##0.00';
    resultado.font = { bold: true };

    // I/J · Unidade e a regra que produz o resultado, lado a lado com ele.
    row.getCell(C_UNIDADE).value = it.unidadeMedida ?? '';
    const calculo = row.getCell(C_CALCULO);
    calculo.value = comoCalculado(it, agr);
    calculo.alignment = { vertical: 'top', wrapText: true };
    calculo.font = { size: 10, color: { argb: CINZA_TEXTO } };

    // K · Pontuação (fórmula tudo-ou-nada / soma dos filhos)
    const pont = row.getCell(C_PONTUACAO);
    if (agr && filhosDoItem.length) {
      pont.value = { formula: `SUM(${filhosDoItem.map((f) => `K${linhaPorId.get(f.id)}`).join(',')})` };
    } else if (!agr && it.metaOperador && it.metaValor != null) {
      const cond = condicaoMeta(it.metaOperador, `H${r}`, it.metaValor, it.metaValorMaximo);
      pont.value = { formula: `IF(H${r}="","",IF(${cond},D${r},0))` };
    }
    pont.numFmt = '#,##0.##';

    // N · Link para a evidência linha a linha, quando ela veio nesta exportação.
    const evid = row.getCell(C_EVIDENCIA);
    const planilhaEvidencia = evidencias.get(it.id);
    if (planilhaEvidencia) {
      evid.value = { formula: `HYPERLINK("#'${planilhaEvidencia}'!A1","ver registros")` };
      evid.font = { color: { argb: AZUL_LINK }, underline: true, size: 10 };
    } else if (it.temAnalitico) {
      evid.value = 'não incluída';
      evid.font = { size: 10, color: { argb: CINZA_TEXTO }, italic: true };
    } else if (agr) {
      evid.value = '—';
      evid.font = { size: 10, color: { argb: CINZA_TEXTO } };
    } else {
      evid.value = 'sem analítico';
      evid.font = { size: 10, color: { argb: AMBAR }, italic: true };
    }

    // Estilo da linha
    for (let c = 1; c <= COLS_ABA.length; c++) {
      const cell = row.getCell(c);
      cell.border = borda();
      const h = ALINHAMENTO_ABA[c];
      if (h) cell.alignment = { ...cell.alignment, horizontal: h };
    }
    if (agr) {
      for (let c = 1; c <= COLS_ABA.length; c++) {
        const cell = row.getCell(c);
        cell.font = { ...cell.font, bold: true };
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: CINZA_SUAVE } };
      }
    }
  });

  // Linha de total (soma só o topo — não conta agrupador + filhos duas vezes)
  const topo = itens.filter((i) => !i.indicadorPaiId);
  const linhaTotal = linhaInicial + itens.length;
  const total = ws.getRow(linhaTotal);
  total.getCell(C_NOME).value = 'Total de pontos';
  const refsTopoD = topo.map((i) => `D${linhaPorId.get(i.id)}`).join(',');
  const refsTopoK = topo.map((i) => `K${linhaPorId.get(i.id)}`).join(',');
  if (topo.length) {
    total.getCell(C_PESO).value = { formula: `SUM(${refsTopoD})` };
    total.getCell(C_PONTUACAO).value = { formula: `SUM(${refsTopoK})` };
  }
  total.getCell(C_PESO).numFmt = '#,##0.##';
  total.getCell(C_PONTUACAO).numFmt = '#,##0.##';
  for (let c = 1; c <= COLS_ABA.length; c++) {
    const cell = total.getCell(c);
    cell.font = { bold: true, color: { argb: VERMELHO_ESCURO } };
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: CINZA_SUAVE } };
    cell.border = { ...borda(), top: { style: 'medium', color: { argb: VERMELHO } } };
    const h = ALINHAMENTO_ABA[c];
    if (h) cell.alignment = { horizontal: h };
  }

  if (itens.length) {
    ws.autoFilter = {
      from: { row: linhaHeader, column: 1 },
      to: { row: linhaTotal - 1, column: COLS_ABA.length },
    };
  }

  return linhaTotal;
}

const COLS_MEMORIA = [
  'Aba',
  'Nº',
  'Indicador',
  'Tipo de cálculo',
  'Fonte declarada na planilha',
  'Base consultada',
  'Memória de cálculo (pactuada)',
  'Meta (texto do contrato)',
  'Regra numérica aplicada',
  'Peso',
  'Numerador',
  'Denominador',
  'Fator',
  'Resultado',
  'Pontuação',
  'Situação',
  'Ressalva',
  'Erro na apuração',
  'Apurado em',
  'Duração (ms)',
  'Evidência',
  'SQL executado',
];
const LARGURAS_MEMORIA = [
  16, 7, 44, 14, 24, 20, 60, 20, 18, 8, 13, 13, 9, 13, 11, 14, 40, 40, 18, 12, 16, 100,
];
const ULTIMA_COL_MEMORIA = 'V';

/**
 * Uma linha por indicador com tudo que sustenta o número: o que foi pactuado, o que foi
 * executado e quando. É a planilha que responde "de onde veio isso?" sem precisar do sistema.
 */
function montarMemoria(
  ws: ExcelJS.Worksheet,
  logoId: number | undefined,
  dados: DadosExportacao,
  evidencias: Map<string, string>,
): void {
  LARGURAS_MEMORIA.forEach((w, i) => (ws.getColumn(i + 1).width = w));

  cabecalho(
    ws,
    logoId,
    ULTIMA_COL_MEMORIA,
    'Memória de cálculo · Indicadores contratuais HMCML',
    `Unidade: ${dados.unidadeNome}   ·   Período: ${formatarData(dados.inicio)} a ${formatarData(dados.fim)}`,
  );

  // O :fim exclusivo é exatamente o tipo de detalhe que muda o número e ninguém lembra de contar.
  faixa(
    ws,
    5,
    ULTIMA_COL_MEMORIA,
    `Parâmetros substituídos no SQL:  :hospital = 1 (${dados.unidadeNome})  ·  ` +
      `:ini = ${formatarData(dados.inicio)}  ·  ` +
      `:fim = ${diaSeguinte(dados.fim)} — exclusivo, o SQL filtra "data < :fim", ` +
      `de modo que o dia ${formatarData(dados.fim)} entra inteiro no período.`,
    CINZA_TEXTO,
  );

  const linhaHeader = 7;
  estiloCabecalhoTabela(ws, linhaHeader, COLS_MEMORIA, {
    2: 'center',
    10: 'right',
    11: 'right',
    12: 'right',
    13: 'right',
    14: 'right',
    15: 'right',
    16: 'center',
    20: 'right',
    21: 'center',
  });
  ws.views = [{ state: 'frozen', xSplit: 3, ySplit: linhaHeader, showGridLines: false }];

  let r = linhaHeader;
  for (const aba of dados.abas) {
    const filhos = mapaFilhos(aba.itens);
    for (const it of aba.itens) {
      r += 1;
      const row = ws.getRow(r);
      const agr = ehAgrupador(it, filhos);
      const res = it.resultado;

      row.getCell(1).value = aba.rotulo;
      row.getCell(2).value = it.numero;
      row.getCell(3).value = it.nome;
      row.getCell(4).value = agr ? 'Agrupador' : TIPO_ROTULO[it.tipoResultado];
      row.getCell(5).value = it.fonteDeclarada ?? '';
      row.getCell(6).value = it.fonteNome ?? '';
      row.getCell(7).value = it.memoriaCalculo ?? '';
      row.getCell(8).value = it.meta ?? '';
      row.getCell(9).value = regraNumerica(it);
      row.getCell(10).value = agr ? null : (it.pontuacao ?? null);
      row.getCell(11).value = res?.numerador ?? null;
      row.getCell(12).value = res?.denominador ?? null;
      row.getCell(13).value = it.tipoResultado === 'Densidade' ? (it.fatorDensidade ?? 1000) : null;
      row.getCell(14).value = res?.valor ?? null;
      row.getCell(15).value = res?.pontuacaoApurada ?? null;
      row.getCell(16).value = SITUACAO_ROTULO[it.situacao] ?? it.situacao;
      row.getCell(17).value = it.ressalva ?? '';
      row.getCell(18).value = res?.erro ?? '';
      row.getCell(19).value = formatarDataHora(res?.executadoEm);
      row.getCell(20).value = res?.duracaoMs ?? null;

      const evid = row.getCell(21);
      const planilhaEvidencia = evidencias.get(it.id);
      if (planilhaEvidencia) {
        evid.value = { formula: `HYPERLINK("#'${planilhaEvidencia}'!A1","ver registros")` };
        evid.font = { color: { argb: AZUL_LINK }, underline: true, size: 10 };
      } else {
        evid.value = agr ? '—' : it.temAnalitico ? 'não incluída' : 'sem analítico';
      }

      row.getCell(22).value = agr ? '(agrupador — soma os filhos, não tem SQL próprio)' : (it.sql ?? '');

      [11, 12, 13, 14].forEach((c) => (row.getCell(c).numFmt = '#,##0.##'));
      row.getCell(10).numFmt = '#,##0.##';
      row.getCell(15).numFmt = '#,##0.##';

      for (let c = 1; c <= COLS_MEMORIA.length; c++) {
        const cell = row.getCell(c);
        cell.border = borda();
        cell.alignment = { ...cell.alignment, vertical: 'top', wrapText: c === 22 ? false : true };
        if (!cell.font) cell.font = { size: 10 };
      }
      row.getCell(22).font = { name: 'Consolas', size: 9, color: { argb: CINZA_TEXTO } };
      row.getCell(18).font = { size: 10, color: { argb: VERMELHO } };
      if (!it.ativo) {
        for (let c = 1; c <= COLS_MEMORIA.length; c++) {
          row.getCell(c).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: CINZA_SUAVE } };
        }
      }
    }
  }

  if (r > linhaHeader) {
    ws.autoFilter = {
      from: { row: linhaHeader, column: 1 },
      to: { row: r, column: COLS_MEMORIA.length },
    };
  }
}

const COLS_DISTRIBUICAO = ['Aba', 'Nº', 'Indicador', 'Categoria', 'Quantidade', '% do total'];

/**
 * Indicadores de distribuição não têm um número único — o resultado deles é a tabela. Antes ela
 * só existia na tela, o que deixava a célula "Resultado" vazia na exportação sem explicação.
 */
function montarDistribuicoes(
  ws: ExcelJS.Worksheet,
  logoId: number | undefined,
  dados: DadosExportacao,
): boolean {
  [16, 7, 44, 46, 14, 12].forEach((w, i) => (ws.getColumn(i + 1).width = w));

  cabecalho(
    ws,
    logoId,
    'F',
    'Distribuições · Indicadores contratuais HMCML',
    `Unidade: ${dados.unidadeNome}   ·   Período: ${formatarData(dados.inicio)} a ${formatarData(dados.fim)}`,
  );

  const linhaHeader = 6;
  estiloCabecalhoTabela(ws, linhaHeader, COLS_DISTRIBUICAO, { 2: 'center', 5: 'right', 6: 'right' });
  ws.views = [{ state: 'frozen', ySplit: linhaHeader, showGridLines: false }];

  let r = linhaHeader;
  for (const aba of dados.abas) {
    for (const it of aba.itens) {
      const linhas = it.resultado?.distribuicao;
      if (!linhas?.length) continue;

      const total = linhas.reduce((s, l) => s + l.quantidade, 0);
      const primeira = r + 1;

      for (const l of linhas) {
        r += 1;
        const row = ws.getRow(r);
        row.getCell(1).value = aba.rotulo;
        row.getCell(2).value = it.numero;
        row.getCell(3).value = it.nome;
        row.getCell(4).value = l.rotulo;
        row.getCell(5).value = l.quantidade;
        row.getCell(5).numFmt = '#,##0.##';
        row.getCell(6).value = total > 0 ? { formula: `E${r}/SUM(E${primeira}:E${primeira + linhas.length - 1})` } : null;
        row.getCell(6).numFmt = '0.0%';
        for (let c = 1; c <= COLS_DISTRIBUICAO.length; c++) {
          const cell = row.getCell(c);
          cell.border = borda();
          cell.alignment = { vertical: 'top', wrapText: c === 3 || c === 4 };
          if (c >= 5) cell.alignment = { horizontal: 'right' };
        }
      }

      // Total da distribuição — é ele que aparece como "Numerador" na planilha da aba.
      r += 1;
      const totalRow = ws.getRow(r);
      totalRow.getCell(4).value = `Total · ${it.numero}`;
      totalRow.getCell(5).value = { formula: `SUM(E${primeira}:E${r - 1})` };
      totalRow.getCell(5).numFmt = '#,##0.##';
      for (let c = 1; c <= COLS_DISTRIBUICAO.length; c++) {
        const cell = totalRow.getCell(c);
        cell.font = { bold: true, color: { argb: VERMELHO_ESCURO } };
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: CINZA_SUAVE } };
        cell.border = borda();
        if (c >= 5) cell.alignment = { horizontal: 'right' };
      }
    }
  }

  return r > linhaHeader;
}

/** Data ISO vinda do JSON (o Oracle devolve DATE/TIMESTAMP) vira célula de data de verdade. */
const ISO_DATA = /^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})?$/;

function escreverValor(cell: ExcelJS.Cell, bruto: unknown): void {
  if (bruto === null || bruto === undefined) {
    cell.value = null;
    return;
  }
  if (typeof bruto === 'number') {
    cell.value = bruto;
    cell.numFmt = Number.isInteger(bruto) ? '#,##0' : '#,##0.##';
    return;
  }
  if (typeof bruto === 'boolean') {
    cell.value = bruto ? 'S' : 'N';
    return;
  }
  const texto = String(bruto);
  if (ISO_DATA.test(texto)) {
    const d = paraDataPlanilha(texto);
    if (d) {
      cell.value = d;
      cell.numFmt = texto.length > 10 ? 'dd/mm/yyyy hh:mm' : 'dd/mm/yyyy';
      return;
    }
  }
  cell.value = texto;
}

/**
 * A evidência de um indicador: os registros que o SQL analítico devolveu, com as colunas do jeito
 * que ele as nomeou. A única coisa que a planilha reordena é o corte incluído/excluído — quem
 * entrou na conta vem primeiro e quem ficou de fora vai para o final, com o motivo. Dentro de
 * cada bloco a ordem da consulta é preservada, para dar para bater linha a linha com ela.
 */
function montarPlanilhaEvidencia(
  ws: ExcelJS.Worksheet,
  logoId: number | undefined,
  dados: DadosExportacao,
  ev: Evidencia,
): void {
  const { item, aba, analitico } = ev;
  const colunas = analitico.colunas;
  const ultimaCol = ws.getColumn(Math.max(1, colunas.length)).letter;

  colunas.forEach((nome, i) => {
    ws.getColumn(i + 1).width = Math.min(46, Math.max(14, nome.length + 6));
  });
  if (!colunas.length) ws.getColumn(1).width = 90;

  cabecalho(
    ws,
    logoId,
    ultimaCol,
    `Evidência · ${item.numero} — ${item.nome}`,
    `${aba.rotulo}   ·   Unidade: ${dados.unidadeNome}   ·   Período: ${formatarData(dados.inicio)} a ${formatarData(dados.fim)}`,
  );

  let linha = 5;
  const total = analitico.linhas.length;
  faixa(
    ws,
    linha,
    ultimaCol,
    `${numeroBr(total)} registro(s) lido(s)  ·  ${numeroBr(analitico.incluidos)} entraram na conta  ·  ` +
      `${numeroBr(analitico.excluidos)} excluído(s)  ·  consultado em ${formatarDataHora(analitico.executadoEm)} ` +
      `(${numeroBr(analitico.duracaoMs)} ms)`,
    CINZA_TEXTO,
    true,
  );

  if (analitico.truncado) {
    linha += 1;
    faixa(
      ws,
      linha,
      ultimaCol,
      `ATENÇÃO: o retorno bateu no teto de ${numeroBr(analitico.limiteLinhas)} linhas — esta ` +
        'evidência está truncada e não cobre o período inteiro. Os números da aba continuam ' +
        'corretos (vêm do SQL agregado), mas a conferência linha a linha aqui está incompleta.',
      VERMELHO,
      true,
    );
  }

  if (analitico.erro) {
    linha += 1;
    faixa(ws, linha, ultimaCol, `Problema no relatório analítico: ${analitico.erro}`, VERMELHO, true);
  }

  if (!colunas.length) {
    linha += 2;
    faixa(ws, linha, ultimaCol, 'Nenhum registro foi devolvido para este período.', CINZA_TEXTO);
    return;
  }

  const linhaHeader = linha + 2;
  estiloCabecalhoTabela(ws, linhaHeader, [...colunas]);
  ws.views = [{ state: 'frozen', ySplit: linhaHeader, showGridLines: false }];

  // Incluídos primeiro, excluídos no fim. Quem abre a planilha quer ler a conta antes de ler
  // as ressalvas dela; e o bloco vermelho no rodapé fecha a evidência em vez de picotá-la.
  const excluidoDe = (valores: readonly unknown[]) =>
    analitico.indiceIncluido >= 0 && !ehSim(valores[analitico.indiceIncluido]);
  const ordenadas = analitico.linhas
    .map((valores, ordemOriginal) => ({ valores, ordemOriginal }))
    .sort((a, b) =>
      Number(excluidoDe(a.valores)) - Number(excluidoDe(b.valores)) ||
      a.ordemOriginal - b.ordemOriginal,
    );

  ordenadas.forEach(({ valores }, idx) => {
    const r = linhaHeader + 1 + idx;
    const row = ws.getRow(r);
    const excluido = excluidoDe(valores);

    valores.forEach((v, c) => {
      const cell = row.getCell(c + 1);
      escreverValor(cell, v);
      cell.border = borda();
      cell.alignment = { vertical: 'top', wrapText: true };
      if (excluido) {
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: ROSA_SUAVE } };
      }
    });

    if (excluido && analitico.indiceMotivo >= 0) {
      row.getCell(analitico.indiceMotivo + 1).font = { size: 10, color: { argb: VERMELHO } };
    }
  });

  ws.autoFilter = {
    from: { row: linhaHeader, column: 1 },
    to: { row: linhaHeader + analitico.linhas.length, column: colunas.length },
  };
}

/** Espelha a regra do backend: só 'S'/'SIM'/'Y'/1/true conta como incluído. */
function ehSim(v: unknown): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v === 1;
  if (typeof v !== 'string') return false;
  return ['S', 'SIM', 'Y', 'YES', '1', 'TRUE'].includes(v.trim().toUpperCase());
}

/** Monta a planilha "Resumo Geral" com os cartões, a tabela por aba e as pizzas. */
function montarResumo(
  ws: ExcelJS.Worksheet,
  wb: ExcelJS.Workbook,
  logoId: number | undefined,
  dados: DadosExportacao,
  agregados: AgregadoAba[],
  totalEvidencias: number,
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
  estiloCabecalhoTabela(ws, linhaHeader, cols, { 2: 'right', 3: 'right', 4: 'right', 5: 'right', 6: 'right' });

  const linhaInicial = linhaHeader + 1;
  agregados.forEach((ag, idx) => {
    const r = linhaInicial + idx;
    const row = ws.getRow(r);
    const nome = ag.nomePlanilha.replace(/'/g, "''");
    row.getCell(1).value = ag.aba.rotulo;
    row.getCell(2).value = { formula: `'${nome}'!D${ag.linhaTotal}` };
    row.getCell(3).value = { formula: `'${nome}'!K${ag.linhaTotal}` };
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

  // Legenda das planilhas — sem ela o arquivo vira um monte de números sem porta de entrada.
  let l = linhaTotal + 2;
  faixa(ws, l, 'F', 'O que tem neste arquivo', VERMELHO, true);
  const legenda: string[] = [
    'Uma planilha por aba — a tabela contratual: insumos, resultado, regra de cálculo, pontuação e situação de cada indicador.',
    '"Memória de cálculo" — uma linha por indicador com o que foi pactuado, a base consultada, o SQL executado e quando ele rodou.',
    '"Distribuições" — a tabela por categoria dos indicadores de distribuição, que não têm um número único.',
    totalEvidencias > 0
      ? `${totalEvidencias} planilha(s) "Ev. …" — a evidência linha a linha: os registros que entraram na conta e os que foram excluídos, com o motivo.`
      : 'Evidência linha a linha: não incluída nesta exportação (marque a opção no momento de exportar; só sai para indicadores com SQL analítico cadastrado).',
    'Os totais consideram apenas indicadores habilitados. Os desabilitados aparecem esmaecidos, para o status ficar visível sem contaminar a pontuação.',
  ];
  for (const texto of legenda) {
    l += 1;
    faixa(ws, l, 'F', `•  ${texto}`, CINZA_TEXTO);
    ws.getRow(l).height = 26;
    ws.getCell(`A${l}`).alignment = { vertical: 'middle', wrapText: true };
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

  let linhaImg = l + 2;
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

/** Gera o arquivo .xlsx (Blob) com o resumo + uma planilha por aba + as planilhas auditáveis. */
export async function gerarXlsxIndicadores(dados: DadosExportacao): Promise<Blob> {
  // Todos os indicadores entram — inclusive os desabilitados (fora do banco / sem motor),
  // que aparecem como linha informativa. Isso dá visibilidade do status de validação; a
  // apuração e os totais continuam considerando só os ativos.
  const abas = dados.abas.filter((a) => a.itens.length > 0);
  const wb = new ExcelJS.Workbook();
  wb.creator = 'SMSMarica';
  wb.created = new Date();

  const logoBase64 = await carregarLogoBase64();
  const logoId = logoBase64 ? wb.addImage({ base64: logoBase64, extension: 'png' }) : undefined;

  const wsResumo = wb.addWorksheet('Resumo Geral', { views: [{ showGridLines: false }] });
  const usados = new Set<string>(['resumo geral', 'memória de cálculo', 'distribuições']);

  // Os nomes das planilhas de evidência precisam existir antes das planilhas de aba, que
  // apontam para eles com HYPERLINK.
  const evidencias: Evidencia[] = [];
  const nomePorIndicador = new Map<string, string>();
  if (dados.analiticos?.size) {
    for (const aba of abas) {
      for (const it of aba.itens) {
        const analitico = dados.analiticos.get(it.id);
        if (!analitico) continue;
        const nome = nomePlanilha(`Ev. ${SIGLA_ABA[aba.aba]} ${it.numero} ${it.nome}`, usados);
        nomePorIndicador.set(it.id, nome);
        evidencias.push({ item: it, aba, analitico, nomePlanilha: nome });
      }
    }
  }

  const agregados: AgregadoAba[] = [];
  for (const aba of abas) {
    const nome = nomePlanilha(aba.rotulo, usados);
    const ws = wb.addWorksheet(nome, { views: [{ showGridLines: false }] });
    const linhaTotal = montarPlanilhaAba(ws, logoId, dados, aba, nomePorIndicador);
    // Só os ativos entram nos totais/pizzas do resumo (os desabilitados não são pontuados).
    const ag = agregar(aba.itens.filter((i) => i.ativo));
    agregados.push({ aba, nomePlanilha: nome, linhaTotal, ...ag });
  }

  montarResumo(wsResumo, wb, logoId, dados, agregados, evidencias.length);

  const wsMemoria = wb.addWorksheet('Memória de cálculo', { views: [{ showGridLines: false }] });
  montarMemoria(wsMemoria, logoId, { ...dados, abas }, nomePorIndicador);

  const wsDist = wb.addWorksheet('Distribuições', { views: [{ showGridLines: false }] });
  if (!montarDistribuicoes(wsDist, logoId, { ...dados, abas })) {
    wb.removeWorksheet(wsDist.id);
  }

  for (const ev of evidencias) {
    const ws = wb.addWorksheet(ev.nomePlanilha, { views: [{ showGridLines: false }] });
    montarPlanilhaEvidencia(ws, logoId, dados, ev);
  }

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
