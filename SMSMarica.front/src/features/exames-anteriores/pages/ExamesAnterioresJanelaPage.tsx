import { useEffect, useMemo, useRef, useState } from 'react';
import { FileText, Loader2 } from 'lucide-react';
import { http, extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useAnexosExamePaciente } from '@/features/pacientes/api/queries';
import { formatarTamanhoBytes } from '@/features/anamnese/lib/anexos';
import type { AnexoExameDto } from '@/features/anamnese/types';

type ParametrosJanela = { pacienteId: string; nome?: string };

function lerParametrosDoHash(): ParametrosJanela | null {
  try {
    const hash = window.location.hash;
    if (!hash || hash.length < 2) return null;
    return JSON.parse(decodeURIComponent(hash.slice(1))) as ParametrosJanela;
  } catch {
    return null;
  }
}

function formatarDataHora(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Janela solta "Exames anteriores": lista dos documentos digitalizados do
 * paciente à esquerda; ao clicar, o PDF abre no painel à direita (stream
 * autenticado baixado como blob, já que o iframe não carrega o bearer).
 * Espelha o visualizador de imagem, mas sem controles de imagem.
 */
export function ExamesAnterioresJanelaPage() {
  const params = useMemo(lerParametrosDoHash, []);
  const pacienteId = params?.pacienteId ?? null;
  const nome = params?.nome ?? '';

  const anexos = useAnexosExamePaciente(pacienteId);
  const lista = anexos.data ?? [];

  const [selecionadoId, setSelecionadoId] = useState<string | null>(null);
  const [urlPdf, setUrlPdf] = useState<string | null>(null);
  const [carregandoPdf, setCarregandoPdf] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const urlAtualRef = useRef<string | null>(null);

  useEffect(() => {
    document.title = nome ? `Exames anteriores — ${nome}` : 'Exames anteriores';
  }, [nome]);

  // Seleciona o primeiro documento assim que a lista chega.
  useEffect(() => {
    if (!selecionadoId && lista.length > 0) setSelecionadoId(lista[0].id);
  }, [lista, selecionadoId]);

  // Baixa o PDF selecionado como blob e exibe no iframe. Revoga o anterior.
  useEffect(() => {
    if (!selecionadoId) return;
    let cancelado = false;
    setCarregandoPdf(true);
    setErro(null);
    http
      .get(`/anexos/${selecionadoId}/conteudo`, { responseType: 'blob' })
      .then((resp) => {
        if (cancelado) return;
        const url = URL.createObjectURL(new Blob([resp.data], { type: 'application/pdf' }));
        if (urlAtualRef.current) URL.revokeObjectURL(urlAtualRef.current);
        urlAtualRef.current = url;
        setUrlPdf(url);
      })
      .catch((e) => {
        if (!cancelado) setErro(extrairMensagemDeErro(e));
      })
      .finally(() => {
        if (!cancelado) setCarregandoPdf(false);
      });
    return () => {
      cancelado = true;
    };
  }, [selecionadoId]);

  // Revoga a última blob URL ao fechar a janela.
  useEffect(() => {
    return () => {
      if (urlAtualRef.current) URL.revokeObjectURL(urlAtualRef.current);
    };
  }, []);

  return (
    <div className="flex h-screen flex-col bg-gray-100">
      <header className="flex items-center gap-2 border-b border-gray-200 bg-white px-4 py-3">
        <FileText className="h-5 w-5 text-primary-600" />
        <div className="min-w-0">
          <h1 className="truncate text-base font-semibold text-gray-900">
            Exames anteriores{nome ? ` — ${nome}` : ''}
          </h1>
          <p className="text-xs text-gray-500">
            {lista.length} {lista.length === 1 ? 'documento' : 'documentos'}
          </p>
        </div>
      </header>

      <div className="flex min-h-0 flex-1">
        {/* Lista (thumbs) à esquerda */}
        <aside className="w-72 shrink-0 overflow-y-auto border-r border-gray-200 bg-white">
          {anexos.isPending ? (
            <p className="flex items-center gap-2 p-4 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" /> Carregando…
            </p>
          ) : lista.length === 0 ? (
            <p className="p-4 text-sm text-gray-400">Nenhum exame anterior para este paciente.</p>
          ) : (
            <ul className="divide-y divide-gray-100">
              {lista.map((a: AnexoExameDto) => {
                const ativo = a.id === selecionadoId;
                return (
                  <li key={a.id}>
                    <button
                      type="button"
                      onClick={() => setSelecionadoId(a.id)}
                      className={`flex w-full items-start gap-3 px-3 py-3 text-left transition ${
                        ativo ? 'bg-primary-50' : 'hover:bg-gray-50'
                      }`}
                    >
                      <span
                        className={`mt-0.5 flex h-10 w-8 shrink-0 items-center justify-center rounded border ${
                          ativo ? 'border-primary-300 bg-white' : 'border-gray-200 bg-gray-50'
                        }`}
                      >
                        <FileText className={`h-5 w-5 ${ativo ? 'text-primary-600' : 'text-gray-400'}`} />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-gray-900" title={a.nome}>
                          {a.nome}
                        </span>
                        {a.descricao ? (
                          <span className="block truncate text-xs text-gray-500" title={a.descricao}>
                            {a.descricao}
                          </span>
                        ) : null}
                        <span className="block text-xs text-gray-400">
                          {formatarDataHora(a.criadoEm)} · {formatarTamanhoBytes(a.tamanhoBytes)}
                          {a.paginas ? ` · ${a.paginas} pág.` : ''}
                        </span>
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </aside>

        {/* Documento à direita */}
        <main className="relative min-w-0 flex-1 bg-gray-200">
          {erro ? (
            <div className="m-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {erro}
            </div>
          ) : null}
          {carregandoPdf ? (
            <div className="absolute inset-0 flex items-center justify-center text-gray-500">
              <Loader2 className="mr-2 h-5 w-5 animate-spin" /> Carregando documento…
            </div>
          ) : null}
          {urlPdf && !erro ? (
            <iframe key={selecionadoId} title="Documento" src={urlPdf} className="h-full w-full border-0" />
          ) : !carregandoPdf && !erro ? (
            <div className="flex h-full items-center justify-center text-sm text-gray-400">
              Selecione um documento à esquerda.
            </div>
          ) : null}
        </main>
      </div>
    </div>
  );
}
