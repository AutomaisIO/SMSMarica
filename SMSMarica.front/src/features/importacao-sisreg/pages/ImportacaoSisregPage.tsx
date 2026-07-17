import { useRef, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import {
  UploadCloud,
  CheckCircle2,
  AlertTriangle,
  Ban,
  FileText,
  ListChecks,
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
import { useFalhasImportacao } from '@/features/importacao-sisreg/api/queries';
import { ErrosImportacao } from '@/features/importacao-sisreg/components/ErrosImportacao';
import { ImportacaoLote } from '@/features/importacao-sisreg/components/ImportacaoLote';
import { RastreioImportacao } from '@/features/importacao-sisreg/components/RastreioImportacao';
import { useQueryClient } from '@tanstack/react-query';
import { importacaoKeys } from '@/features/importacao-sisreg/api/queries';
import type {
  ImportacaoExecucaoResultado,
  ImportacaoPreviewItem,
} from '@/features/importacao-sisreg/types';

function formatarDataHora(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

type AbaImportacao = 'um' | 'lote' | 'rastreio' | 'erros';

export function ImportacaoSisregPage() {
  const inputRef = useRef<HTMLInputElement>(null);
  const queryClient = useQueryClient();
  const [aba, setAba] = useState<AbaImportacao>('um');
  const [arquivo, setArquivo] = useState<File | null>(null);
  // Resultado de cada importação (por código), para marcar a linha e abrir o modal.
  const [resultados, setResultados] = useState<Record<string, ImportacaoExecucaoResultado>>({});
  const [importando, setImportando] = useState<string | null>(null);
  const [modal, setModal] = useState<ImportacaoExecucaoResultado | null>(null);
  // Importação em lote ("Importar todos"): flag + progresso + cancelamento.
  const [importandoTodos, setImportandoTodos] = useState(false);
  const [progresso, setProgresso] = useState<
    { total: number; feitos: number; ok: number; falhas: number } | null
  >(null);
  const cancelarRef = useRef(false);

  const preview = useMutation({
    mutationFn: (f: File) => previewImportacaoTxt(f),
    onSuccess: () => {
      setResultados({});
      setProgresso(null);
    },
  });

  // Executa um código e grava o resultado no map. NÃO abre o modal (reusado pelo lote).
  async function executarUm(codigo: string): Promise<ImportacaoExecucaoResultado> {
    setImportando(codigo);
    try {
      if (!arquivo) throw new Error('Nenhum arquivo selecionado.');
      const res = await executarImportacaoTxt(arquivo, codigo);
      setResultados((m) => ({ ...m, [codigo]: res }));
      return res;
    } catch (e) {
      const erro: ImportacaoExecucaoResultado = {
        codigoSolicitacao: codigo,
        sucesso: false,
        solicitacaoId: null,
        accessionNumber: null,
        pacienteNome: null,
        pacienteCriado: false,
        unidadeSolicitanteCriada: false,
        unidadeExecutanteCriada: false,
        passos: [],
        erro: extrairMensagemDeErro(e),
      };
      setResultados((m) => ({ ...m, [codigo]: erro }));
      return erro;
    } finally {
      setImportando(null);
    }
  }

  // Importar UM (manual, pelo botão da linha) — abre o modal com o resultado.
  async function importar(codigo: string) {
    if (!arquivo) return;
    const res = await executarUm(codigo);
    setModal(res);
  }

  // Importar TODOS os pendentes (novos ainda não importados com sucesso), em série.
  // Série (não paralelo): o backend não é seguro para concorrência (SaveChanges +
  // idempotência por nº do SISREG). Pode ser interrompido pelo botão "Parar".
  async function importarTodos() {
    if (!arquivo || !r) return;
    const pendentes = r.itens
      .filter((i) => !i.jaExiste && resultados[i.codigoSolicitacao]?.sucesso !== true)
      .map((i) => i.codigoSolicitacao);
    if (pendentes.length === 0) return;

    cancelarRef.current = false;
    setImportandoTodos(true);
    setProgresso({ total: pendentes.length, feitos: 0, ok: 0, falhas: 0 });
    for (const codigo of pendentes) {
      if (cancelarRef.current) break;
      const res = await executarUm(codigo);
      setProgresso((p) =>
        p
          ? {
              ...p,
              feitos: p.feitos + 1,
              ok: p.ok + (res.sucesso ? 1 : 0),
              falhas: p.falhas + (res.sucesso ? 0 : 1),
            }
          : p,
      );
    }
    setImportandoTodos(false);
  }

  const r = preview.data;
  const pendentesCount = r
    ? r.itens.filter((i) => !i.jaExiste && resultados[i.codigoSolicitacao]?.sucesso !== true).length
    : 0;
  // Contador do badge da aba Erros — o que ficou pendente de correção, entre todas as importações.
  const errosPendentes = useFalhasImportacao(true).data?.length ?? 0;

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Importação SISREG</h1>
        <p className="mt-1 text-sm text-gray-500">
          Export de agendamentos do SISREG (<strong>TXT</strong> ou <strong>CSV</strong>). A{' '}
          <strong>unidade executante</strong> é a do seu <strong>contexto atual</strong>. Use{' '}
          <strong>Um arquivo</strong> para conferir no preview antes de importar, ou{' '}
          <strong>Vários / pasta / zip</strong> para importar tudo de uma vez.
        </p>
      </header>

      <nav className="flex gap-1 border-b border-gray-200">
        <Aba ativa={aba === 'um'} onClick={() => setAba('um')}>
          Um arquivo
        </Aba>
        <Aba ativa={aba === 'lote'} onClick={() => setAba('lote')}>
          Vários / pasta / zip
        </Aba>
        <Aba ativa={aba === 'rastreio'} onClick={() => setAba('rastreio')}>
          Rastreio
        </Aba>
        <Aba ativa={aba === 'erros'} onClick={() => setAba('erros')}>
          Erros
          {errosPendentes > 0 ? (
            <span className="ml-1.5 rounded-full bg-red-100 px-1.5 py-0.5 text-[10px] font-semibold text-red-700">
              {errosPendentes}
            </span>
          ) : null}
        </Aba>
      </nav>

      {aba === 'lote' ? (
        <ImportacaoLote
          aoConcluir={() => {
            queryClient.invalidateQueries({ queryKey: importacaoKeys.execucoes });
            queryClient.invalidateQueries({ queryKey: ['importacao-sisreg', 'falhas'] });
          }}
        />
      ) : null}
      {aba === 'rastreio' ? <RastreioImportacao aoVerErros={() => setAba('erros')} /> : null}
      {aba === 'erros' ? <ErrosImportacao /> : null}

      <div className={aba === 'um' ? 'space-y-6' : 'hidden'}>
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center gap-3">
          <input
            ref={inputRef}
            type="file"
            accept=".txt,.csv,text/plain,text/csv"
            className="hidden"
            onChange={(e) => setArquivo(e.target.files?.[0] ?? null)}
          />
          <Button variante="outline" onClick={() => inputRef.current?.click()}>
            <FileText className="h-4 w-4" /> Escolher arquivo (TXT ou CSV)
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
        {r && r.rejeitadas > 0 ? (
          <div className="mt-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
            <AlertTriangle className="mr-1 inline h-4 w-4" />
            {r.rejeitadas} linha(s) do arquivo não puderam ser lidas e não aparecem no preview.
            Elas foram guardadas na{' '}
            <button type="button" className="font-semibold underline" onClick={() => setAba('erros')}>
              aba Erros
            </button>
            .
          </div>
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
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-gray-100 px-4 py-3">
              <div>
                <h2 className="text-sm font-semibold text-gray-900">Diferenças no período</h2>
                <p className="text-xs text-gray-500">
                  Importe tudo de uma vez ou registro a registro pelo botão da linha.
                </p>
              </div>
              <div className="flex items-center gap-2">
                {importandoTodos ? (
                  <Button
                    variante="outline"
                    onClick={() => {
                      cancelarRef.current = true;
                    }}
                  >
                    <Ban className="h-4 w-4" /> Parar
                  </Button>
                ) : null}
                <Button
                  onClick={importarTodos}
                  disabled={!arquivo || importandoTodos || importando !== null || pendentesCount === 0}
                >
                  {importandoTodos ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <ListChecks className="h-4 w-4" />
                  )}
                  {importandoTodos
                    ? 'Importando…'
                    : pendentesCount > 0
                      ? `Importar todos (${pendentesCount})`
                      : 'Tudo importado'}
                </Button>
              </div>
            </div>

            {progresso ? (
              <div className="border-b border-gray-100 px-4 py-3">
                <div className="mb-1 flex items-center justify-between text-xs text-gray-600">
                  <span>
                    {progresso.feitos} de {progresso.total} processados
                    {' · '}
                    <span className="text-emerald-700">{progresso.ok} ok</span>
                    {progresso.falhas > 0 ? (
                      <span className="text-red-600"> · {progresso.falhas} com falha</span>
                    ) : null}
                  </span>
                  {!importandoTodos ? <span className="text-gray-400">Concluído</span> : null}
                </div>
                <div className="h-2 w-full overflow-hidden rounded-full bg-gray-100">
                  <div
                    className="h-full rounded-full bg-emerald-500 transition-all"
                    style={{
                      width: `${progresso.total > 0 ? (progresso.feitos / progresso.total) * 100 : 0}%`,
                    }}
                  />
                </div>
              </div>
            ) : null}
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500">
                  <tr>
                    <th className="px-3 py-2">Status</th>
                    <th className="px-3 py-2">Nº SISREG</th>
                    <th className="px-3 py-2">Data/Hora</th>
                    <th className="px-3 py-2">Paciente</th>
                    <th className="px-3 py-2">Procedimento</th>
                    <th className="px-3 py-2">Unidade executante</th>
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
                      podeImportar={Boolean(arquivo) && importando === null && !importandoTodos}
                      onImportar={() => importar(i.codigoSolicitacao)}
                      onVerResultado={(res) => setModal(res)}
                    />
                  ))}
                  {r.itens.length === 0 ? (
                    <tr>
                      <td colSpan={9} className="px-3 py-6 text-center text-gray-400">
                        Nenhuma marcação no período.
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

      {modal ? <ModalResultado resultado={modal} aoFechar={() => setModal(null)} /> : null}
    </div>
  );
}

function Aba({
  ativa,
  onClick,
  children,
}: {
  ativa: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`-mb-px flex items-center border-b-2 px-4 py-2 text-sm font-medium ${
        ativa
          ? 'border-red-600 text-red-700'
          : 'border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700'
      }`}
    >
      {children}
    </button>
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
        {item.unidadeExecutanteExiste ? (
          item.nomeUnidadeExecutante ?? '—'
        ) : (
          <span className="text-xs text-amber-700" title="Selecione a unidade no seu contexto para importar">
            selecione a unidade
          </span>
        )}
      </td>
      <td className="px-3 py-2">
        {item.nomeUnidadeSolicitante ?? '—'}
        {item.nomeUnidadeSolicitante && !item.unidadeSolicitanteExiste ? (
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
                  rotulo="Unidade executante"
                  valor={resultado.unidadeExecutanteCriada ? 'criada (cabeçalho do arquivo)' : 'contexto atual'}
                />
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
