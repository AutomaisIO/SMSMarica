import { useEffect, useState } from 'react';
import { Calculator, Loader2, Settings2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  useConfigFaturamento,
  useRegistrosFaturamento,
  useResumoFaturamento,
  useSalvarConfigFaturamento,
} from '@/features/faturamento/api';
import type { DimensaoFaturamento } from '@/features/faturamento/types';

const DIMENSOES: { id: DimensaoFaturamento; rotulo: string }[] = [
  { id: 'Paciente', rotulo: 'Paciente' },
  { id: 'Motorista', rotulo: 'Motorista' },
  { id: 'Veiculo', rotulo: 'Veículo' },
  { id: 'TipoTratamento', rotulo: 'Tipo de tratamento' },
  { id: 'Unidade', rotulo: 'Unidade' },
];

const STATUS_FAT: Record<string, { rotulo: string; cor: string }> = {
  Pendente: { rotulo: 'Pendente', cor: 'bg-gray-100 text-gray-700' },
  EmBpa: { rotulo: 'Em BPA', cor: 'bg-blue-50 text-blue-700' },
  Faturado: { rotulo: 'Faturado', cor: 'bg-emerald-50 text-emerald-700' },
  Cancelado: { rotulo: 'Cancelado', cor: 'bg-red-50 text-red-700' },
};

const brl = (v: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v);
const num = (v: number, casas = 2) => v.toFixed(casas).replace('.', ',');

function mesAtual(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
}

function competenciaInt(mes: string): number | undefined {
  const m = mes.match(/^(\d{4})-(\d{2})$/);
  return m ? Number(`${m[1]}${m[2]}`) : undefined;
}

function dataBr(iso: string): string {
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}` : iso;
}

export function FaturamentoPage() {
  const podeEditar = usePermissao('Faturamento', 'Edicao');
  const [mes, setMes] = useState(mesAtual());
  const competencia = competenciaInt(mes);
  const [dimensao, setDimensao] = useState<DimensaoFaturamento>('Paciente');

  const resumo = useResumoFaturamento(dimensao, competencia);
  const registros = useRegistrosFaturamento(competencia);

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <Calculator className="h-6 w-6 text-primary-600" />
            Faturamento (TFD / SUS)
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            Unidades de faturamento por transporte realizado (km com paciente a bordo ÷ km por unidade,
            proporcional). Relatórios por dimensão e competência.
          </p>
        </div>
        <Campo label="Competência" htmlFor="ft-mes">
          <Input id="ft-mes" type="month" value={mes} onChange={(e) => setMes(e.target.value)} />
        </Campo>
      </header>

      <ConfigCard podeEditar={podeEditar} />

      {/* Resumo por dimensão */}
      <section className="space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm font-semibold text-gray-700">Resumo por:</span>
          {DIMENSOES.map((d) => (
            <button
              key={d.id}
              type="button"
              onClick={() => setDimensao(d.id)}
              className={`rounded-full border px-3 py-1 text-sm ${
                dimensao === d.id
                  ? 'border-primary-300 bg-primary-50 text-primary-700'
                  : 'border-gray-200 text-gray-600 hover:bg-gray-50'
              }`}
            >
              {d.rotulo}
            </button>
          ))}
        </div>

        {resumo.isError ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {extrairMensagemDeErro(resumo.error)}
          </div>
        ) : null}

        <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500">
              <tr>
                <th className="px-4 py-2">{DIMENSOES.find((d) => d.id === dimensao)?.rotulo}</th>
                <th className="px-4 py-2 text-right">Registros</th>
                <th className="px-4 py-2 text-right">Km</th>
                <th className="px-4 py-2 text-right">Unidades</th>
                <th className="px-4 py-2 text-right">Valor</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {resumo.isLoading ? (
                <tr><td colSpan={5} className="px-4 py-6 text-center text-gray-400"><Loader2 className="mx-auto h-4 w-4 animate-spin" /></td></tr>
              ) : (resumo.data?.itens.length ?? 0) === 0 ? (
                <tr><td colSpan={5} className="px-4 py-6 text-center text-gray-400">Nenhum registro nesta competência.</td></tr>
              ) : (
                resumo.data!.itens.map((i) => (
                  <tr key={i.chaveId || i.descricao}>
                    <td className="px-4 py-2 text-gray-900">{i.descricao}</td>
                    <td className="px-4 py-2 text-right text-gray-600">{i.qtdRegistros}</td>
                    <td className="px-4 py-2 text-right text-gray-600">{num(i.totalKm, 1)}</td>
                    <td className="px-4 py-2 text-right text-gray-600">{num(i.totalUnidades)}</td>
                    <td className="px-4 py-2 text-right font-medium text-gray-900">{brl(i.totalValor)}</td>
                  </tr>
                ))
              )}
            </tbody>
            {resumo.data && resumo.data.itens.length > 0 ? (
              <tfoot className="border-t border-gray-200 bg-gray-50 font-semibold text-gray-900">
                <tr>
                  <td className="px-4 py-2" colSpan={3}>Total</td>
                  <td className="px-4 py-2 text-right">{num(resumo.data.totalGeralUnidades)}</td>
                  <td className="px-4 py-2 text-right">{brl(resumo.data.totalGeralValor)}</td>
                </tr>
              </tfoot>
            ) : null}
          </table>
        </div>
      </section>

      {/* Registros detalhados */}
      <section className="space-y-3">
        <h2 className="text-sm font-semibold text-gray-700">Registros da competência</h2>
        <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500">
              <tr>
                <th className="px-4 py-2">Data</th>
                <th className="px-4 py-2">Paciente</th>
                <th className="px-4 py-2">Unidade</th>
                <th className="px-4 py-2 text-right">Km</th>
                <th className="px-4 py-2 text-right">Unid.</th>
                <th className="px-4 py-2 text-right">Valor</th>
                <th className="px-4 py-2">SIGTAP</th>
                <th className="px-4 py-2">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {registros.isLoading ? (
                <tr><td colSpan={8} className="px-4 py-6 text-center text-gray-400"><Loader2 className="mx-auto h-4 w-4 animate-spin" /></td></tr>
              ) : (registros.data?.length ?? 0) === 0 ? (
                <tr><td colSpan={8} className="px-4 py-6 text-center text-gray-400">Sem registros. Eles nascem ao confirmar sessões realizadas.</td></tr>
              ) : (
                registros.data!.map((r) => {
                  const st = STATUS_FAT[r.status] ?? { rotulo: r.status, cor: 'bg-gray-100 text-gray-700' };
                  return (
                    <tr key={r.id}>
                      <td className="px-4 py-2 text-gray-600">{dataBr(r.data)}</td>
                      <td className="px-4 py-2 text-gray-900">{r.pacienteNome}</td>
                      <td className="px-4 py-2 text-gray-600">{r.unidadeNome}</td>
                      <td className="px-4 py-2 text-right text-gray-600">{num(r.kmComPaciente, 1)}</td>
                      <td className="px-4 py-2 text-right text-gray-600">{num(r.unidades)}</td>
                      <td className="px-4 py-2 text-right font-medium text-gray-900">{brl(r.valorTotal)}</td>
                      <td className="px-4 py-2 text-gray-500">{r.codigoSigtap ?? '—'}</td>
                      <td className="px-4 py-2">
                        <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${st.cor}`}>{st.rotulo}</span>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

