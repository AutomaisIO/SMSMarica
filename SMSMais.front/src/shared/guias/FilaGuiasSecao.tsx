import { useEffect, useState } from 'react';
import { FileText, Loader2, PauseCircle, Save } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante } from '@/shared/lib/datas';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';

import { useFilaGuias, useSalvarFilaGuias, type SistemaFilaGuias } from './filaGuiasApi';

const NOME: Record<SistemaFilaGuias, string> = {
  Sisreg: 'SISREG',
  Ser: 'SER',
  Sernit: 'SERNIT',
  EsusSg: 'ESUS',
};

/** O que muda de um sistema para outro — o resto do quadro é igual nos quatro. */
const CUSTO: Record<SistemaFilaGuias, string> = {
  Sisreg:
    'Cada guia é uma leitura da ficha (1 a 2 requisições), do mesmo orçamento anti-robô dos outros motores do SISREG: a fila cede a eles, ao orçamento curto e, se o SISREG pedir CAPTCHA, para por 24 horas.',
  Ser: 'Cada guia é a chave de autorização do SER (2 requisições) e gasta uma via lá. A fila espera a varredura do SER terminar.',
  Sernit:
    'Cada guia é a chave de autorização do SERNIT (2 requisições) e gasta uma via lá. A fila espera a varredura do SERNIT terminar.',
  EsusSg:
    'Cada guia é a leitura do comprovante do ESUS (1 requisição) — não marca "comprovante impresso" lá. A fila espera a varredura do ESUS terminar.',
};

/**
 * A fila de guias de UM sistema de regulação (ADR-0076) — o mesmo quadro nas configurações do SISREG,
 * do SER, do SERNIT e do ESUS: ligar, quantas por hora, em que janela do dia, quanto falta e, se não
 * está andando, por quê. A guia vem uma vez e fica no acervo do paciente.
 */
export function FilaGuiasSecao({ sistema, podeEditar }: { sistema: SistemaFilaGuias; podeEditar: boolean }) {
  const fila = useFilaGuias(sistema);
  const salvar = useSalvarFilaGuias(sistema);

  const [habilitada, setHabilitada] = useState(true);
  const [porHora, setPorHora] = useState('60');
  const [inicio, setInicio] = useState('00:00');
  const [fim, setFim] = useState('00:00');
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (fila.data) {
      setHabilitada(fila.data.habilitada);
      setPorHora(String(fila.data.porHora));
      setInicio(fila.data.janelaInicio);
      setFim(fila.data.janelaFim);
    }
  }, [fila.data]);

  async function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    try {
      await salvar.mutateAsync({ habilitada, porHora: Number(porHora), janelaInicio: inicio, janelaFim: fim });
      setSalvo(true);
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  const f = fila.data;
  const nome = NOME[sistema];

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <header className="mb-4">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <FileText className="h-4 w-4 text-primary-600" />
          Guias dos agendamentos
          <AjudaManual artigo="guias-agendamento" secao="fila" />
        </h2>
        <p className="mt-1 max-w-3xl text-xs text-gray-600">
          Todo agendamento do {nome} entra numa fila; a guia é buscada <strong>uma vez</strong>, no
          ritmo abaixo, e fica no cadastro do paciente — na tela do pedido, em “Exames anexados” e no app
          do cidadão, junto do agendamento. O atendimento mais perto sai primeiro.
        </p>
        <p className="mt-1 max-w-3xl text-xs text-gray-500">{CUSTO[sistema]}</p>
      </header>

      {fila.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : f ? (
        <>
          <dl className="mb-4 grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
            <Numero rotulo="Na fila" valor={f.naFila} />
            <Numero rotulo="Trazidas hoje" valor={f.obtidasHoje} />
            <Numero rotulo="Não vieram" valor={f.falharam} destaque={f.falharam > 0} />
            <div>
              <dt className="text-xs text-gray-500">Última trazida</dt>
              <dd className="font-medium text-gray-900">{f.ultimaObtidaEm ? formatarInstante(f.ultimaObtidaEm) : '—'}</dd>
            </div>
          </dl>

          {f.impedimento ? (
            <p className="mb-4 flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
              <PauseCircle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>
                Parada agora: {f.impedimento}
                {f.pausadaAte ? <> Volta sozinha {formatarInstante(f.pausadaAte)}, ou ao salvar abaixo.</> : null}
              </span>
            </p>
          ) : f.naFila > 0 ? (
            <p className="mb-4 flex items-center gap-2 text-sm text-green-800">
              <Loader2 className="h-4 w-4 animate-spin" />
              Buscando, {f.porHora} por hora.
            </p>
          ) : null}
        </>
      ) : (
        <p className="mb-4 text-sm text-red-700">Não foi possível carregar a fila de guias.</p>
      )}

      <form onSubmit={aoSalvar} className="flex flex-wrap items-end gap-3 rounded-lg border border-slate-200 bg-slate-50 p-3">
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            className="size-4"
            checked={habilitada}
            onChange={(e) => setHabilitada(e.target.checked)}
            disabled={!podeEditar}
          />
          Buscar as guias automaticamente
        </label>
        <Campo label="Guias por hora" htmlFor={`guias-por-hora-${sistema}`} className="w-32">
          <Input
            id={`guias-por-hora-${sistema}`}
            type="number"
            min={1}
            max={3600}
            value={porHora}
            onChange={(e) => setPorHora(e.target.value)}
            disabled={!podeEditar}
          />
        </Campo>
        <Campo label="Das (Brasília)" htmlFor={`guias-inicio-${sistema}`} className="w-32">
          <Input
            id={`guias-inicio-${sistema}`}
            type="time"
            value={inicio}
            onChange={(e) => setInicio(e.target.value)}
            disabled={!podeEditar}
          />
        </Campo>
        <Campo label="Até" htmlFor={`guias-fim-${sistema}`} className="w-32">
          <Input
            id={`guias-fim-${sistema}`}
            type="time"
            value={fim}
            onChange={(e) => setFim(e.target.value)}
            disabled={!podeEditar}
          />
        </Campo>
        <Button type="submit" variante="secundaria" disabled={!podeEditar || salvar.isPending}>
          {salvar.isPending ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
          Salvar
        </Button>
        <p className="w-full text-xs text-slate-500">
          Início igual ao fim = o dia inteiro. Janela que vira o dia (22:00 até 06:00) vale. Salvar também tira a
          fila de uma pausa.
        </p>
        {erro ? <p className="w-full text-sm text-red-700">{erro}</p> : null}
        {salvo ? <p className="w-full text-sm text-green-700">Salvo.</p> : null}
      </form>
    </section>
  );
}

function Numero({ rotulo, valor, destaque }: { rotulo: string; valor: number; destaque?: boolean }) {
  return (
    <div>
      <dt className="text-xs text-gray-500">{rotulo}</dt>
      <dd className={`text-lg font-semibold ${destaque ? 'text-red-700' : 'text-gray-900'}`}>
        {valor.toLocaleString('pt-BR')}
      </dd>
    </div>
  );
}
