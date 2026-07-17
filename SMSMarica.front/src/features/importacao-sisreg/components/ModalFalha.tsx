import {
  AlertTriangle,
  Building2,
  CheckCircle2,
  FileWarning,
  Loader2,
  ShieldCheck,
  Trash2,
  X,
} from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useDescartarFalha,
  useFalhaDetalhe,
  useReprocessarFalha,
} from '@/features/importacao-sisreg/api/queries';
import type { ImportacaoFalha } from '@/features/importacao-sisreg/types';
import { useState } from 'react';

function fmt(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

/**
 * Modal de análise de uma falha de importação. Mostra o PARSE do SISREG (campos nomeados) e o
 * conteúdo RAW lado a lado — o parse para entender rápido, o RAW para conferir o que veio de fato —
 * mais as unidades e o botão Validar. Para "arquivo incompatível" não há linha do SISREG a
 * parsear: degrada para o RAW + motivo, e o Validar some (não há o que revalidar).
 */
export function ModalFalha({
  falha,
  aoFechar,
  aoResolver,
}: {
  falha: ImportacaoFalha;
  aoFechar: () => void;
  aoResolver: (mensagem: string, ok: boolean) => void;
}) {
  const detalhe = useFalhaDetalhe(falha.id);
  const reprocessar = useReprocessarFalha();
  const descartar = useDescartarFalha();
  const [erroAcao, setErroAcao] = useState<string | null>(null);
  const ocupada = reprocessar.isPending || descartar.isPending;

  const d = detalhe.data;
  const resolvida = Boolean(falha.resolvidoEm);
  const ehArquivo = falha.origem === 'Arquivo';

  async function validar() {
    setErroAcao(null);
    try {
      const r = await reprocessar.mutateAsync(falha.id);
      aoResolver(r.mensagem, r.resolvida);
      if (r.resolvida) aoFechar();
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  async function remover() {
    const nota = window.prompt(
      'Descartar esta linha da lista de erros (não será importada). Motivo (opcional):',
      '',
    );
    if (nota === null) return;
    setErroAcao(null);
    try {
      await descartar.mutateAsync({ id: falha.id, nota: nota.trim() || undefined });
      aoResolver('Linha descartada — saiu da lista de pendências.', true);
      aoFechar();
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 p-4" onClick={aoFechar}>
      <div
        className="my-8 w-full max-w-3xl rounded-lg bg-white shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-gray-100 bg-red-50 px-4 py-3">
          <h3 className="flex items-center gap-2 text-sm font-semibold text-red-800">
            <FileWarning className="h-4 w-4" />
            {ehArquivo ? 'Arquivo incompatível' : 'Linha não importada'}
            {falha.codigoSolicitacao ? ` · Nº ${falha.codigoSolicitacao}` : ''}
          </h3>
          <button type="button" onClick={aoFechar} className="rounded p-1 text-gray-500 hover:bg-white">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="space-y-4 px-4 py-4 text-sm">
          {/* Motivo + metadados */}
          <div className="rounded-md border border-red-200 bg-red-50/60 px-3 py-2 text-red-800">
            <AlertTriangle className="mr-1 inline h-4 w-4" />
            {falha.motivo}
          </div>
          <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs text-gray-600 sm:grid-cols-4">
            <Meta rotulo="Arquivo" valor={falha.nomeArquivo} span />
            <Meta rotulo="Tentativas" valor={String(falha.tentativas)} />
            <Meta rotulo="Registrada" valor={fmt(falha.criadoEm)} />
            <Meta rotulo="Última tentativa" valor={fmt(falha.atualizadoEm)} />
            {resolvida ? <Meta rotulo="Resolvida" valor={`${fmt(falha.resolvidoEm)} — ${falha.resolucaoNota ?? ''}`} span /> : null}
          </div>

          {/* Unidades */}
          {d && d.parseavel ? (
            <div className="grid gap-3 sm:grid-cols-2">
              <Unidade titulo="Solicitante" nome={d.nomeUnidadeSolicitante} cnes={d.cnesUnidadeSolicitante} />
              <Unidade titulo="Executante" nome={d.nomeUnidadeExecutante} cnes={d.cnesUnidadeExecutante} />
            </div>
          ) : null}

          {/* Parse + RAW lado a lado */}
          <div className="grid gap-3 lg:grid-cols-2">
            <div>
              <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-gray-500">
                Dados do SISREG
              </div>
              {detalhe.isLoading ? (
                <div className="py-6 text-center text-gray-400">
                  <Loader2 className="inline h-4 w-4 animate-spin" /> Lendo…
                </div>
              ) : d && d.parseavel && d.campos.length > 0 ? (
                <dl className="divide-y divide-gray-100 rounded border border-gray-200">
                  {d.campos.map((c) => (
                    <div key={c.coluna} className="grid grid-cols-3 gap-2 px-2 py-1">
                      <dt className="text-xs text-gray-500">{c.rotulo}</dt>
                      <dd className="col-span-2 break-words text-gray-800">{c.valor || '—'}</dd>
                    </div>
                  ))}
                </dl>
              ) : (
                <div className="rounded border border-gray-200 bg-gray-50 px-3 py-4 text-xs text-gray-500">
                  Não há linha do SISREG para interpretar — este é um arquivo inteiro que não bate com
                  o formato de agendamentos. Veja o conteúdo ao lado.
                </div>
              )}
            </div>
            <div>
              <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-gray-500">
                {ehArquivo ? 'Trecho do arquivo' : 'Conteúdo original da linha'}
              </div>
              <pre className="max-h-72 overflow-auto whitespace-pre-wrap break-all rounded border border-gray-200 bg-gray-50 p-2 font-mono text-[11px] text-gray-700">
                {(d?.falha.linhaRaw ?? falha.linhaRaw) || '(vazio)'}
              </pre>
            </div>
          </div>

          {erroAcao ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-red-700">{erroAcao}</div>
          ) : null}
        </div>

        <div className="flex items-center justify-between border-t border-gray-100 px-4 py-3">
          <div className="text-xs text-gray-400">
            {ehArquivo ? 'Arquivo incompatível não se revalida — envie o arquivo correto ou descarte.' : ''}
          </div>
          <div className="flex gap-2">
            {!resolvida ? (
              <>
                <Button variante="outline" className="!text-red-600" disabled={ocupada} onClick={remover}>
                  <Trash2 className="h-4 w-4" /> Descartar
                </Button>
                {!ehArquivo ? (
                  <Button disabled={ocupada} onClick={validar}>
                    {reprocessar.isPending ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <ShieldCheck className="h-4 w-4" />
                    )}
                    Validar
                  </Button>
                ) : null}
              </>
            ) : (
              <span className="inline-flex items-center gap-1 text-xs text-emerald-700">
                <CheckCircle2 className="h-4 w-4" /> Resolvida
              </span>
            )}
            <Button variante="outline" onClick={aoFechar}>
              Fechar
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}

function Meta({ rotulo, valor, span }: { rotulo: string; valor: string | null; span?: boolean }) {
  return (
    <div className={span ? 'col-span-2' : ''}>
      <span className="text-gray-400">{rotulo}: </span>
      <span className="text-gray-700">{valor || '—'}</span>
    </div>
  );
}

function Unidade({ titulo, nome, cnes }: { titulo: string; nome: string | null; cnes: string | null }) {
  return (
    <div className="rounded border border-gray-200 px-3 py-2">
      <div className="flex items-center gap-1 text-xs font-semibold uppercase tracking-wide text-gray-500">
        <Building2 className="h-3.5 w-3.5" /> {titulo}
      </div>
      <div className="mt-0.5 text-gray-800">{nome || '—'}</div>
      {cnes ? <div className="text-xs text-gray-400">CNES {cnes}</div> : null}
    </div>
  );
}
