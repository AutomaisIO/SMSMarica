import { useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Loader2,
  RefreshCw,
  ShieldCheck,
  Trash2,
} from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useDescartarFalha,
  useFalhasImportacao,
  useReprocessarFalha,
} from '@/features/importacao-sisreg/api/queries';
import type { ImportacaoFalha } from '@/features/importacao-sisreg/types';

function formatarDataHora(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

/**
 * Lista durável das linhas do SISREG que não viraram solicitação, com o conteúdo RAW.
 * O operador corrige a causa (cadastra o paciente, entra no contexto da unidade…) e clica em
 * "Validar": a linha é reimportada a partir do RAW guardado — sem precisar do arquivo de novo.
 * Como a importação é idempotente pelo nº do SISREG, validar algo já criado dá ok e sai da lista.
 */
export function ErrosImportacao() {
  const [somentePendentes, setSomentePendentes] = useState(true);
  const [expandida, setExpandida] = useState<string | null>(null);
  const [aviso, setAviso] = useState<{ ok: boolean; texto: string } | null>(null);

  const falhas = useFalhasImportacao(somentePendentes);
  const reprocessar = useReprocessarFalha();
  const descartar = useDescartarFalha();
  const ocupada = reprocessar.isPending || descartar.isPending;

  async function validar(f: ImportacaoFalha) {
    setAviso(null);
    try {
      const r = await reprocessar.mutateAsync(f.id);
      setAviso({ ok: r.resolvida, texto: r.mensagem });
    } catch (e) {
      setAviso({ ok: false, texto: extrairMensagemDeErro(e) });
    }
  }

  async function remover(f: ImportacaoFalha) {
    const nota = window.prompt(
      'Descartar esta linha da lista de erros (não será importada). Motivo (opcional):',
      '',
    );
    if (nota === null) return;
    setAviso(null);
    try {
      await descartar.mutateAsync({ id: f.id, nota: nota.trim() || undefined });
      setAviso({ ok: true, texto: 'Linha descartada — saiu da lista de pendências.' });
    } catch (e) {
      setAviso({ ok: false, texto: extrairMensagemDeErro(e) });
    }
  }

  const lista = falhas.data ?? [];
  const pendentes = lista.filter((f) => !f.resolvidoEm).length;

  return (
    <section className="rounded-lg border border-gray-200 bg-white shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-gray-100 px-4 py-3">
        <div>
          <h2 className="text-sm font-semibold text-gray-900">Erros de importação</h2>
          <p className="text-xs text-gray-500">
            Linhas que não viraram solicitação. Corrija a causa e clique em <strong>Validar</strong> — a
            linha é reimportada do conteúdo guardado. Se já tiver sido criada, não duplica: dá ok e sai
            da lista.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <label className="flex items-center gap-2 text-xs text-gray-600">
            <input
              type="checkbox"
              checked={somentePendentes}
              onChange={(e) => setSomentePendentes(e.target.checked)}
            />
            Só pendentes
          </label>
          <Button variante="outline" onClick={() => falhas.refetch()} disabled={falhas.isFetching}>
            {falhas.isFetching ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <RefreshCw className="h-4 w-4" />
            )}
            Atualizar
          </Button>
        </div>
      </div>

      {aviso ? (
        <div
          className={`border-b px-4 py-2 text-sm ${
            aviso.ok
              ? 'border-emerald-100 bg-emerald-50 text-emerald-800'
              : 'border-red-100 bg-red-50 text-red-700'
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
              <th className="w-8 px-3 py-2" />
              <th className="px-3 py-2">Nº SISREG</th>
              <th className="px-3 py-2">Data/Hora</th>
              <th className="px-3 py-2">Paciente</th>
              <th className="px-3 py-2">Procedimento</th>
              <th className="px-3 py-2">Motivo</th>
              <th className="px-3 py-2">Tentativas</th>
              <th className="px-3 py-2 text-right">Ação</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {lista.map((f) => (
              <LinhaFalha
                key={f.id}
                falha={f}
                aberta={expandida === f.id}
                ocupada={ocupada}
                onAlternar={() => setExpandida((id) => (id === f.id ? null : f.id))}
                onValidar={() => validar(f)}
                onDescartar={() => remover(f)}
              />
            ))}
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
    </section>
  );
}

function LinhaFalha({
  falha,
  aberta,
  ocupada,
  onAlternar,
  onValidar,
  onDescartar,
}: {
  falha: ImportacaoFalha;
  aberta: boolean;
  ocupada: boolean;
  onAlternar: () => void;
  onValidar: () => void;
  onDescartar: () => void;
}) {
  const resolvida = Boolean(falha.resolvidoEm);
  return (
    <>
      <tr className={resolvida ? 'bg-gray-50/60 text-gray-500' : ''}>
        <td className="px-3 py-2">
          <button type="button" onClick={onAlternar} className="rounded p-1 text-gray-400 hover:bg-gray-100">
            {aberta ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
          </button>
        </td>
        <td className="px-3 py-2 font-mono text-xs">
          {falha.codigoSolicitacao ?? <span className="text-gray-400">sem nº</span>}
          {falha.origem === 'Parser' ? (
            <span
              className="ml-1 rounded bg-gray-100 px-1 py-0.5 text-[10px] text-gray-600"
              title="A linha nem pôde ser lida — layout fora do esperado."
            >
              layout
            </span>
          ) : null}
        </td>
        <td className="px-3 py-2">{formatarDataHora(falha.dataAgendada)}</td>
        <td className="px-3 py-2">{falha.nomePaciente ?? '—'}</td>
        <td className="px-3 py-2">{falha.procedimentoTexto ?? '—'}</td>
        <td className="px-3 py-2">
          {resolvida ? (
            <span className="inline-flex items-center gap-1 text-xs text-emerald-700">
              <CheckCircle2 className="h-3.5 w-3.5" /> {falha.resolucaoNota ?? 'Resolvida'}
            </span>
          ) : (
            <span className="text-xs text-red-700">{falha.motivo}</span>
          )}
        </td>
        <td className="px-3 py-2 text-xs text-gray-500">{falha.tentativas}</td>
        <td className="px-3 py-2 text-right">
          {resolvida ? (
            <span className="text-xs text-gray-400">—</span>
          ) : (
            <div className="flex justify-end gap-1">
              <Button variante="outline" className="!px-2 !py-1 text-xs" disabled={ocupada} onClick={onValidar}>
                <ShieldCheck className="h-3.5 w-3.5" /> Validar
              </Button>
              <Button
                variante="outline"
                className="!px-2 !py-1 text-xs !text-red-600"
                disabled={ocupada}
                onClick={onDescartar}
                title="Descartar sem importar"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </Button>
            </div>
          )}
        </td>
      </tr>
      {aberta ? (
        <tr className="bg-gray-50/60">
          <td />
          <td colSpan={7} className="px-3 pb-3">
            <div className="space-y-2">
              <div className="text-xs text-gray-500">
                Arquivo: <strong>{falha.nomeArquivo ?? '—'}</strong>
                {falha.nomeExecutante ? ` · Executante: ${falha.nomeExecutante}` : null}
                {' · Registrado em '}
                {formatarDataHora(falha.criadoEm)}
                {' · Última tentativa '}
                {formatarDataHora(falha.atualizadoEm)}
              </div>
              <div>
                <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-gray-500">
                  Conteúdo original da linha
                </div>
                <pre className="max-h-40 overflow-auto whitespace-pre-wrap break-all rounded border border-gray-200 bg-white p-2 font-mono text-[11px] text-gray-700">
                  {falha.linhaRaw || '(vazio)'}
                </pre>
              </div>
            </div>
          </td>
        </tr>
      ) : null}
    </>
  );
}
