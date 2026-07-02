import { useRef, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { UploadCloud, CheckCircle2, AlertTriangle, FileText } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { previewImportacaoTxt } from '@/features/importacao-sisreg/api/importacaoApi';
import type { ImportacaoPreviewItem } from '@/features/importacao-sisreg/types';

function formatarDataHora(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

export function ImportacaoSisregPage() {
  const inputRef = useRef<HTMLInputElement>(null);
  const [arquivo, setArquivo] = useState<File | null>(null);

  const preview = useMutation({
    mutationFn: (f: File) => previewImportacaoTxt(f),
  });

  const r = preview.data;

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Importação SISREG</h1>
        <p className="mt-1 text-sm text-gray-500">
          Envie o <strong>Arquivo Agendamento (TXT)</strong> exportado do SISREG (menu Arquivo
          Agendamento) e gere o <strong>preview</strong>. Esta tela é <strong>somente leitura</strong> —
          nada é criado até você rodar a importação.
        </p>
      </header>

      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center gap-3">
          <input
            ref={inputRef}
            type="file"
            accept=".txt,text/plain"
            className="hidden"
            onChange={(e) => setArquivo(e.target.files?.[0] ?? null)}
          />
          <Button variante="outline" onClick={() => inputRef.current?.click()}>
            <FileText className="h-4 w-4" /> Escolher arquivo TXT
          </Button>
          <span className="text-sm text-gray-600">{arquivo ? arquivo.name : 'Nenhum arquivo selecionado'}</span>
          <Button onClick={() => arquivo && preview.mutate(arquivo)} disabled={preview.isPending || !arquivo}>
            <UploadCloud className="h-4 w-4" />
            {preview.isPending ? 'Processando…' : 'Gerar preview'}
          </Button>
        </div>
        {preview.isError ? (
          <div className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {extrairMensagemDeErro(preview.error)}
          </div>
        ) : null}
        {r ? (
          <p className="mt-3 text-xs text-gray-500">
            Período do arquivo: {formatarDataHora(`${r.inicio}T00:00:00`).slice(0, 10)} a{' '}
            {formatarDataHora(`${r.fim}T00:00:00`).slice(0, 10)}
          </p>
        ) : null}
      </section>

      {r ? (
        <>
          <section className="grid grid-cols-3 gap-3">
            <Cartao rotulo="Total no SISREG" valor={r.total} />
            <Cartao rotulo="Novos (a importar)" valor={r.novos} destaque="verde" />
            <Cartao rotulo="Já existem" valor={r.existentes} destaque="cinza" />
          </section>

          <section className="rounded-lg border border-gray-200 bg-white shadow-sm">
            <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
              <h2 className="text-sm font-semibold text-gray-900">Diferenças no período</h2>
              <Button variante="outline" disabled title="Chega na próxima fatia">
                Importar novos ({r.novos}) — em breve
              </Button>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500">
                  <tr>
                    <th className="px-3 py-2">Status</th>
                    <th className="px-3 py-2">Nº SISREG</th>
                    <th className="px-3 py-2">Data/Hora</th>
                    <th className="px-3 py-2">Paciente</th>
                    <th className="px-3 py-2">Procedimento</th>
                    <th className="px-3 py-2">Unidade solicitante</th>
                    <th className="px-3 py-2">Alertas</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {r.itens.map((i) => (
                    <LinhaItem key={i.codigoSolicitacao} item={i} />
                  ))}
                  {r.itens.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="px-3 py-6 text-center text-gray-400">
                        Nenhuma marcação de mamografia no período.
                      </td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          </section>
        </>
      ) : null}
    </div>
  );
}

function Cartao({ rotulo, valor, destaque }: { rotulo: string; valor: number; destaque?: 'verde' | 'cinza' }) {
  const cor =
    destaque === 'verde' ? 'text-emerald-700' : destaque === 'cinza' ? 'text-gray-500' : 'text-gray-900';
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <div className="text-xs uppercase tracking-wide text-gray-500">{rotulo}</div>
      <div className={`mt-1 text-2xl font-semibold ${cor}`}>{valor}</div>
    </div>
  );
}

function LinhaItem({ item }: { item: ImportacaoPreviewItem }) {
  return (
    <tr className={item.jaExiste ? 'bg-gray-50/60 text-gray-500' : ''}>
      <td className="px-3 py-2">
        {item.jaExiste ? (
          <span className="inline-flex items-center gap-1 text-xs text-gray-500">
            <CheckCircle2 className="h-3.5 w-3.5" /> Já existe
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700">
            Novo
          </span>
        )}
      </td>
      <td className="px-3 py-2 font-mono text-xs">{item.codigoSolicitacao}</td>
      <td className="px-3 py-2">{formatarDataHora(item.dataHoraAtendimento)}</td>
      <td className="px-3 py-2">{item.nomePaciente ?? '—'}</td>
      <td className="px-3 py-2">{item.procedimentoTexto ?? '—'}</td>
      <td className="px-3 py-2">
        {item.nomeUnidadeSolicitante ?? '—'}
        {item.cnesUnidadeSolicitante && !item.unidadeSolicitanteExiste ? (
          <span className="ml-1 text-xs text-amber-600">(nova)</span>
        ) : null}
      </td>
      <td className="px-3 py-2">
        {item.alertas.length > 0 ? (
          <span className="inline-flex items-center gap-1 text-xs text-amber-700" title={item.alertas.join(' | ')}>
            <AlertTriangle className="h-3.5 w-3.5" /> {item.alertas.length}
          </span>
        ) : (
          <span className="text-xs text-emerald-600">ok</span>
        )}
      </td>
    </tr>
  );
}
