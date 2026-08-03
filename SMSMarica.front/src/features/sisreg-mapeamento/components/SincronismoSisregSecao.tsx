import { useEffect, useState } from 'react';
import { AlertTriangle, CalendarClock, Loader2, Play, Square } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  useCancelarVarredura,
  useExecutarVarredura,
  useSalvarVarreduraAgenda,
  useStatusVarredura,
  useVarreduraAgenda,
  useVarreduraExecucoes,
} from '@/features/sisreg-mapeamento/api/queries';
import type { StatusVarredura, VarreduraExecucao } from '@/features/sisreg-mapeamento/types';

type Props = { unidadeId: string; podeEditar: boolean };

const CLASSE_STATUS: Record<StatusVarredura, string> = {
  Pendente: 'bg-gray-100 text-gray-700',
  EmExecucao: 'bg-blue-50 text-blue-700',
  Concluida: 'bg-emerald-50 text-emerald-700',
  Parcial: 'bg-amber-50 text-amber-800',
  Erro: 'bg-red-50 text-red-700',
  Cancelada: 'bg-gray-100 text-gray-600',
};

const ROTULO_STATUS: Record<StatusVarredura, string> = {
  Pendente: 'Na fila',
  EmExecucao: 'Rodando',
  Concluida: 'Concluída',
  Parcial: 'Parcial',
  Erro: 'Erro',
  Cancelada: 'Cancelada',
};

/**
 * Sincronismo diário da agenda do SISREG para UMA unidade: liga/desliga, hora, janela de dias,
 * disparo manual e as varreduras recentes.
 *
 * O bloco existe para responder duas perguntas que o operador precisa fazer ANTES de confiar no
 * motor: quanto vai custar (requisições estimadas × teto) e o que está de fora (procedimentos
 * habilitados sem SIGTAP confirmado não são varridos).
 */
