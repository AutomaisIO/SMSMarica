import { ArrowLeft } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { FormularioUnidadeAtendimento } from '@/features/unidades-atendimento/components/FormularioUnidadeAtendimento';

export function UnidadeAtendimentoFormPage() {
  const navigate = useNavigate();
  const params = useParams<{ id?: string }>();
  const editando = Boolean(params.id);
  const voltarPara = editando ? `/app/unidades-atendimento/${params.id}` : '/app/unidades-atendimento';

  return (
    <div className="space-y-6">
      <header className="flex items-center gap-3">
        <button
          type="button"
          onClick={() => navigate(voltarPara)}
          className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
          aria-label="Voltar"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <div className="flex items-center gap-1.5">
            <h1 className="text-2xl font-semibold text-gray-900">
              {editando ? 'Editar unidade de atendimento' : 'Nova unidade de atendimento'}
            </h1>
            <AjudaManual artigo="unidades-atendimento" secao="cadastrar" />
          </div>
          <p className="mt-1 text-sm text-gray-600">
            Nome, endereço completo e o ponto no mapa onde a van deixa o paciente.
          </p>
        </div>
      </header>

      <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <FormularioUnidadeAtendimento
          modo={editando ? 'editar' : 'criar'}
          idUnidade={params.id ?? null}
          aoConcluir={(id) => navigate(id ? `/app/unidades-atendimento/${id}` : voltarPara)}
        />
      </div>
    </div>
  );
}
