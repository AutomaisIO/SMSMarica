import { useMemo, useState } from 'react';
import { Download, FileText, Loader2, Search } from 'lucide-react';

import { baixarArquivoBpa } from '@/features/faturamento-producao/api/faturamentoProducaoApi';
import { usePreviewBpa, useUnidadesHabilitadas } from '@/features/faturamento-producao/api/queries';
import type { BpaLinha } from '@/features/faturamento-producao/types';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { notificar } from '@/shared/ui/Notificacoes';

function mesAtual(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
}

function competenciaDe(ym: string): number | null {
  const m = /^(\d{4})-(\d{2})$/.exec(ym);
  return m ? Number(m[1] + m[2]) : null;
}

function dataBr(iso: string | null): string {
  if (!iso) return '—';
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

function pct(parte: number, total: number): string {
  if (total <= 0) return '—';
  return `${Math.round((parte / total) * 100)}%`;
}

function Tile({ rotulo, valor, detalhe, tom }: { rotulo: string; valor: string | number; detalhe?: string; tom?: 'ok' | 'alerta' }) {
  const cor = tom === 'ok' ? 'text-emerald-700' : tom === 'alerta' ? 'text-amber-700' : 'text-slate-900';
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
      <div className="text-[11px] font-medium uppercase tracking-wide text-slate-500">{rotulo}</div>
      <div className={`mt-1 text-2xl font-semibold ${cor}`}>{valor}</div>
      {detalhe && <div className="mt-0.5 text-xs text-slate-500">{detalhe}</div>}
    </div>
  );
}

function LinhaTabela({ l }: { l: BpaLinha }) {
  return (
    <tr className={l.valida ? '' : 'bg-amber-50/60'}>
      <td className="whitespace-nowrap px-2 py-1">
        {l.valida ? (
          <span className="rounded bg-emerald-100 px-1.5 py-0.5 text-[11px] font-medium text-emerald-700">OK</span>
        ) : (
          <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[11px] font-medium text-amber-700" title={l.motivos.join(' · ')}>
            pendência
          </span>
        )}
      </td>
      <td className="whitespace-nowrap px-2 py-1 tabular-nums text-slate-500">
        {l.folha != null ? `${String(l.folha).padStart(3, '0')}/${String(l.sequencia).padStart(2, '0')}` : '—'}
      </td>
      <td className="whitespace-nowrap px-2 py-1 tabular-nums">{dataBr(l.dataAtendimento)}</td>
      <td className="whitespace-nowrap px-2 py-1 tabular-nums font-medium">{l.procedimentoSigtap ?? '—'}</td>
      <td className="max-w-[16rem] truncate px-2 py-1 text-slate-600" title={l.procedimentoTexto ?? ''}>{l.procedimentoTexto ?? '—'}</td>
      <td className="whitespace-nowrap px-2 py-1 tabular-nums">{l.cid ?? '—'}</td>
      <td className="max-w-[14rem] truncate px-2 py-1" title={l.pacienteNome}>{l.pacienteNome || '—'}</td>
      <td className="whitespace-nowrap px-2 py-1 tabular-nums text-slate-600">{l.cns ?? l.cpf ?? '—'}</td>
      <td className="whitespace-nowrap px-2 py-1 tabular-nums">{dataBr(l.nascimento)}</td>
      <td className="whitespace-nowrap px-2 py-1 text-center">{l.sexo || '—'}</td>
      <td className="whitespace-nowrap px-2 py-1 tabular-nums text-slate-600">{l.cnsProfissional ?? '—'}</td>
      <td className="whitespace-nowrap px-2 py-1 tabular-nums text-slate-600">{l.cbo ?? '—'}</td>
      <td className="max-w-[18rem] truncate px-2 py-1 text-amber-700" title={l.motivos.join(' · ')}>{l.motivos.join(' · ')}</td>
    </tr>
  );
}

type Consulta = { unidadeId: string; competencia: number };

