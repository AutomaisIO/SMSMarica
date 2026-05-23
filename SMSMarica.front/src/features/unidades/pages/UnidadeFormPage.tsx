import { ArrowLeft } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { FormularioUnidade } from '@/features/unidades/components/FormularioUnidade';

export function UnidadeFormPage() {
  const navigate = useNavigate();
  const params = useParams<{ id?: string }>();
  const editando = Boolean(params.id);

  function aoConcluir() {
    if (editando && params.id) {
      navigate(`/app/unidades/${params.id}`);
    } else {
      navigate('/app/unidades');
    }
  }

  return (
    <div className="space-y-6">
      <header className="flex items-center gap-3">
        <button
          type="button"
          onClick={() => navigate(editando ? `/app/unidades/${params.id}` : '/app/unidades')}
          className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
          aria-label="Voltar"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">
            {editando ? 'Editar unidade' : 'Nova unidade'}
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            Dados de identificação, endereço estruturado e localização opcional.
          </p>
        </div>
      </header>

      <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <FormularioUnidade
          modo={editando ? 'editar' : 'criar'}
          idUnidade={params.id ?? null}
          aoConcluir={aoConcluir}
        />
      </div>
    </div>
  );
}