function ConfigCard({ podeEditar }: { podeEditar: boolean }) {
  const config = useConfigFaturamento();
  const salvar = useSalvarConfigFaturamento();

  const [valor, setValor] = useState('');
  const [kmPorUnidade, setKmPorUnidade] = useState('50');
  const [codigoSigtap, setCodigoSigtap] = useState('');
  const [ativo, setAtivo] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (config.data) {
      setValor(String(config.data.valorPor50Km));
      setKmPorUnidade(String(config.data.kmPorUnidade));
      setCodigoSigtap(config.data.codigoSigtap ?? '');
      setAtivo(config.data.ativo);
    }
  }, [config.data]);

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    salvar.mutate(
      {
        valorPor50Km: Number(valor.replace(',', '.')) || 0,
        kmPorUnidade: Number(kmPorUnidade) || 50,
        codigoSigtap: codigoSigtap.trim() || null,
        ativo,
      },
      {
        onSuccess: () => setSalvo(true),
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-700">
        <Settings2 className="h-4 w-4 text-primary-600" />
        Regra de faturamento
      </h2>
      <form onSubmit={aoSalvar} className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Campo label="Valor por unidade (R$)" htmlFor="ft-valor" dica="Valor de 1 unidade (a cada N km).">
          <Input id="ft-valor" value={valor} onChange={(e) => setValor(e.target.value)} placeholder="0,00" disabled={!podeEditar} />
        </Campo>
        <Campo label="Km por unidade" htmlFor="ft-km" dica="Ex.: 50 — 1 unidade a cada 50 km (proporcional).">
          <Input id="ft-km" type="number" value={kmPorUnidade} onChange={(e) => setKmPorUnidade(e.target.value)} disabled={!podeEditar} />
        </Campo>
        <Campo label="Código SIGTAP" htmlFor="ft-sigtap" dica="Procedimento de transporte.">
          <Input id="ft-sigtap" value={codigoSigtap} onChange={(e) => setCodigoSigtap(e.target.value)} placeholder="0000000000" disabled={!podeEditar} />
        </Campo>

        <label className="flex items-center gap-2 text-sm text-gray-700 sm:col-span-3">
          <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} disabled={!podeEditar} />
          Contabilização ativa (registros nascem ao confirmar sessões realizadas)
        </label>

        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 sm:col-span-3">{erro}</div>
        ) : null}

        {podeEditar ? (
          <div className="flex items-center justify-end gap-3 sm:col-span-3">
            {salvo ? <span className="text-sm text-green-600">Salvo.</span> : null}
            <Button type="submit" disabled={salvar.isPending || config.isPending}>
              {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Salvar regra
            </Button>
          </div>
        ) : null}
      </form>
    </section>
  );
}