export function FaturamentoProducaoPage() {
  const [unidadeId, setUnidadeId] = useState<string>('');
  const [ym, setYm] = useState<string>(mesAtual());
  const [baixando, setBaixando] = useState(false);
  // A prévia só roda quando o usuário clica em "Gerar prévia" (não automaticamente ao trocar os
  // seletores). `consulta` guarda a seleção submetida; mudar unidade/competência a limpa.
  const [consulta, setConsulta] = useState<Consulta | null>(null);

  const unidades = useUnidadesHabilitadas();
  const competencia = competenciaDe(ym);
  const preview = usePreviewBpa(consulta?.unidadeId ?? null, consulta?.competencia ?? null);

  const est = preview.data?.estatisticas;
  const podeGerarPrevia = Boolean(unidadeId && competencia);
  const podeBaixar = Boolean(consulta && (est?.validas ?? 0) > 0);

  const linhasOrdenadas = useMemo(() => {
    const linhas = preview.data?.linhas ?? [];
    return [...linhas].sort((a, b) => {
      if (a.valida !== b.valida) return a.valida ? 1 : -1;
      return (a.folha ?? 0) - (b.folha ?? 0) || (a.sequencia ?? 0) - (b.sequencia ?? 0);
    });
  }, [preview.data]);

  function gerarPrevia() {
    if (!unidadeId || !competencia) return;
    if (consulta && consulta.unidadeId === unidadeId && consulta.competencia === competencia) {
      void preview.refetch(); // mesma seleção → atualiza (ex.: profissional conciliado depois)
    } else {
      setConsulta({ unidadeId, competencia });
    }
  }

  async function baixar() {
    if (!consulta) return;
    setBaixando(true);
    try {
      await baixarArquivoBpa(consulta.unidadeId, consulta.competencia);
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    } finally {
      setBaixando(false);
    }
  }

  return (
    <div className="mx-auto max-w-[1200px] space-y-4 p-4">
      <header className="flex items-center gap-2">
        <FileText className="size-6 text-red-600" />
        <div>
          <h1 className="text-lg font-semibold text-slate-900">Faturamento — Produção (BPA)</h1>
          <p className="text-sm text-slate-500">
            Gera o arquivo BPA Magnético a partir da produção da nossa base (SISREG, com comparecimento confirmado).
          </p>
        </div>
      </header>

      <div className="flex flex-wrap items-end gap-3 rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
        <label className="flex flex-col text-sm">
          <span className="mb-1 font-medium text-slate-700">Unidade</span>
          <select
            className="min-w-[18rem] rounded border border-slate-300 px-2 py-1.5"
            value={unidadeId}
            onChange={(e) => {
              setUnidadeId(e.target.value);
              setConsulta(null);
            }}
          >
            <option value="">Selecione…</option>
            {(unidades.data ?? []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.nome}
                {u.cnes ? ` (CNES ${u.cnes})` : ''}
              </option>
            ))}
          </select>
        </label>

        <label className="flex flex-col text-sm">
          <span className="mb-1 font-medium text-slate-700">Competência</span>
          <input
            type="month"
            className="rounded border border-slate-300 px-2 py-1.5"
            value={ym}
            onChange={(e) => {
              setYm(e.target.value);
              setConsulta(null);
            }}
          />
        </label>

        <button
          type="button"
          onClick={gerarPrevia}
          disabled={!podeGerarPrevia || preview.isFetching}
          className="inline-flex items-center gap-2 rounded border border-red-600 px-4 py-2 text-sm font-medium text-red-700 hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {preview.isFetching ? <Loader2 className="size-4 animate-spin" /> : <Search className="size-4" />}
          Gerar prévia
        </button>

        <button
          type="button"
          onClick={baixar}
          disabled={!podeBaixar || baixando}
          className="ml-auto inline-flex items-center gap-2 rounded bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {baixando ? <Loader2 className="size-4 animate-spin" /> : <Download className="size-4" />}
          Gerar arquivo BPA
        </button>
      </div>

      {unidades.data && unidades.data.length === 0 && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          Nenhuma unidade habilitada para faturamento. Habilite em <strong>Faturamento → Configurações</strong>.
        </div>
      )}

      {!consulta && (
        <div className="rounded-lg border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">
          Selecione a unidade e a competência e clique em <strong>Gerar prévia</strong> para ver as linhas e as estatísticas.
        </div>
      )}

      {consulta && preview.isError && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {extrairMensagemDeErro(preview.error)}
        </div>
      )}

      {consulta && preview.isFetching && !preview.data && (
        <div className="flex items-center justify-center gap-2 p-8 text-slate-500">
          <Loader2 className="size-5 animate-spin" /> Gerando prévia…
        </div>
      )}

      {consulta && est && (
        <>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-7">
            <Tile rotulo="Procedimentos" valor={est.total} detalhe={`folhas: ${preview.data?.folhas ?? 0}`} />
            <Tile rotulo="Válidas" valor={est.validas} tom="ok" detalhe="vão para o arquivo" />
            <Tile rotulo="Pendências" valor={est.pendencias} tom={est.pendencias > 0 ? 'alerta' : 'ok'} detalhe="ficam de fora" />
            <Tile rotulo="SIGTAP" valor={pct(est.comSigtap, est.total)} detalhe={`${est.comSigtap}/${est.total}`} />
            <Tile rotulo="CNS prof." valor={pct(est.comCnsProfissional, est.total)} detalhe={`${est.comCnsProfissional}/${est.total}`} tom={est.comCnsProfissional < est.total ? 'alerta' : 'ok'} />
            <Tile rotulo="CNS/CPF pac." valor={pct(Math.max(est.comCnsPaciente, est.comCpfPaciente), est.total)} detalhe="documento do paciente" />
            <Tile rotulo="CID" valor={pct(est.comCid, est.total)} detalhe={`${est.comCid}/${est.total}`} />
          </div>

          {est.motivosPendencia.length > 0 && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
              <span className="font-medium">Motivos de pendência:</span>{' '}
              {est.motivosPendencia.map((m) => `${m.motivo} (${m.quantidade})`).join(' · ')}
            </div>
          )}

          <div className="overflow-auto rounded-lg border border-slate-200 bg-white shadow-sm" style={{ maxHeight: '60vh' }}>
            <table className="w-full text-sm">
              <thead className="sticky top-0 z-10 bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-2 py-2">Status</th>
                  <th className="px-2 py-2">Folha/Seq</th>
                  <th className="px-2 py-2">Data</th>
                  <th className="px-2 py-2">SIGTAP</th>
                  <th className="px-2 py-2">Procedimento</th>
                  <th className="px-2 py-2">CID</th>
                  <th className="px-2 py-2">Paciente</th>
                  <th className="px-2 py-2">CNS/CPF</th>
                  <th className="px-2 py-2">Nascimento</th>
                  <th className="px-2 py-2">Sexo</th>
                  <th className="px-2 py-2">CNS prof.</th>
                  <th className="px-2 py-2">CBO</th>
                  <th className="px-2 py-2">Motivo</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {linhasOrdenadas.map((l, i) => (
                  <LinhaTabela key={i} l={l} />
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
