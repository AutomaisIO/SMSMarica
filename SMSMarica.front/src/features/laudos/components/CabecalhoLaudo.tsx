import { ScanLine, User } from 'lucide-react';

type Props = {
  pacienteNome?: string | null;
  pacienteCpf?: string | null;
  studyInstanceUID: string;
  medicoNome?: string;
  medicoCrm?: string;
  medicoUfCrm?: string;
  modalidade?: string;
};

export function CabecalhoLaudo({
  pacienteNome,
  pacienteCpf,
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
          <div className="truncate text-sm font-medium text-gray-900">
            {pacienteNome || 'Não vinculado'}
          </div>
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
