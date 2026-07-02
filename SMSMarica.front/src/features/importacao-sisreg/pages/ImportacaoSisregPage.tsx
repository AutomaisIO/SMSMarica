import { useRef, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import {
  UploadCloud,
  CheckCircle2,
  AlertTriangle,
  FileText,
  Loader2,
  X,
  Play,
} from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  executarImportacaoTxt,
  previewImportacaoTxt,
} from '@/features/importacao-sisreg/api/importacaoApi';
import type {
  ImportacaoExecucaoResultado,
  ImportacaoPreviewItem,
} from '@/features/importacao-sisreg/types';

function formatarDataHora(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

export function ImportacaoSisregPage() {
  const inputRef = useRef<HTMLInputElement>(null);
  const [arquivo, setArquivo] = useState<File | null>(null);
  // Resultado de cada importação (por código), para marcar a linha e abrir o modal.
  const [resultados, setResultados] = useState<Record<string, ImportacaoExecucaoResultado>>({});
  const [importando, setImportando] = useState<string | null>(null);
  const [modal, setModal] = useState<ImportacaoExecucaoResultado | null>(null);

  const preview = useMutation({
    mutationFn: (f: File) => previewImportacaoTxt(f),
    onSuccess: () => {
      setResultados({});
    },
  });

  async function importar(codigo: string) {
    if (!arquivo) return;
    setImportando(codigo);
    try {
      const res = await executarImportacaoTxt(arquivo, codigo);
      setResultados((m) => ({ ...m, [codigo]: res }));
      setModal(res);
    } catch (e) {
      const erro: ImportacaoExecucaoResultado = {
        codigoSolicitacao: codigo,
        sucesso: false,
        solicitacaoId: null,
        accessionNumber: null,
        pacienteNome: null,
        pacienteCriado: false,
        unidadeSolicitanteCriada: false,
        passos: [],
        erro: extrairMensagemDeErro(e),
      };
      setResultados((m) => ({ ...m, [codigo]: erro }));
      setModal(erro);
    } finally {
      setImportando(null);
    }
  }

  const r = preview.data;

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Importação SISREG</h1>
        <p className="mt-1 text-sm text-gray-500">
          Envie o <strong>Arquivo Agendamento (TXT)</strong> exportado do SISREG e gere o{' '}
          <strong>preview</strong>. Depois importe <strong>um a um</strong> pelo botão de cada linha
          e confira o resultado no modal.
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
            Período do arquivo: {r.inicio} a {r.fim}
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
            <div className="border-b border-gray-100 px-4 py-3">
              <h2 className="text-sm font-semibold text-gray-900">Diferenças no período</h2>
              <p className="text-xs text-gray-500">Importe cada registro pelo botão da linha e confira.</p>
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
                    <th className="px-3 py-2 text-right">Ação</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {r.itens.map((i) => (
                    <LinhaItem
                      key={i.codigoSolicitacao}
                      item={i}
                      resultado={resultados[i.codigoSolicitacao]}
                      importando={importando === i.codigoSolicitacao}
                      podeImportar={Boolean(arquivo) && importando === null}
                      onImportar={() => importar(i.codigoSolicitacao)}
                      onVerResultado={(res) => setModal(res)}
                    />
                  ))}
                  {r.itens.length === 0 ? (
                    <tr>
                      <td colSpan={8} className="px-3 py-6 text-center text-gray-400">
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

      {modal ? <ModalResultado resultado={modal} aoFechar={() => setModal(null)} /> : null}
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

function LinhaItem({
  item,
  resultado,
  importando,
  podeImportar,
  onImportar,
  onVerResultado,
}: {
  item: ImportacaoPreviewItem;
  resultado: ImportacaoExecucaoResultado | undefined;
  importando: boolean;
  podeImportar: boolean;
  onImportar: () => void;
  onVerResultado: (r: ImportacaoExecucaoResultado) => void;
}) {
  const importado = resultado?.sucesso === true;
  const falhou = resultado?.sucesso === false;
  return (
    <tr className={item.jaExiste || importado ? 'bg-gray-50/60 text-gray-500' : ''}>
      <td className="px-3 py-2">
        {importado ? (
          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-600 px-2 py-0.5 text-xs font-medium text-white">
            <CheckCircle2 className="h-3.5 w-3.5" /> Importado
          </span>
        ) : item.jaExiste ? (
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
      <td className="px-3 py-2 text-right">
        {item.jaExiste ? (
          <span className="text-xs text-gray-400">—</span>
        ) : importado || falhou ? (
          <button
            type="button"
            className={`text-xs underline ${falhou ? 'text-red-600' : 'text-emerald-700'}`}
            onClick={() => resultado && onVerResultado(resultado)}
          >
            {falhou ? 'ver erro' : 'ver resultado'}
          </button>
        ) : (
          <Button
            variante="outline"
            className="!px-2 !py-1 text-xs"
            disabled={!podeImportar}
            onClick={onImportar}
          >
            {importando ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Play className="h-3.5 w-3.5" />}
            {importando ? 'Importando…' : 'Importar'}
          </Button>
        )}
      </td>
    </tr>
  );
}

function ModalResultado({
  resultado,
  aoFechar,
}: {
  resultado: ImportacaoExecucaoResultado;
  aoFechar: () => void;
}) {
  const ok = resultado.sucesso;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={aoFechar}>
      <div
        className="w-full max-w-lg rounded-lg bg-white shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className={`flex items-center justify-between rounded-t-lg px-4 py-3 ${ok ? 'bg-emerald-50' : 'bg-red-50'}`}>
          <h3 className={`text-sm font-semibold ${ok ? 'text-emerald-800' : 'text-red-800'}`}>
            {ok ? 'Importação concluída' : 'Não foi possível importar'} · Nº {resultado.codigoSolicitacao}
          </h3>
          <button type="button" onClick={aoFechar} className="rounded p-1 text-gray-500 hover:bg-gray-100">
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="space-y-3 px-4 py-4 text-sm">
          {ok ? (
            <>
              <div className="grid grid-cols-2 gap-2">
                <Info rotulo="Paciente" valor={resultado.pacienteNome ?? '—'} />
                <Info rotulo="Paciente" valor={resultado.pacienteCriado ? 'criado' : 'já existia (reusado)'} />
                <Info rotulo="Accession" valor={resultado.accessionNumber ?? '—'} />
                <Info
                  rotulo="Unidade solicitante"
                  valor={resultado.unidadeSolicitanteCriada ? 'criada' : 'já existia'}
                />
              </div>
              {resultado.passos.length > 0 ? (
                <div>
                  <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-gray-500">Passos</div>
                  <ol className="list-decimal space-y-1 pl-5 text-gray-700">
                    {resultado.passos.map((p, idx) => (
                      <li key={idx}>{p}</li>
                    ))}
                  </ol>
                </div>
              ) : null}
            </>
          ) : (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-red-700">
              {resultado.erro ?? 'Erro desconhecido.'}
            </div>
          )}
        </div>
        <div className="flex justify-end border-t border-gray-100 px-4 py-3">
          <Button onClick={aoFechar}>Fechar</Button>
        </div>
      </div>
    </div>
  );
}

function Info({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div>
      <div className="text-xs uppercase tracking-wide text-gray-500">{rotulo}</div>
      <div className="text-gray-900">{valor}</div>
    </div>
  );
}
