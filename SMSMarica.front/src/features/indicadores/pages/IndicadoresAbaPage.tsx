import { useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { AlertTriangle, BarChart3, FileSpreadsheet, Loader2, Plus } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { FiltroIndicadores } from '@/features/indicadores/components/FiltroIndicadores';
import { ModalExportar } from '@/features/indicadores/components/ModalExportar';
import { ModalIndicador } from '@/features/indicadores/components/ModalIndicador';
import { TabelaIndicadores } from '@/features/indicadores/components/TabelaIndicadores';
import {
  useApurarAba,
  useApurarIndicador,
  useListarIndicadores,
  useUnidadesIndicador,
} from '@/features/indicadores/api/queries';
import {
  ABAS,
  abaPorRota,
  type FiltroIndicador,
  type IndicadorResumo,
} from '@/features/indicadores/types';

/** Mês passado fechado — é assim que o contrato é apurado. */
function periodoPadrao(): { inicio: string; fim: string } {
  const hoje = new Date();
  const ini = new Date(hoje.getFullYear(), hoje.getMonth() - 1, 1);
  const fim = new Date(hoje.getFullYear(), hoje.getMonth(), 0);
  const iso = (d: Date) =>
    `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  return { inicio: iso(ini), fim: iso(fim) };
}

export function IndicadoresAbaPage() {
  const { aba: rota } = useParams<{ aba: string }>();
  const aba = abaPorRota(rota);
  const meta = ABAS.find((a) => a.id === aba);

  const podeEditar = usePermissao('Indicadores', 'Edicao');

  const [filtro, setFiltro] = useState<FiltroIndicador>(() => ({ hospital: 1, ...periodoPadrao() }));
  const [modal, setModal] = useState<{ id: string | null } | null>(null);
  const [ressalva, setRessalva] = useState<IndicadorResumo | null>(null);
  const [exportarAberto, setExportarAberto] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const unidades = useUnidadesIndicador();
  const lista = useListarIndicadores(aba ?? 'Adulto', filtro);
  const apurarAba = useApurarAba(aba ?? 'Adulto');
  const apurarUm = useApurarIndicador();

  const itens = useMemo(() => lista.data ?? [], [lista.data]);

  const unidadeNome = useMemo(
    () =>
      (unidades.data ?? []).find((u) => u.hospital === filtro.hospital)?.nome ??
      `Unidade ${filtro.hospital}`,
    [unidades.data, filtro.hospital],
  );

  const resumo = useMemo(
    () => ({
      total: itens.length,
      comMotor: itens.filter((i) => i.temMotor).length,
      validados: itens.filter((i) => i.situacao === 'Validado').length,
      apurados: itens.filter((i) => i.resultado?.valor != null).length,
    }),
    [itens],
  );

  if (!aba || !meta) {
    return <p className="p-6 text-sm text-slate-500">Aba de indicadores não encontrada.</p>;
  }

  async function apurarTudo() {
    setErro(null);
    try {
      await apurarAba.mutateAsync(filtro);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function apurarIndividual(id: string) {
    setErro(null);
    try {
      await apurarUm.mutateAsync({ id, filtro });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="p-6">
      <header className="mb-5 flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <BarChart3 className="h-3.5 w-3.5" />
            Indicadores contratuais · HMCML
          </div>
          <h1 className="mt-1 text-xl font-semibold text-slate-900">{meta.rotulo}</h1>
          <p className="mt-1 text-sm text-slate-500">
            {resumo.total} indicadores · {resumo.comMotor} com motor · {resumo.validados} validados ·{' '}
            {resumo.apurados} apurados no período
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button variante="outline" onClick={() => setExportarAberto(true)}>
            <FileSpreadsheet className="h-4 w-4" />
            Exportar
          </Button>
          {podeEditar && (
            <Button variante="secundaria" onClick={() => setModal({ id: null })}>
              <Plus className="h-4 w-4" />
              Novo indicador
            </Button>
          )}
        </div>
      </header>

      <FiltroIndicadores
        filtro={filtro}
        onChange={setFiltro}
        onApurar={apurarTudo}
        apurando={apurarAba.isPending}
      />

      {erro && (
        <p className="mb-4 flex items-start gap-2 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          {erro}
        </p>
      )}

      {lista.isLoading ? (
        <div className="flex items-center justify-center py-16 text-slate-400">
          <Loader2 className="h-6 w-6 animate-spin" />
        </div>
      ) : itens.length === 0 ? (
        <p className="rounded-xl border border-dashed border-slate-200 p-10 text-center text-sm text-slate-400">
          Nenhum indicador cadastrado nesta aba.
        </p>
      ) : (
        <TabelaIndicadores
          indicadores={itens}
          onEditar={(id) => setModal({ id })}
          onApurar={apurarIndividual}
          onVerRessalva={setRessalva}
          apurandoId={apurarUm.isPending ? (apurarUm.variables?.id ?? null) : null}
          podeEditar={podeEditar}
        />
      )}

      {ressalva && (
        <Modal
          aberto
          aoFechar={() => setRessalva(null)}
          titulo={`Ressalva · ${ressalva.numero} ${ressalva.nome}`}
          largura="md"
        >
          <p className="whitespace-pre-line text-sm leading-relaxed text-slate-700">
            {ressalva.ressalva}
          </p>
        </Modal>
      )}

      {exportarAberto && (
        <ModalExportar
          aberto
          aoFechar={() => setExportarAberto(false)}
          aba={aba}
          rotuloAba={meta.rotulo}
          filtro={filtro}
          unidadeNome={unidadeNome}
          itensAtual={itens}
        />
      )}

      {modal && (
        <ModalIndicador
          indicadorId={modal.id}
          aba={aba}
          filtro={filtro}
          podeEditar={podeEditar}
          agrupadores={itens.filter((i) => i.tipoResultado === 'Agrupador')}
          onFechar={() => setModal(null)}
        />
      )}
    </div>
  );
}
