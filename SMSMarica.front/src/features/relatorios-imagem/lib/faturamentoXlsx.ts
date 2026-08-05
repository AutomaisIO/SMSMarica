import ExcelJS from 'exceljs';
import { paraDataPlanilha } from '@/shared/lib/datas';
import type { ExameFaturamento } from '@/features/relatorios-imagem/types';

// Paleta da marca (ARGB, exigido pelo exceljs) — mesma da exportação de indicadores.
const VERMELHO = 'FFC8102E';
const BRANCO = 'FFFFFFFF';
const CINZA_BORDA = 'FFE5E7EB';

type Coluna = {
  titulo: string;
  larguraMin: number;
  numFmt?: string;
  valor: (l: ExameFaturamento) => string | number | Date | null;
};

// Só dígitos → "000.000.000-00".
function formatarCpf(cpf: string | null): string {
  const d = (cpf ?? '').replace(/\D/g, '');
  if (d.length !== 11) return cpf ?? '';
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

// Só dígitos → "00000-000".
function formatarCep(cep: string | null): string {
  const d = (cep ?? '').replace(/\D/g, '');
  if (d.length !== 8) return cep ?? '';
  return `${d.slice(0, 5)}-${d.slice(5)}`;
}

const COLUNAS: Coluna[] = [
  { titulo: 'PACIENTE', larguraMin: 28, valor: (l) => l.paciente },
  { titulo: 'CPF', larguraMin: 16, valor: (l) => formatarCpf(l.cpf) },
  { titulo: 'CNS', larguraMin: 18, valor: (l) => l.cns ?? '' },
  {
    titulo: 'NASCIMENTO',
    larguraMin: 13,
    numFmt: 'dd/mm/yyyy',
    valor: (l) => paraDataPlanilha(l.nascimento),
  },
  { titulo: 'CEP', larguraMin: 11, valor: (l) => formatarCep(l.cep) },
  { titulo: 'CELULAR', larguraMin: 16, valor: (l) => l.celular ?? '' },
  { titulo: 'EXAME', larguraMin: 24, valor: (l) => l.exame ?? '' },
  {
    titulo: 'REALIZAÇÃO',
    larguraMin: 13,
    numFmt: 'dd/mm/yyyy',
    valor: (l) => paraDataPlanilha(l.realizacao),
  },
];

/** Comprimento visual de uma célula, para dimensionar a largura da coluna. */
function comprimento(valor: string | number | Date | null, numFmt?: string): number {
  if (valor == null) return 0;
  if (valor instanceof Date) return numFmt ? 10 : 12; // dd/mm/yyyy
  return String(valor).length;
}

/** Monta o .xlsx de faturamento (uma aba, cabeçalho estilizado, colunas auto-dimensionadas). */
export async function gerarXlsxFaturamento(linhas: ExameFaturamento[]): Promise<Blob> {
  const wb = new ExcelJS.Workbook();
  wb.creator = 'SMS Maricá';
  const ws = wb.addWorksheet('Faturamento', {
    views: [{ state: 'frozen', ySplit: 1 }],
  });

  // Cabeçalho.
  ws.addRow(COLUNAS.map((c) => c.titulo));
  const cab = ws.getRow(1);
  cab.height = 20;
  cab.eachCell((cell) => {
    cell.font = { bold: true, color: { argb: BRANCO }, size: 11 };
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: VERMELHO } };
    cell.alignment = { vertical: 'middle', horizontal: 'left' };
  });

  // Linhas.
  for (const l of linhas) {
    const row = ws.addRow(COLUNAS.map((c) => c.valor(l)));
    COLUNAS.forEach((c, i) => {
      const cell = row.getCell(i + 1);
      if (c.numFmt) cell.numFmt = c.numFmt;
      cell.alignment = { vertical: 'middle' };
      cell.border = { bottom: { style: 'hair', color: { argb: CINZA_BORDA } } };
    });
  }

  // Largura das colunas: cabe o conteúdo (maior célula da coluna + folga), respeitando o mínimo.
  COLUNAS.forEach((c, i) => {
    let max = c.titulo.length;
    for (const l of linhas) max = Math.max(max, comprimento(c.valor(l), c.numFmt));
    ws.getColumn(i + 1).width = Math.min(60, Math.max(c.larguraMin, max + 2));
  });

  ws.autoFilter = { from: { row: 1, column: 1 }, to: { row: 1, column: COLUNAS.length } };

  const buffer = await wb.xlsx.writeBuffer();
  return new Blob([buffer], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
}
