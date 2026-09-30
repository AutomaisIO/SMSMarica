import { Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { descreverDias } from '@/shared/lib/diasSemana';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { SeletorDiasSemana } from '@/shared/ui/SeletorDiasSemana';
import { usePreviaAgenda } from '@/features/tratamentos/api/queries';
import {
  LIMITE_SESSOES,
  diaSemanaCurto,
  formatarDataBr,
  paraAgendaPayload,
  type EstadoAgenda,
} from '@/features/tratamentos/lib/agenda';

type Props = {
  valor: EstadoAgenda;
  aoMudar: (v: EstadoAgenda) => void;
  /** Rótulo do campo de data (no cadastro é o início; na troca, "a partir de"). */
  rotuloInicio?: string;
  inicioMinimo?: string;
};

/**
 * Agenda do atendimento: dias da semana + N sessões, ou contínuo (renova todo mês). A prévia vem
 * do servidor — é ele que gera as sessões.
 */
export function CamposAgenda({ valor, aoMudar, rotuloInicio = 'Data de início', inicioMinimo }: Props) {
  const payload = paraAgendaPayload(valor);
  const previa = usePreviaAgenda(payload);
  const datas = payload ? (previa.data?.datas ?? []) : [];

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        <Campo label={rotuloInicio} htmlFor="agenda-inicio" required>
          <Input
            id="agenda-inicio"
            type="date"
            min={inicioMinimo}
            value={valor.dataInicio}
            onChange={(e) => aoMudar({ ...valor, dataInicio: e.target.value })}
          />
        </Campo>
        <Campo
          label="Dias da semana"
          htmlFor="agenda-dias"
          required
          className="md:col-span-2"
          dica={valor.diasSemanaMascara ? descreverDias(valor.diasSemanaMascara) : 'Marque os dias em que o paciente vai.'}
        >
          <SeletorDiasSemana
            id="agenda-dias"
            valor={valor.diasSemanaMascara}
            aoMudar={(m) => aoMudar({ ...valor, diasSemanaMascara: m })}
          />
        </Campo>
      </div>

      <fieldset className="space-y-2">
        <legend className="label">Duração</legend>
        <label className="flex flex-wrap items-center gap-2 text-sm text-gray-800">
          <input
            type="radio"
            name="agenda-modo"
            checked={!valor.continuo}
            onChange={() => aoMudar({ ...valor, continuo: false })}
          />
          Número de sessões:
          <Input
            type="number"
            min={1}
            max={LIMITE_SESSOES}
            inputMode="numeric"
            className="w-24"
            aria-label="Número de sessões"
            value={valor.quantidade}
            disabled={valor.continuo}
            onChange={(e) => aoMudar({ ...valor, quantidade: e.target.value })}
          />
        </label>
        <label className="flex items-start gap-2 text-sm text-gray-800">
          <input
            type="radio"
            name="agenda-modo"
            className="mt-1"
            checked={valor.continuo}
            onChange={() => aoMudar({ ...valor, continuo: true })}
          />
          <span>
            Contínuo — sem fim previsto.
            <span className="block text-xs text-gray-500">
              As sessões são criadas de mês em mês (sempre até o fim do mês seguinte). Param quando o
              atendimento é encerrado ou quando há óbito no cadastro.
            </span>
          </span>
        </label>
      </fieldset>

      <div>
        <div className="flex items-center gap-2">
          <h3 className="text-sm font-semibold text-gray-900">
            Prévia das sessões {payload ? `(${datas.length})` : ''}
          </h3>
          {previa.isFetching ? <Loader2 className="h-4 w-4 animate-spin text-gray-400" /> : null}
        </div>
        {!payload ? (
          <p className="mt-2 text-sm text-gray-500">
            Preencha a data, marque os dias e o número de sessões (ou contínuo) para ver as datas.
          </p>
        ) : previa.isError ? (
          <p className="mt-2 text-sm text-red-700">{extrairMensagemDeErro(previa.error)}</p>
        ) : (
          <>
            {valor.continuo && previa.data?.geradasAte ? (
              <p className="mt-1 text-xs text-gray-500">
                Criadas agora até {formatarDataBr(previa.data.geradasAte)}; depois, renovadas todo mês.
              </p>
            ) : null}
            <ul className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-6">
              {datas.map((d) => (
                <li key={d} className="rounded-md border border-gray-200 bg-gray-50 px-3 py-1.5 text-sm">
                  <strong>{formatarDataBr(d)}</strong>
                  <span className="ml-1 text-xs text-gray-500">{diaSemanaCurto(d)}</span>
                </li>
              ))}
            </ul>
          </>
        )}
      </div>
    </div>
  );
}
