import { useEffect, useState } from 'react';
import { Loader2, RefreshCw, RotateCw, XCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  useAgendamentoMapeamentoLote,
  useCancelarMapeamentoLote,
  useSalvarAgendamentoMapeamentoLote,
  useSincronizarMapeamentoLote,
  useStatusMapeamentoLote,
} from '@/features/sisreg/api/queries';

/**
 * "SISREG Sincroniza tudo" (#118): dispara, de uma vez, a atualização de profissionais e
 * procedimentos (+ vínculo FHIR) de TODAS as unidades configuradas — sem ir unidade por unidade —
 * e configura o disparo automático diário. Roda no servidor, em sequência, respeitando o limite de
 * requisições do SISREG; o progresso abaixo acompanha a execução.
 */
export function SincronizarTudoSecao() {
  const status = useStatusMapeamentoLote();
  const agendamento = useAgendamentoMapeamentoLote();
  const sincronizar = useSincronizarMapeamentoLote();
  const cancelar = useCancelarMapeamentoLote();
  const salvar = useSalvarAgendamentoMapeamentoLote();

  const [ativo, setAtivo] = useState(false);
  const [hora, setHora] = useState('03:30');
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [agendaSalva, setAgendaSalva] = useState(false);

  useEffect(() => {
    if (agendamento.data) {
      setAtivo(agendamento.data.ativo);
      setHora(agendamento.data.horaLocal);
    }
  }, [agendamento.data]);

  const emExecucao = status.data?.emExecucao ?? false;

  async function aoSincronizar() {
    setErro(null);
    setAviso(null);
    try {
      const r = await sincronizar.mutateAsync();
      setAviso(r.mensagem);
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  async function aoCancelar() {
    setErro(null);
    try {
      await cancelar.mutateAsync();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  async function aoSalvarAgenda(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setAgendaSalva(false);
    try {
      await salvar.mutateAsync({ ativo, horaLocal: hora });
      setAgendaSalva(true);
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  const s = status.data;
  const pct =
    s && s.unidadesTotal > 0 ? Math.round((s.unidadesFeitas / s.unidadesTotal) * 100) : 0;

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <header className="mb-4">
        <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
          <RefreshCw className="h-5 w-5 text-primary-600" />
          Sincronizar profissionais e procedimentos (todas as unidades)
        </h2>
        <p className="mt-1 text-sm text-gray-600">
          Atualiza de uma vez o mapeamento de médicos e procedimentos de todas as unidades já
          configuradas, sem precisar entrar em cada uma. Roda no servidor, em sequência, respeitando
          o limite de acessos do SISREG — pode levar alguns minutos.
        </p>
      </header>

      {erro ? (
        <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      {aviso && !emExecucao ? (
        <div className="mb-3 rounded-md border border-blue-200 bg-blue-50 px-3 py-2 text-sm text-blue-700">
          {aviso}
        </div>
      ) : null}

      {/* Progresso do lote em curso */}
      {s && emExecucao ? (
        <div className="mb-4 rounded-lg border border-gray-200 bg-gray-50 p-4">
          <div className="mb-2 flex items-center justify-between text-sm text-gray-700">
            <span className="flex items-center gap-2">
              <Loader2 className="h-4 w-4 animate-spin text-primary-600" />
              Sincronizando… {s.unidadesFeitas}/{s.unidadesTotal} unidades
              {s.unidadeAtual ? ` — ${s.unidadeAtual}` : ''}
            </span>
            <span className="tabular-nums text-gray-500">{s.requisicoesFeitas} requisições</span>
          </div>
          <div className="h-2 w-full overflow-hidden rounded-full bg-gray-200">
            <div className="h-full rounded-full bg-primary-600 transition-all" style={{ width: `${pct}%` }} />
          </div>
          <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-500">
            <span>{s.profissionaisNovos} profissionais novos</span>
            <span>{s.practitionersCriados} criados no hub / {s.practitionersVinculados} vinculados</span>
            {s.unidadesComErro > 0 ? (
              <span className="text-amber-600">{s.unidadesComErro} unidade(s) com erro</span>
            ) : null}
          </div>
          {s.ultimoErro ? (
            <p className="mt-2 text-xs text-amber-700">{s.ultimoErro}</p>
          ) : null}
          <div className="mt-3">
            <Button type="button" variante="outline" onClick={aoCancelar} disabled={cancelar.isPending}>
              <XCircle className="mr-2 h-4 w-4" />
              Cancelar
            </Button>
          </div>
        </div>
      ) : (
        <div className="mb-4">
          <Button type="button" onClick={aoSincronizar} disabled={sincronizar.isPending}>
            {sincronizar.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <RotateCw className="mr-2 h-4 w-4" />
            )}
            Sincronizar tudo agora
          </Button>
        </div>
      )}

      {/* Agendamento diário */}
      <form onSubmit={aoSalvarAgenda} className="border-t border-gray-100 pt-4">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} />
          Sincronizar automaticamente todo dia
        </label>

        <div className="mt-3 flex flex-wrap items-end gap-3">
          <Campo label="Hora (Brasília)" htmlFor="lote-hora" dica="Recomendado de madrugada (ex.: 03:30).">
            <Input
              id="lote-hora"
              type="time"
              value={hora}
              onChange={(e) => setHora(e.target.value)}
              disabled={!ativo}
              className="w-32"
            />
          </Campo>
          <Button type="submit" variante="outline" disabled={salvar.isPending || agendamento.isPending}>
            {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Salvar agendamento
          </Button>
          {agendaSalva ? <span className="pb-2 text-sm text-green-600">Agendamento salvo.</span> : null}
        </div>
      </form>
    </section>
  );
}
