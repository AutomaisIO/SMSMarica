import { useEffect, useState } from 'react';
import { Loader2, Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { useConfiguracaoRobo, useSalvarConfiguracaoRobo } from '@/features/robo-atendimento/api/queries';
import type { RoboConfiguracao } from '@/features/robo-atendimento/types';

const CAMPO_CLASSE =
  'w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-primary-500 focus:outline-none focus:ring-1 focus:ring-primary-500';

export function ConfiguracaoRoboCard() {
  const consulta = useConfiguracaoRobo();
  const salvar = useSalvarConfiguracaoRobo();
  const [form, setForm] = useState<RoboConfiguracao | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [ok, setOk] = useState(false);

  useEffect(() => {
    if (consulta.data) setForm(consulta.data);
  }, [consulta.data]);

  if (!form) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-gray-200 bg-white p-4 text-sm text-gray-500">
        <Loader2 className="h-4 w-4 animate-spin" /> Carregando configuração…
      </div>
    );
  }

  function set<K extends keyof RoboConfiguracao>(chave: K, valor: RoboConfiguracao[K]) {
    setForm((f) => (f ? { ...f, [chave]: valor } : f));
    setOk(false);
  }

  function aoSalvar(ev: React.FormEvent) {
    ev.preventDefault();
    if (!form) return;
    setErro(null);
    setOk(false);
    salvar.mutate(
      {
        ...form,
        mensagemHandOff: form.mensagemHandOff?.trim() || null,
        mensagemForaHorario: form.mensagemForaHorario?.trim() || null,
      },
      {
        onSuccess: () => setOk(true),
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  return (
    <form onSubmit={aoSalvar} className="space-y-4 rounded-lg border border-gray-200 bg-white p-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-gray-900">Configuração global do robô</h2>
        <label className="flex items-center gap-2 text-sm font-medium text-gray-700">
          <input type="checkbox" checked={form.ativo} onChange={(e) => set('ativo', e.target.checked)} />
          Robô ligado
        </label>
      </div>

      <Campo label="Persona base do robô" htmlFor="cfg-persona"
        dica="Vale para todos os assuntos. Ele é um atendente virtual, mas só assume isso se perguntarem.">
        <textarea
          id="cfg-persona"
          className={CAMPO_CLASSE}
          rows={4}
          value={form.personaGlobal}
          onChange={(e) => set('personaGlobal', e.target.value)}
        />
      </Campo>

      <div className="grid gap-4 sm:grid-cols-2">
        <Campo label="Modelo padrão" htmlFor="cfg-modelo" dica="Usado quando o assunto não define o seu.">
          <Input id="cfg-modelo" value={form.modeloPadrao} onChange={(e) => set('modeloPadrao', e.target.value)} />
        </Campo>
        <Campo label="Nome de exibição" htmlFor="cfg-nome">
          <Input id="cfg-nome" value={form.nomeExibicao} onChange={(e) => set('nomeExibicao', e.target.value)} />
        </Campo>
      </div>

      <Campo label="Mensagem ao passar para atendente" htmlFor="cfg-handoff" dica="Opcional.">
        <textarea
          id="cfg-handoff"
          className={CAMPO_CLASSE}
          rows={2}
          value={form.mensagemHandOff ?? ''}
          onChange={(e) => set('mensagemHandOff', e.target.value)}
        />
      </Campo>

      <Campo label="Mensagem fora do horário (sem resolver)" htmlFor="cfg-fora" dica="Opcional.">
        <textarea
          id="cfg-fora"
          className={CAMPO_CLASSE}
          rows={2}
          value={form.mensagemForaHorario ?? ''}
          onChange={(e) => set('mensagemForaHorario', e.target.value)}
        />
      </Campo>

      <div className="grid gap-4 sm:grid-cols-2">
        <Campo label="Início do expediente dos atendentes" htmlFor="cfg-exp-inicio"
          dica="Antes deste horário (Brasília) o robô assume mesmo com atendente na sessão. Vazio = sem limite de manhã.">
          <Input
            id="cfg-exp-inicio"
            type="time"
            value={(form.horaAtendimentoHumanoInicio ?? '').slice(0, 5)}
            onChange={(e) => set('horaAtendimentoHumanoInicio', e.target.value || null)}
          />
        </Campo>
        <Campo label="Fim do expediente dos atendentes" htmlFor="cfg-exp-fim"
          dica="A partir deste horário (Brasília) o robô assume mesmo com atendente na sessão aberta. Vazio = sem limite de fim.">
          <Input
            id="cfg-exp-fim"
            type="time"
            value={(form.horaAtendimentoHumanoFim ?? '').slice(0, 5)}
            onChange={(e) => set('horaAtendimentoHumanoFim', e.target.value || null)}
          />
        </Campo>
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}
      {ok ? <p className="text-sm text-green-600">Configuração salva.</p> : null}

      <div className="flex justify-end">
        <Button type="submit" disabled={salvar.isPending}>
          {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          Salvar configuração
        </Button>
      </div>
    </form>
  );
}
