import { useEffect, useState } from 'react';
import { DownloadCloud, ListOrdered, Loader2, RotateCw } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  useAgendamentoFila,
  useCarregarFila,
  useSalvarAgendamentoFila,
  useStatusFilaConfig,
} from '@/features/sisreg/api/queries';

function dataHora(iso: string | null) {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('pt-BR', {
    timeZone: 'America/Sao_Paulo',
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function dataCurta(iso: string | null) {
  if (!iso) return '—';
  const [a, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

/**
 * Leitura da FILA DE ESPERA do SISREG — quem pediu e ainda não foi agendado.
 *
 * <p>Relida inteira todo dia, na hora configurada (padrão 03:00): ler só "daqui para frente" deixa
 * passar quem foi cancelado ou devolvido, pedido antigo reenviado (volta com a data original) e
 * troca de risco ou de procedimento. Quem vira agendamento sai antes, sozinho, pela agenda
 * importada — sem requisição.</p>
 *
 * <p>Os botões ficam aqui, e não em Ofertas, porque disparam requisições ao SISREG com o operador
 * da integração: é decisão de quem cuida da integração, não de quem está regulando.</p>
 */
export function FilaEsperaSecao() {
  const status = useStatusFilaConfig();
  const agendamento = useAgendamentoFila();
  const salvar = useSalvarAgendamentoFila();
  const carregar = useCarregarFila();

  const [ativo, setAtivo] = useState(true);
  const [hora, setHora] = useState('03:00');
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (agendamento.data) {
      setAtivo(agendamento.data.ativo);
      setHora(agendamento.data.horaLocal);
    }
  }, [agendamento.data]);

  const s = status.data;

  function pedir(completa: boolean) {
    setErro(null);
    carregar.mutate(completa, { onError: (e) => setErro(extrairMensagemDeErro(e)) });
  }

  async function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    try {
      await salvar.mutateAsync({ ativo, horaLocal: hora });
      setSalvo(true);
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <header className="mb-4">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <ListOrdered className="h-4 w-4 text-primary-600" />
          Fila de espera
        </h2>
        <p className="mt-1 max-w-3xl text-xs text-gray-600">
          Quem pediu no SISREG e ainda <strong>não foi agendado</strong> — a lista que a tela de
          Ofertas mostra em “Quem espera”. É relida <strong>inteira</strong> todo dia: só assim
          aparecem os cancelados e devolvidos, os pedidos antigos reenviados e as trocas de risco ou
          de procedimento. Quem vira agendamento sai da fila sozinho, sem requisição.
        </p>
        <p className="mt-1 max-w-3xl text-xs text-gray-500">
          A releitura completa custa cerca de 88 requisições (um mês de pedidos por vez, desde
          jan/2023) e leva uns 20 minutos, intercalada com os outros motores.
        </p>
      </header>

      {s?.emExecucao ? (
        <div className="mb-4 rounded-lg border border-blue-200 bg-blue-50 p-3 text-sm text-blue-800">
          <p className="flex items-center gap-2 font-medium">
            <Loader2 className="h-4 w-4 animate-spin" />
            Lendo a fila: {s.janelasLidas} de {s.janelasTotal} período(s)
          </p>
          <p className="mt-1 text-xs">
            {s.janelaAtualInicio
              ? `Agora: pedidos de ${dataCurta(s.janelaAtualInicio)} a ${dataCurta(s.janelaAtualFim)}. `
              : ''}
            {s.pessoasLidas.toLocaleString('pt-BR')} pessoa(s) lidas até aqui.
          </p>
        </div>
      ) : s ? (
        <p className="mb-4 rounded-lg border border-gray-200 bg-gray-50 p-3 text-xs text-gray-700">
          {s.ultimaLeitura ? (
            <>
              Última leitura em <strong>{dataHora(s.ultimaLeitura)}</strong> —{' '}
              <strong>{s.pessoasNaFila.toLocaleString('pt-BR')}</strong> pessoa(s) esperando na rede
              toda.
            </>
          ) : (
            <strong>A fila ainda não foi lida do SISREG.</strong>
          )}
        </p>
      ) : null}

      {s && s.relidasAposFalha > 0 && !s.ultimoErro ? (
        <p className="mb-3 text-xs text-gray-500">
          {s.relidasAposFalha} período(s) precisaram ser relidos (a sessão do SISREG caiu ou cortou a
          conexão) — resolvido na nova tentativa.
        </p>
      ) : null}
      {s?.ultimoErro ? (
        <p className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          Leitura interrompida em {dataHora(s.ultimoErroEm)}: {s.ultimoErro}
        </p>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        <Button disabled={carregar.isPending || s?.emExecucao} onClick={() => pedir(true)}>
          {carregar.isPending ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <DownloadCloud className="mr-2 h-4 w-4" />
          )}
          Reler a fila inteira agora
        </Button>
        <Button
          variante="outline"
          disabled={carregar.isPending || s?.emExecucao}
          onClick={() => pedir(false)}
        >
          <RotateCw className="mr-2 h-4 w-4" />
          Só os últimos 31 dias
        </Button>
        <span className="text-xs text-gray-500">Últimos 31 dias custa 2 requisições.</span>
      </div>

      <form onSubmit={aoSalvar} className="mt-4 border-t border-gray-100 pt-4">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} />
          Reler a fila inteira todo dia
        </label>

        <div className="mt-3 flex flex-wrap items-end gap-3">
          <Campo
            label="Horário (Brasília)"
            htmlFor="fila-hora"
            dica="De madrugada: a sessão do SISREG é única por operador, e 20 minutos de leitura no expediente disputam com quem está atendendo. Se outro motor estiver usando a sessão na hora, a leitura espera e começa em até 2 horas."
          >
            <Input
              id="fila-hora"
              type="time"
              className="w-32"
              value={hora}
              onChange={(e) => setHora(e.target.value)}
            />
          </Campo>
          <Button type="submit" variante="outline" className="mb-0.5" disabled={salvar.isPending}>
            {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Salvar horário
          </Button>
          {salvo ? <span className="mb-2 text-xs text-green-600">Horário salvo.</span> : null}
        </div>

        <p className="mt-2 text-xs text-gray-500">
          O interruptor de sincronismo automático no topo da tela vale também para esta releitura:
          desligado lá, ela não roda sozinha. Os botões acima rodam mesmo assim.
        </p>
      </form>

      {erro ? (
        <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          {erro}
        </p>
      ) : null}
    </section>
  );
}