export function SincronismoSisregSecao({ unidadeId, podeEditar }: Props) {
  const agenda = useVarreduraAgenda(unidadeId);
  const salvar = useSalvarVarreduraAgenda(unidadeId);
  const executar = useExecutarVarredura(unidadeId);
  const cancelar = useCancelarVarredura(unidadeId);
  const status = useStatusVarredura(unidadeId);
  const execucoes = useVarreduraExecucoes(unidadeId);

  const [ativo, setAtivo] = useState(false);
  const [hora, setHora] = useState('04:30');
  const [dias, setDias] = useState('21');
  const [aviso, setAviso] = useState<{ tipo: 'ok' | 'erro'; texto: string } | null>(null);

  const dados = agenda.data;

  useEffect(() => {
    if (!dados) return;
    setAtivo(dados.ativo);
    setHora(dados.horaLocal.slice(0, 5));
    setDias(String(dados.diasAFrente));
  }, [dados?.unidadeId, dados?.ativo, dados?.horaLocal, dados?.diasAFrente]);

  const rodando = Boolean(status.data);
  const pausado = Boolean(dados?.pausadoAte && new Date(dados.pausadoAte) > new Date());

  async function comAviso(acao: () => Promise<unknown>, sucesso: (r: unknown) => string) {
    setAviso(null);
    try {
      const r = await acao();
      setAviso({ tipo: 'ok', texto: sucesso(r) });
    } catch (e) {
      setAviso({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
    }
  }

  if (agenda.isLoading) {
    return <p className="text-sm text-gray-500">Carregando sincronismo…</p>;
  }

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-4">
      <header className="mb-3">
        <h3 className="flex items-center gap-2 font-medium text-gray-900">
          <CalendarClock className="h-5 w-5 text-gray-500" />
          Sincronismo diário da agenda
        </h3>
        <p className="mt-1 text-sm text-gray-600">
          Todo dia, no horário escolhido, o sistema lê no SISREG a agenda desta unidade e cria as
          solicitações. Varre só os profissionais e procedimentos marcados acima.
        </p>
      </header>

      {!dados?.temCredencial && (
        <p className="mb-3 flex items-start gap-2 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          Esta unidade não tem credencial própria do SISREG. Cadastre acima antes de ligar o
          sincronismo — sem ela o motor usaria a credencial global, que enxerga outra unidade.
        </p>
      )}

      {/* Configuração */}
      <div className="flex flex-wrap items-end gap-4">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={ativo}
            disabled={!podeEditar}
            onChange={(e) => setAtivo(e.target.checked)}
          />
          Sincronizar a agenda todo dia
        </label>

        <Campo label="Hora (Brasília)" htmlFor="varredura-hora">
          <Input
            id="varredura-hora"
            type="time"
            className="w-32"
            value={hora}
            disabled={!podeEditar}
            onChange={(e) => setHora(e.target.value)}
          />
        </Campo>

        <Campo label="Dias à frente" htmlFor="varredura-dias">
          <Input
            id="varredura-dias"
            type="number"
            min={1}
            max={30}
            className="w-24"
            value={dias}
            disabled={!podeEditar}
            onChange={(e) => setDias(e.target.value)}
          />
        </Campo>

        {podeEditar && (
          <Button
            tamanho="sm"
            disabled={salvar.isPending}
            onClick={() =>
              comAviso(
                () =>
                  salvar.mutateAsync({
                    ativo,
                    horaLocal: hora,
                    diasAFrente: Number(dias) || 21,
                  }),
                () => 'Sincronismo salvo.',
              )
            }
          >
            {salvar.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            Salvar
          </Button>
        )}
      </div>

      <p className="mt-2 text-xs text-gray-500">
        Só é possível rodar entre {dados?.janelaInicioLocal?.slice(0, 5)} e{' '}
        {dados?.janelaFimLocal?.slice(0, 5)}: o SISREG aceita <strong>uma sessão por operador</strong>,
        então varrer no expediente derrubaria quem estivesse atendendo por esta unidade.
      </p>

      {/* Estado */}
      <dl className="mt-4 grid grid-cols-2 gap-3 border-t border-gray-100 pt-3 text-sm md:grid-cols-4">
        <div>
          <dt className="text-xs text-gray-500">Próxima varredura</dt>
          <dd className={pausado ? 'font-medium text-red-600' : 'text-gray-900'}>
            {pausado
              ? `pausada até ${dataHora(dados!.pausadoAte)}`
              : dados?.ativo
                ? dataHora(dados.proximoRunEm)
                : 'desligada'}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-gray-500">Última varredura</dt>
          <dd className="text-gray-900">{dataHora(dados?.ultimaExecucaoEm)}</dd>
        </div>
        <div>
          <dt className="text-xs text-gray-500">Combinações prontas</dt>
          <dd className="text-gray-900">{dados?.combinacoesProntas ?? 0}</dd>
        </div>
        <div>
          <dt className="text-xs text-gray-500">Requisições estimadas</dt>
          <dd
            className={
              (dados?.requisicoesEstimadas ?? 0) > (dados?.tetoPorExecucao ?? 0)
                ? 'font-medium text-amber-700'
                : 'text-gray-900'
            }
          >
            ≈ {dados?.requisicoesEstimadas ?? 0} de {dados?.tetoPorExecucao ?? 0}
          </dd>
        </div>
      </dl>

      {(dados?.falhasConsecutivas ?? 0) > 0 && (
        <p className="mt-2 text-sm text-amber-700">
          {dados!.falhasConsecutivas} falhas consecutivas — confira a credencial da unidade.
        </p>
      )}

      {/* Execução */}
      <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-gray-100 pt-3">
        {rodando ? (
          <>
            <span className="flex items-center gap-2 text-sm text-blue-700">
              <Loader2 className="h-4 w-4 animate-spin" />
              {status.data!.combinacoesFeitas}/{status.data!.combinacoesTotal} combinações ·{' '}
              {status.data!.requisicoes} requisições · {status.data!.validos} importadas
              {status.data!.procedimentoAtual ? ` · ${status.data!.procedimentoAtual}` : ''}
            </span>
            {podeEditar && (
              <Button
                variante="ghost"
                tamanho="sm"
                disabled={cancelar.isPending}
                onClick={() => comAviso(() => cancelar.mutateAsync(), () => 'Varredura interrompida.')}
              >
                <Square className="h-4 w-4" />
                Parar
              </Button>
            )}
          </>
        ) : (
          podeEditar && (
            <Button
              variante="outline"
              tamanho="sm"
              disabled={executar.isPending}
              onClick={() =>
                comAviso(
                  () => executar.mutateAsync(),
                  (r) => (r as { mensagem: string }).mensagem,
                )
              }
            >
              {executar.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Play className="h-4 w-4" />
              )}
              Sincronizar agora
            </Button>
          )
        )}
      </div>

      {aviso && (
        <p
          className={`mt-3 rounded-md border p-3 text-sm ${
            aviso.tipo === 'ok'
              ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
              : 'border-red-200 bg-red-50 text-red-700'
          }`}
        >
          {aviso.texto}
        </p>
      )}

      {/* Histórico */}
      {(execucoes.data?.length ?? 0) > 0 && (
        <div className="mt-4 border-t border-gray-100 pt-3">
          <h4 className="mb-2 text-sm font-medium text-gray-700">Varreduras recentes</h4>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-gray-500">
                  <th className="py-1 pr-3 font-medium">Quando</th>
                  <th className="py-1 pr-3 font-medium">Disparo</th>
                  <th className="py-1 pr-3 font-medium">Status</th>
                  <th className="py-1 pr-3 font-medium">Cobertura</th>
                  <th className="py-1 pr-3 font-medium">Req.</th>
                  <th className="py-1 pr-3 font-medium">Importadas</th>
                  <th className="py-1 pr-3 font-medium">Pendências</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {execucoes.data!.map((e) => (
                  <LinhaExecucao key={e.id} execucao={e} />
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  );
}

function LinhaExecucao({ execucao: e }: { execucao: VarreduraExecucao }) {
  return (
    <tr className="text-gray-700">
      <td className="py-1.5 pr-3 whitespace-nowrap">{dataHora(e.iniciadoEm)}</td>
      <td className="py-1.5 pr-3">{e.disparo === 'Agendado' ? '⏱ Agendado' : 'Manual'}</td>
      <td className="py-1.5 pr-3">
        <span className={`rounded px-1.5 py-0.5 text-xs ${CLASSE_STATUS[e.status]}`}>
          {ROTULO_STATUS[e.status]}
        </span>
        {/* Parcial precisa dizer o porquê: senão o operador conclui que o dia está importado. */}
        {e.status === 'Parcial' && e.mensagemErro && (
          <span className="ml-1 text-xs text-amber-700" title={e.mensagemErro}>
            (incompleta)
          </span>
        )}
      </td>
      <td className="py-1.5 pr-3 whitespace-nowrap">
        {e.combinacoesFeitas}/{e.combinacoesTotal}
      </td>
      <td className="py-1.5 pr-3">{e.requisicoes}</td>
      <td className="py-1.5 pr-3">{e.validos}</td>
      <td className="py-1.5 pr-3">{e.invalidos > 0 ? e.invalidos : '—'}</td>
    </tr>
  );
}

function dataHora(iso: string | null | undefined) {
  return iso ? new Date(iso).toLocaleString('pt-BR') : '—';
}
