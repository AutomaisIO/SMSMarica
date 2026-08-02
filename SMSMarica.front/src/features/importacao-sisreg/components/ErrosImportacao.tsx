import { useState } from 'react';
import { AlertTriangle, CheckCircle2, Loader2, RefreshCw, Search } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useFalhasImportacao } from '@/features/importacao-sisreg/api/queries';
import { ModalFalha } from '@/features/importacao-sisreg/components/ModalFalha';
import { ModalInformarCpf } from '@/features/painel-inicio/components/ModalInformarCpf';
import { ROTULO_CAUSA } from '@/features/painel-inicio/components/LinhaPendencia';
import type { ImportacaoFalha } from '@/features/importacao-sisreg/types';

function formatarDataHora(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

const ROTULO_ORIGEM: Record<ImportacaoFalha['origem'], { texto: string; dica: string } | null> = {
  Parser: { texto: 'layout', dica: 'A linha não pôde ser lida — layout fora do esperado.' },
  Arquivo: { texto: 'arquivo', dica: 'O arquivo inteiro não é do SISREG.' },
  Execucao: null,
};

/**
 * Lista durável das linhas do SISREG que não viraram solicitação. Clicar numa linha abre o modal
 * de análise (parse + RAW + unidades + Validar). Como a importação é idempotente pelo nº do SISREG,
 * validar algo já criado dá ok e sai da lista.
 */
export function ErrosImportacao({ buscaInicial = '' }: { buscaInicial?: string }) {
  const [somentePendentes, setSomentePendentes] = useState(true);
  const [selecionada, setSelecionada] = useState<ImportacaoFalha | null>(null);
  const [pendenciaCpf, setPendenciaCpf] = useState<ImportacaoFalha | null>(null);
  const [aviso, setAviso] = useState<{ ok: boolean; texto: string } | null>(null);
  const [busca, setBusca] = useState(buscaInicial);

  const falhas = useFalhasImportacao(somentePendentes, busca);

  const lista = falhas.data ?? [];
  const pendentes = lista.filter((f) => !f.resolvidoEm).length;

  return (
    <section className="rounded-lg border border-gray-200 bg-white shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-gray-100 px-4 py-3">
        <div>
          <h2 className="text-sm font-semibold text-gray-900">Erros de importação</h2>
          <p className="text-xs text-gray-500">
            Linhas que não viraram solicitação. Clique numa linha para analisar e <strong>Validar</strong> —
            é reimportada do conteúdo guardado. Se já tiver sido criada, não duplica: dá ok e sai da lista.
          </p>
        </div>
        <div className="flex items-center gap-3">
          {/* Busca por paciente: é assim que se acha a pessoa que chegou e "não tem agendamento". */}
          <input
            type="search"
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Buscar paciente, CNS ou nº"
            className="input h-8 w-52 text-xs"
          />
          <label className="flex items-center gap-2 text-xs text-gray-600">
            <input
              type="checkbox"
              checked={somentePendentes}
              onChange={(e) => setSomentePendentes(e.target.checked)}
            />
            Só pendentes
          </label>
          <Button variante="outline" onClick={() => falhas.refetch()} disabled={falhas.isFetching}>
            {falhas.isFetching ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
            Atualizar
          </Button>
        </div>
      </div>

      {aviso ? (
        <div
          className={`border-b px-4 py-2 text-sm ${
            aviso.ok ? 'border-emerald-100 bg-emerald-50 text-emerald-800' : 'border-red-100 bg-red-50 text-red-700'
          }`}
        >
          {aviso.texto}
        </div>
      ) : null}

      {falhas.isError ? (
        <div className="px-4 py-3 text-sm text-red-700">{extrairMensagemDeErro(falhas.error)}</div>
      ) : null}

      {somentePendentes && pendentes > 0 ? (
        <div className="border-b border-gray-100 px-4 py-2 text-xs text-amber-700">
          <AlertTriangle className="mr-1 inline h-3.5 w-3.5" />
          {pendentes} linha(s) aguardando correção.
        </div>
      ) : null}

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500">
            <tr>
              <th className="px-3 py-2">Nº SISREG</th>
              <th className="px-3 py-2">Data/Hora</th>
              <th className="px-3 py-2">Paciente</th>
              <th className="px-3 py-2">Procedimento</th>
              <th className="px-3 py-2">Causa</th>
              <th className="px-3 py-2">Motivo</th>
              <th className="px-3 py-2">Tentativas</th>
              <th className="px-3 py-2 text-right">Ação</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {lista.map((f) => {
              const resolvida = Boolean(f.resolvidoEm);
              const tag = ROTULO_ORIGEM[f.origem];
              return (
                <tr
                  key={f.id}
                  className={`cursor-pointer hover:bg-gray-50 ${resolvida ? 'bg-gray-50/60 text-gray-500' : ''}`}
                  onClick={() => setSelecionada(f)}
                >
                  <td className="px-3 py-2 font-mono text-xs">
                    {f.codigoSolicitacao ?? <span className="text-gray-400">sem nº</span>}
                    {tag ? (
                      <span className="ml-1 rounded bg-gray-100 px-1 py-0.5 text-[10px] text-gray-600" title={tag.dica}>
                        {tag.texto}
                      </span>
                    ) : null}
                  </td>
                  <td className="px-3 py-2">{formatarDataHora(f.dataAgendada)}</td>
                  <td className="px-3 py-2">{f.nomePaciente ?? '—'}</td>
                  <td className="px-3 py-2">{f.procedimentoTexto ?? '—'}</td>
                  <td className="px-3 py-2 text-xs text-gray-600">{ROTULO_CAUSA[f.causa] ?? '—'}</td>
                  <td className="px-3 py-2">
                    {resolvida ? (
                      <span className="inline-flex items-center gap-1 text-xs text-emerald-700">
                        <CheckCircle2 className="h-3.5 w-3.5" /> {f.resolucaoNota ?? 'Resolvida'}
                      </span>
                    ) : (
                      <span className="text-xs text-red-700">{f.motivo}</span>
                    )}
                  </td>
                  <td className="px-3 py-2 text-xs text-gray-500">{f.tentativas}</td>
                  <td className="px-3 py-2 text-right">
                    {/* A ação depende da CAUSA: só faz sentido pedir CPF quando foi ele que
                        faltou. As demais causas ou são transitórias (revalidar basta) ou não têm
                        o dado na origem — ver ADR-0035. */}
                    {f.podeInformarCpf ? (
                      <button
                        type="button"
                        className="text-xs font-medium text-primary-700 underline"
                        onClick={(e) => {
                          e.stopPropagation();
                          setPendenciaCpf(f);
                        }}
                      >
                        Informar CPF
                      </button>
                    ) : (
                      <span className="inline-flex items-center gap-1 text-xs text-red-700 underline">
                        <Search className="h-3.5 w-3.5" /> Analisar
                      </span>
                    )}
                  </td>
                </tr>
              );
            })}
            {lista.length === 0 && !falhas.isLoading ? (
              <tr>
                <td colSpan={8} className="px-3 py-8 text-center text-sm text-gray-400">
                  {somentePendentes
                    ? 'Nenhum erro pendente — todas as linhas importadas foram aceitas.'
                    : 'Nenhum erro registrado.'}
                </td>
              </tr>
            ) : null}
            {falhas.isLoading ? (
              <tr>
                <td colSpan={8} className="px-3 py-8 text-center text-gray-400">
                  <Loader2 className="inline h-4 w-4 animate-spin" /> Carregando…
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>

      {selecionada ? (
        <ModalFalha
          falha={selecionada}
          aoFechar={() => setSelecionada(null)}
          aoResolver={(texto, ok) => setAviso({ ok, texto })}
        />
      ) : null}

      <ModalInformarCpf
        pendencia={
          pendenciaCpf && {
            id: pendenciaCpf.id,
            pacienteNome: pendenciaCpf.nomePaciente,
            procedimento: pendenciaCpf.procedimentoTexto,
            codigoSolicitacao: pendenciaCpf.codigoSolicitacao,
          }
        }
        aoFechar={() => setPendenciaCpf(null)}
      />
    </section>
  );
}
