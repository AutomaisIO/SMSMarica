import { useState } from 'react';

const ROTULOS_RAW_SISREG = [
  'Código Solicitação', 'Cód. proc. interno', 'Código SIGTAP', 'Procedimento',
  'CPF prof. executante', 'Prof. executante', 'Data atendimento', 'Hora atendimento',
  'Vaga (flag)', 'CNS paciente', 'Paciente', 'Nascimento', 'Idade', 'Registro',
  'Nome da mãe', 'Tipo logradouro', 'Logradouro', 'Complemento', 'Número', 'Bairro', 'CEP',
  'Telefone', 'Município', 'IBGE', 'Município (2)', 'IBGE (2)',
  'CNES unid. solicitante', 'Unidade solicitante', 'Sexo', 'Data solicitação', 'Op. solicitante',
  'Data aprovação', 'Op. autorizador', 'Valor SIGTAP', 'Situação', 'CID',
  'CPF médico solicitante', 'Médico solicitante',
];
// CPFs de profissionais → mascarados (XX***XX). Não expor CPF de médico/executante.
const IDX_CPF_MASCARAR = new Set([4, 36]);

function mascararCpf(v: string) {
  const d = v.replace(/\D/g, '');
  return d.length < 4 ? v : `${d.slice(0, 2)}***${d.slice(-2)}`;
}

function parsearRawSisreg(raw: string): { rotulo: string; valor: string }[] {
  const campos = raw.split(';');
  return ROTULOS_RAW_SISREG.map((rotulo, i) => {
    let valor = (campos[i] ?? '').trim();
    if (IDX_CPF_MASCARAR.has(i) && valor) valor = mascararCpf(valor);
    return { rotulo, valor };
  });
}

/**
 * Botão "Raw data (SISREG)" + a linha crua do TXT parseada em campos — proveniência da
 * importação. Mesmo componente para exame e consulta (o layout é idêntico).
 */
export function RawSisregDisclosure({ raw }: { raw: string }) {
  const [mostrar, setMostrar] = useState(false);
  return (
    <div className="lg:col-span-2">
      <button
        type="button"
        onClick={() => setMostrar((v) => !v)}
        className="rounded border border-gray-200 bg-gray-50 px-2.5 py-1 text-xs font-medium text-gray-400 transition-colors hover:bg-gray-100 hover:text-gray-600"
        title="Dado bruto da importação do SISREG (TXT)"
      >
        {mostrar ? 'Ocultar' : 'Raw data (SISREG)'}
      </button>
      {mostrar ? (
        <div className="mt-2 rounded-lg border border-gray-200 bg-gray-50 p-4">
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">
            Arquivo Agendamento (TXT) — linha de origem
          </h3>
          <dl className="grid grid-cols-1 gap-x-6 gap-y-1 text-sm sm:grid-cols-2">
            {parsearRawSisreg(raw).map((campo) => (
              <div key={campo.rotulo} className="flex gap-2">
                <dt className="min-w-[11rem] shrink-0 text-gray-500">{campo.rotulo}:</dt>
                <dd className="break-all font-medium text-gray-800">{campo.valor || '—'}</dd>
              </div>
            ))}
          </dl>
        </div>
      ) : null}
    </div>
  );
}
