import { useMemo, useState } from 'react';
import {
  AlertTriangle,
  ChevronDown,
  Code2,
  Database,
  Download,
  ThumbsDown,
} from 'lucide-react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip as ChartTooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { cn } from '@/shared/lib/cn';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import type { RespostaIa, VisualizacaoIa } from '@/features/ia/types';

const CORES = ['#C8102E', '#E4572E', '#F4A259', '#1B998B', '#2D7DD2', '#7B2D8B', '#9CA3AF'];

type Props = {
  resposta: RespostaIa;
  onReportarErro: (resposta: RespostaIa) => void;
};

/** Converte `dados: unknown[][]` + `colunas` em objetos { coluna: valor }. */
function comoObjetos(colunas: string[], dados: unknown[][]): Record<string, unknown>[] {
  return dados.map((linha) =>
    colunas.reduce<Record<string, unknown>>((acc, col, i) => {
      acc[col] = linha[i];
      return acc;
    }, {}),
  );
}

function texto(v: unknown): string {
  if (v === null || v === undefined) return '—';
  if (typeof v === 'object') return JSON.stringify(v);
  return String(v);
}

function numero(v: unknown): number {
  const n = typeof v === 'number' ? v : Number(v);
  return Number.isFinite(n) ? n : 0;
}

