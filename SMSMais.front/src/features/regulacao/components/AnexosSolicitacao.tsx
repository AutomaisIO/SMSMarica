import { useState } from 'react';
import { AlertTriangle, FileText, Image as ImagemIcone } from 'lucide-react';

import { formatarTamanhoBytes } from '@/features/anamnese/lib/anexos';
import { VisualizadorArquivo } from '@/shared/acervo/VisualizadorArquivo';

import { caminhoArquivoExigencia } from '../api/acervoRegulacao';
import { useExigencias } from '../api/solicitacoesQueries';
import type { ArquivoExigencia } from '../tiposSolicitacao';

/**
 * Os anexos da solicitação, só para ler — caixinha por caixinha, como a unidade os anexou.
 *
 * <p>Existe porque, depois de enviada, a solicitação só abria no assistente para quem ainda podia
 * editá-la: o regulador analisava o pedido <b>sem ver os documentos</b>, e a própria unidade não
 * conseguia conferir o que tinha mandado. Aqui ninguém anexa nem remove — isso continua no
 * assistente, enquanto a solicitação é da unidade.</p>
 *
 * <p>Caixinha opcional vazia não aparece (não diz nada a quem lê). A obrigatória ainda pendente
 * aparece vazia de propósito: "faltou" é informação para quem analisa.</p>
 */
export function AnexosSolicitacao({ solicitacaoId }: { solicitacaoId: string }) {
  const exigencias = useExigencias(solicitacaoId);
  const [aberto, setAberto] = useState<ArquivoExigencia | null>(null);

  if (exigencias.isLoading) return null;

  const caixinhas = (exigencias.data ?? []).filter(
    (e) => e.arquivos.length > 0 || (e.obrigatoria && e.situacao === 'Pendente'),
  );

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <h2 className="mb-3 text-sm font-semibold text-slate-900">Anexos</h2>

      {exigencias.isError ? (
        <p className="text-sm text-red-700">Não foi possível carregar os anexos.</p>
      ) : caixinhas.length === 0 ? (
        <p className="text-sm text-slate-500">Nenhum arquivo anexado.</p>
      ) : (
        <div className="space-y-3">
          {caixinhas.map((e) => (
            <div key={e.id}>
              <p className="text-sm font-medium text-slate-800">
                {e.titulo}
                {e.obrigatoria ? <span className="ml-1 text-red-600">*</span> : null}
              </p>
              {e.criticaTexto ? (
                <p className="mt-0.5 flex items-start gap-1.5 text-xs text-amber-800">
                  <AlertTriangle className="mt-0.5 size-3.5 shrink-0" />
                  <span>{e.criticaTexto}</span>
                </p>
              ) : null}
              {e.arquivos.length === 0 ? (
                <p className="mt-1 text-xs text-amber-700">Nenhum arquivo nesta caixinha.</p>
              ) : (
                <ul className="mt-1 space-y-1">
                  {e.arquivos.map((a) => {
                    const Icone = a.contentType.startsWith('image/') ? ImagemIcone : FileText;
                    return (
                      <li key={a.id}>
                        <button
                          type="button"
                          onClick={() => setAberto(a)}
                          title="Visualizar arquivo"
                          className="flex w-full items-center gap-2 rounded border border-slate-200 bg-slate-50 px-2 py-1.5 text-left text-sm hover:border-red-300"
                        >
                          <Icone className="size-4 shrink-0 text-slate-400" />
                          <span className="min-w-0 flex-1">
                            <span className="block truncate text-slate-700">{a.titulo || a.nome}</span>
                            {a.titulo || a.descricao ? (
                              <span className="block truncate text-[11px] text-slate-400">
                                {[a.descricao, a.titulo ? a.nome : null].filter(Boolean).join(' · ')}
                              </span>
                            ) : null}
                          </span>
                          <span className="shrink-0 text-xs text-slate-400">
                            {formatarTamanhoBytes(a.tamanho)}
                          </span>
                          {a.enviadoAoSistemaEm ? (
                            <span
                              className="shrink-0 rounded bg-emerald-100 px-1.5 text-[11px] text-emerald-800"
                              title="Já enviado ao sistema de regulação"
                            >
                              no sistema
                            </span>
                          ) : null}
                        </button>
                      </li>
                    );
                  })}
                </ul>
              )}
            </div>
          ))}
        </div>
      )}

      {aberto ? (
        <VisualizadorArquivo
          caminho={caminhoArquivoExigencia(solicitacaoId, aberto.id)}
          titulo={aberto.titulo || aberto.nome}
          descricao={aberto.descricao}
          nomeArquivo={aberto.nome}
          aoFechar={() => setAberto(null)}
        />
      ) : null}
    </section>
  );
}
