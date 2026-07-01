import { ScanLine, User } from 'lucide-react';

type Props = {
  pacienteNome?: string | null;
  pacienteCpf?: string | null;
  /** Nome cru do DICOM — rótulo temporário exibido em cinza quando não há vínculo. */
  pacienteNomeDicom?: string | null;
  /** Há paciente vinculado (PacienteId setado), mesmo que o nome não tenha resolvido no hub. */
  pacienteVinculado?: boolean;
  studyInstanceUID: string;
  medicoNome?: string;
  medicoCrm?: string;
  medicoUfCrm?: string;
  modalidade?: string;
};

export function CabecalhoLaudo({
  pacienteNome,
  pacienteCpf,
  pacienteNomeDicom,
  pacienteVinculado,
  studyInstanceUID,
  medicoNome,
  medicoCrm,
  medicoUfCrm,
  modalidade,
}: Props) {
  return (
    <div className="grid grid-cols-1 gap-3 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-3">
      <div className="flex items-start gap-2 min-w-0">
        <User className="mt-0.5 h-4 w-4 flex-shrink-0 text-gray-400" />
        <div className="min-w-0">
          <div className="text-xs uppercase tracking-wide text-gray-500">Paciente</div>
          {pacienteNome ? (
            <div className="truncate text-sm font-medium text-gray-900">{pacienteNome}</div>
          ) : pacienteVinculado ? (
            // Vinculado, mas o nome não resolveu no hub FHIR — vínculo real, nunca "não vinculado".
            <div className="truncate text-sm font-medium text-gray-500">Paciente vinculado</div>
          ) : pacienteNomeDicom ? (
            // Selo em linha própria: fora do elemento com `truncate`, para não ser cortado
            // por nomes longos (o nome trunca; o rótulo de alerta permanece visível).
            <div title="Nome informado no equipamento (DICOM). Exame ainda não vinculado a um paciente cadastrado — associe-o para confirmar.">
              <div className="truncate text-sm font-medium italic text-gray-400">
                {pacienteNomeDicom}
              </div>
              <span className="text-[10px] uppercase tracking-wide text-gray-400">não vinculado</span>
            </div>
          ) : (
            <div className="truncate text-sm font-medium text-gray-400">Não vinculado</div>
          )}
          {pacienteCpf ? <div className="text-xs text-gray-500">CPF {pacienteCpf}</div> : null}
        </div>
      </div>

      <div className="flex items-start gap-2 min-w-0">
        <ScanLine className="mt-0.5 h-4 w-4 flex-shrink-0 text-gray-400" />
        <div className="min-w-0">
          <div className="text-xs uppercase tracking-wide text-gray-500">Exame</div>
          <div className="truncate text-sm font-mono text-gray-900">{studyInstanceUID}</div>
          {modalidade ? <div className="text-xs text-gray-500">Modalidade {modalidade}</div> : null}
        </div>
      </div>

      <div className="min-w-0">
        <div className="text-xs uppercase tracking-wide text-gray-500">Médico responsável</div>
        <div className="truncate text-sm font-medium text-gray-900">
          {medicoNome || 'Você (do JWT)'}
        </div>
        {medicoCrm ? (
          <div className="text-xs text-gray-500">
            CRM {medicoUfCrm}/{medicoCrm}
          </div>
        ) : null}
      </div>
    </div>
  );
}
