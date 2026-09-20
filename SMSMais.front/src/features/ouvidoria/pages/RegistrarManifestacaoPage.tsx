import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, FilePlus2 } from 'lucide-react';
import { ModalProtocoloCriado } from '@/features/ouvidoria/components/ModalProtocoloCriado';
import { RegistrarManifestacaoForm } from '@/features/ouvidoria/components/RegistrarManifestacaoForm';
import type { ManifestacaoCriadaDto } from '@/features/ouvidoria/types';

/** Registro interno pela ouvidoria (canal presencial, telefone, carta…). */
export function RegistrarManifestacaoPage() {
  const navigate = useNavigate();
  const [criada, setCriada] = useState<ManifestacaoCriadaDto | null>(null);

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <button
        type="button"
        onClick={() => navigate('/app/ouvidoria')}
        className="inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-800"
      >
        <ArrowLeft className="h-4 w-4" aria-hidden="true" /> Voltar à fila
      </button>

      <div>
        <h1 className="flex items-center gap-2 text-xl font-semibold text-slate-800">
          <FilePlus2 className="h-5 w-5 text-red-600" aria-hidden="true" />
          Registrar manifestação
        </h1>
        <p className="text-sm text-slate-500">
          Só tipo, identificação, canal e relato são obrigatórios. Nada é recusado por falta dos demais campos.
        </p>
      </div>

      <RegistrarManifestacaoForm aoCriar={setCriada} />

      <ModalProtocoloCriado
        criada={criada}
        aoFechar={() => setCriada(null)}
        aoAbrirDetalhe={(id) => navigate(`/app/ouvidoria/${id}`)}
        aoRegistrarOutra={() => setCriada(null)}
      />
    </div>
  );
}
