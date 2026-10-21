import { useState } from 'react';
import { Link } from 'react-router-dom';
import { BellRing, Loader2, Save } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import {
  type SistemaComAvisos,
  useAvisosPaciente,
  useSalvarAvisosPaciente,
} from '@/features/regulacao/api/avisosPacienteApi';

const ROTULO: Record<SistemaComAvisos, string> = {
  sisreg: 'SISREG',
  ser: 'SER',
  sernit: 'SERNIT',
  esussg: 'ESUS',
};

type Rascunho = { lembreteHabilitado: boolean; lembreteDiasAntes: string; avisoCancelamentoHabilitado: boolean };

/**
 * "Avisos ao paciente" na tela de Configuração de cada regulação (21/10/2026): o lembrete X dias
 * antes e o aviso de cancelamento. SER, SERNIT e ESUS têm chaves próprias; o SISREG mostra as mesmas
 * da Mensageria → Regras (um valor só, visto dos dois lugares). O aviso do agendamento em si é ligado
 * em outro lugar, e aqui só aparece o estado dele.
 */
export function AvisosPacienteSecao({ sistema, podeEditar }: { sistema: SistemaComAvisos; podeEditar: boolean }) {
  const { data, isLoading, isError, error } = useAvisosPaciente(sistema);
  const salvar = useSalvarAvisosPaciente(sistema);
  const [rascunho, setRascunho] = useState<Rascunho | null>(null);

  const rotulo = ROTULO[sistema];
  const externo = sistema !== 'sisreg';

  if (isLoading) {
    return (
      <section className="rounded-lg border border-slate-200 bg-white p-4 text-sm text-slate-500">
        <Loader2 className="mr-2 inline size-4 animate-spin" /> Carregando os avisos ao paciente…
      </section>
    );
  }
  if (isError || !data) {
    return (
      <section className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
        Não foi possível carregar os avisos ao paciente: {extrairMensagemDeErro(error)}
      </section>
    );
  }

  const atual: Rascunho = rascunho ?? {
    lembreteHabilitado: data.lembreteHabilitado,
    lembreteDiasAntes: String(data.lembreteDiasAntes),
    avisoCancelamentoHabilitado: data.avisoCancelamentoHabilitado,
  };
  const dias = Number.parseInt(atual.lembreteDiasAntes, 10);
  const diasValidos = Number.isInteger(dias) && dias >= 1 && dias <= 30;
  const alterar = (parcial: Partial<Rascunho>) => {
    salvar.reset();
    setRascunho({ ...atual, ...parcial });
  };

  function aoSalvar() {
    if (!diasValidos) return;
    // O erro aparece ao lado do botão (salvar.isError); o rascunho fica para a pessoa corrigir.
    salvar.mutate(
      {
        lembreteHabilitado: atual.lembreteHabilitado,
        lembreteDiasAntes: dias,
        avisoCancelamentoHabilitado: atual.avisoCancelamentoHabilitado,
      },
      { onSuccess: () => setRascunho(null) },
    );
  }

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <h3 className="mb-1 flex items-center gap-2 text-sm font-semibold">
        <BellRing className="size-4 text-red-600" /> Avisos ao paciente
      </h3>
      <p className="mb-3 text-xs text-slate-500">
        O que o paciente recebe pelo WhatsApp sobre os agendamentos do {rotulo}, além do aviso de que foi agendado.
      </p>

      <p className="mb-3 rounded bg-slate-50 p-2 text-xs text-slate-600">
        {externo ? (
          <>
            Aviso do agendamento:{' '}
            <strong>{data.avisoAgendamentoHabilitado ? 'ligado' : 'desligado'}</strong> — vale para SER, SERNIT e ESUS
            juntos e se liga em{' '}
            <Link to="/app/mensageria?aba=regras" className="text-red-700 hover:underline">
              Mensageria → Regras
            </Link>
            .
          </>
        ) : (
          <>
            O aviso do agendamento do SISREG se liga unidade a unidade (Unidade → aba SISREG). Estas duas chaves são as
            mesmas de{' '}
            <Link to="/app/mensageria?aba=regras" className="text-red-700 hover:underline">
              Mensageria → Regras
            </Link>
            : mudar aqui muda lá.
          </>
        )}
      </p>

      <div className="space-y-4">
        <div className="flex flex-wrap items-start gap-3">
          <label className="flex flex-1 items-start gap-2 text-sm">
            <input
              type="checkbox"
              className="mt-0.5 size-4"
              checked={atual.lembreteHabilitado}
              onChange={(e) => alterar({ lembreteHabilitado: e.target.checked })}
              disabled={!podeEditar}
            />
            <span>
              Enviar lembrete antes do agendamento
              <span className="block text-xs text-slate-500">
                Quem já confirmou recebe o lembrete com o dia e a hora. Quem não respondeu recebe a primeira mensagem de
                novo. Quem avisou que não vai não recebe nada, e quem recebeu o aviso há menos de 7 dias também não.
              </span>
            </span>
          </label>
          <div className="flex items-center gap-2 text-sm text-slate-700">
            <Input
              id={`${sistema}-lembrete-dias`}
              type="number"
              min={1}
              max={30}
              className="w-20"
              value={atual.lembreteDiasAntes}
              onChange={(e) => alterar({ lembreteDiasAntes: e.target.value })}
              disabled={!podeEditar}
              aria-label="Dias antes do agendamento"
            />
            <label htmlFor={`${sistema}-lembrete-dias`}>dia(s) antes</label>
          </div>
        </div>
        {!diasValidos && <p className="text-xs text-red-700">O lembrete sai de 1 a 30 dias antes.</p>}

        <label className="flex items-start gap-2 text-sm">
          <input
            type="checkbox"
            className="mt-0.5 size-4"
            checked={atual.avisoCancelamentoHabilitado}
            onChange={(e) => alterar({ avisoCancelamentoHabilitado: e.target.checked })}
            disabled={!podeEditar}
          />
          <span>
            Avisar o paciente quando o agendamento for cancelado
            <span className="block text-xs text-slate-500">
              {sistema === 'esussg' ? (
                <>
                  Sai quando o ESUS mostra o pedido de volta na fila, com a data ainda pela frente — a mensagem diz que
                  o agendamento foi desmarcado e o pedido voltou para a fila. Saída da fila não avisa: o ESUS não diz
                  por quê. O aviso espera 30 minutos: se o pedido voltar com o mesmo dia, não sai. Mesmo modelo do
                  SISREG: anônimo para número não confirmado.
                </>
              ) : externo ? (
                <>
                  Sai quando o {rotulo} mostra o pedido cancelado ou devolvido para a fila, com a data ainda pela frente
                  — no segundo caso a mensagem diz que o pedido voltou para a fila. Pendência não avisa (às vezes o
                  pedido volta com o mesmo dia). O aviso espera 30 minutos: se o pedido voltar com o mesmo dia, não
                  sai. Mesmo modelo do SISREG: anônimo para número não confirmado.
                </>
              ) : (
                <>Os cancelamentos feitos no SISREG por outra pessoa chegam pela conciliação de cancelamentos.</>
              )}{' '}
              Ligar vale daqui para frente.
              {data.avisoCancelamentoHabilitado && data.avisoCancelamentoLigadoEm && (
                <> Ligado em {formatarInstante(data.avisoCancelamentoLigadoEm)}.</>
              )}
            </span>
            {!externo && data.conciliacaoCancelamentoHabilitada === false && (
              <span className="mt-1 block rounded bg-amber-50 px-2 py-1 text-xs text-amber-800">
                A conciliação de cancelamentos está desligada (Mensageria → Regras): só os cancelamentos feitos pela tela
                de Confirmações avisam.
              </span>
            )}
          </span>
        </label>
      </div>

      {podeEditar && (
        <div className="mt-4 flex items-center justify-end gap-3">
          {salvar.isError && <span className="text-sm text-red-700">{extrairMensagemDeErro(salvar.error)}</span>}
          {salvar.isSuccess && <span className="text-sm text-emerald-700">Salvo.</span>}
          <Button onClick={aoSalvar} disabled={!rascunho || !diasValidos || salvar.isPending}>
            {salvar.isPending ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
            Salvar avisos
          </Button>
        </div>
      )}
    </section>
  );
}