function celulaCsv(v: string): string {
  return /[";\n\r]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v;
}

/** Baixa a resposta como CSV (separador ';' + BOM, amigável ao Excel pt-BR). */
function baixarCsv(resposta: RespostaIa): void {
  const linhas = [
    resposta.colunas,
    ...resposta.dados.map((linha) => linha.map((c) => texto(c))),
  ];
  const csv = linhas.map((l) => l.map(celulaCsv).join(';')).join('\r\n');
  const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${(resposta.titulo ?? resposta.fonteNome ?? 'resposta')
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .replace(/[^a-zA-Z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '') || 'resposta'}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

function CorpoVisualizacao({ resposta }: { resposta: RespostaIa }) {
  const { visualizacao, colunas, dados } = resposta;
  const objetos = useMemo(() => comoObjetos(colunas, dados), [colunas, dados]);

  if (!dados.length) {
    return <p className="text-sm text-gray-500">Sem dados para exibir.</p>;
  }

  const vis: VisualizacaoIa = visualizacao ?? 'tabela';

  if (vis === 'numero') {
    const valor = texto(dados[0]?.[0]);
    return (
      <div className="flex flex-col items-start gap-1 rounded-lg bg-primary-50 px-5 py-4">
        <span className="text-3xl font-bold text-primary-700">{valor}</span>
        {colunas[0] ? <span className="text-xs uppercase tracking-wide text-primary-600">{colunas[0]}</span> : null}
      </div>
    );
  }

  if (vis === 'lista') {
    return (
      <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200">
        {dados.map((linha, i) => (
          <li key={i} className="px-4 py-2 text-sm text-gray-700">
            {linha.map((c) => texto(c)).join(' · ')}
          </li>
        ))}
      </ul>
    );
  }

  if (vis === 'grafico_pizza') {
    const chaveNome = colunas[0];
    const chaveValor = colunas[1] ?? colunas[0];
    const dadosPie = objetos.map((o) => ({ nome: texto(o[chaveNome]), valor: numero(o[chaveValor]) }));
    return (
      <ResponsiveContainer width="100%" height={300}>
        <PieChart>
          <Pie data={dadosPie} dataKey="valor" nameKey="nome" outerRadius={100} label>
            {dadosPie.map((_, i) => (
              <Cell key={i} fill={CORES[i % CORES.length]} />
            ))}
          </Pie>
          <ChartTooltip />
          <Legend />
        </PieChart>
      </ResponsiveContainer>
    );
  }

  if (vis === 'grafico_barra') {
    const chaveX = colunas[0];
    const chaveY = colunas[1] ?? colunas[0];
    const dadosBar = objetos.map((o) => ({ nome: texto(o[chaveX]), valor: numero(o[chaveY]) }));
    return (
      <ResponsiveContainer width="100%" height={300}>
        <BarChart data={dadosBar}>
          <CartesianGrid strokeDasharray="3 3" stroke="#eee" />
          <XAxis dataKey="nome" tick={{ fontSize: 12 }} />
          <YAxis tick={{ fontSize: 12 }} />
          <ChartTooltip />
          <Bar dataKey="valor" fill="#C8102E" radius={[4, 4, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    );
  }

  if (vis === 'grafico_linha') {
    const chaveX = colunas[0];
    const chaveY = colunas[1] ?? colunas[0];
    const dadosLinha = objetos.map((o) => ({ nome: texto(o[chaveX]), valor: numero(o[chaveY]) }));
    return (
      <ResponsiveContainer width="100%" height={300}>
        <LineChart data={dadosLinha}>
          <CartesianGrid strokeDasharray="3 3" stroke="#eee" />
          <XAxis dataKey="nome" tick={{ fontSize: 12 }} />
          <YAxis tick={{ fontSize: 12 }} />
          <ChartTooltip />
          <Line type="monotone" dataKey="valor" stroke="#C8102E" strokeWidth={2} dot={false} />
        </LineChart>
      </ResponsiveContainer>
    );
  }

  // tabela (default)
  const colsTabela: Coluna<{ idx: number; linha: unknown[] }>[] = colunas.map((c, i) => ({
    chave: `${c}-${i}`,
    cabecalho: c,
    render: (item) => texto(item.linha[i]),
  }));
  const linhasTabela = dados.map((linha, idx) => ({ idx, linha }));
  return (
    <Tabela
      colunas={colsTabela}
      dados={linhasTabela}
      chaveLinha={(item) => String(item.idx)}
      vazio="Sem dados."
    />
  );
}

export function RespostaRenderer({ resposta, onReportarErro }: Props) {
  const [verSql, setVerSql] = useState(false);
  const erro = resposta.status === 'erro';
  const vazio = resposta.status === 'vazio' || (resposta.status === 'ok' && resposta.dados.length === 0);

  return (
    <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
      <header className="flex flex-wrap items-center justify-between gap-2 border-b border-gray-100 px-5 py-3">
        <div className="flex min-w-0 items-center gap-2">
          <Database className="h-4 w-4 flex-shrink-0 text-primary-600" />
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-gray-900">
              {resposta.titulo ?? resposta.fonteNome}
            </div>
            {resposta.titulo ? (
              <div className="truncate text-xs text-gray-500">{resposta.fonteNome}</div>
            ) : null}
          </div>
        </div>
        <div className="flex items-center gap-2">
          {resposta.dados.length > 0 ? (
            <button
              type="button"
              onClick={() => baixarCsv(resposta)}
              className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-600 hover:bg-gray-50 hover:text-primary-700"
              title="Baixar a resposta em CSV"
            >
              <Download className="h-3.5 w-3.5" />
              Baixar
            </button>
          ) : null}
          <button
            type="button"
            onClick={() => onReportarErro(resposta)}
            className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-600 hover:bg-gray-50 hover:text-red-700"
            title="Sinalizar que esta resposta está errada"
          >
            <ThumbsDown className="h-3.5 w-3.5" />
            Resposta errada?
          </button>
        </div>
      </header>

      <div className="space-y-3 px-5 py-4">
        {resposta.resumo ? <p className="text-sm text-gray-700">{resposta.resumo}</p> : null}

        {erro ? (
          <div className="flex items-start gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0" />
            <span>{resposta.erro ?? 'Não foi possível responder a partir desta base.'}</span>
          </div>
        ) : vazio ? (
          <p className="text-sm text-gray-500">Nenhum resultado encontrado nesta base.</p>
        ) : (
          <CorpoVisualizacao resposta={resposta} />
        )}

        {resposta.sql ? (
          <div className="rounded-md border border-gray-100 bg-gray-50">
            <button
              type="button"
              onClick={() => setVerSql((v) => !v)}
              className="flex w-full items-center justify-between px-3 py-2 text-xs font-medium text-gray-600 hover:text-gray-900"
            >
              <span className="inline-flex items-center gap-1.5">
                <Code2 className="h-3.5 w-3.5" />
                Ver detalhes (consulta gerada)
              </span>
              <ChevronDown className={cn('h-3.5 w-3.5 transition-transform', verSql && 'rotate-180')} />
            </button>
            {verSql ? (
              <pre className="overflow-x-auto border-t border-gray-100 px-3 py-2 text-xs text-gray-700">
                <code>{resposta.sql}</code>
              </pre>
            ) : null}
          </div>
        ) : null}
      </div>
    </div>
  );
}
