import { useState } from 'react';
import { Plus } from 'lucide-react';
import { useTemConsulta } from '@/shared/auth/authStore';
import { ListaConversas } from '@/features/conversas/components/ListaConversas';
import { ThreadMensagens } from '@/features/conversas/components/ThreadMensagens';
import { NovaConversaDialog } from '@/features/conversas/components/NovaConversaDialog';

export function ConversasPage() {
  const podeSupervisao = useTemConsulta('ConversasSupervisao');
  const [ativa, setAtiva] = useState<string | null>(null);
  const [nova, setNova] = useState(false);

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Central de Atendimento</h1>
          <p className="text-sm text-gray-500">Conversas de WhatsApp das suas unidades.</p>
        </div>
        <button
          type="button"
          onClick={() => setNova(true)}
          className="flex items-center gap-1.5 rounded-md bg-primary-600 px-3 py-2 text-sm font-medium text-white hover:bg-primary-700"
        >
          <Plus className="h-4 w-4" /> Nova conversa
        </button>
      </div>

      <div className="flex h-[72vh] overflow-hidden rounded-lg border border-gray-200 bg-white">
        <div className="w-80 shrink-0 border-r border-gray-200">
          <ListaConversas
            conversaAtivaId={ativa}
            onSelecionar={setAtiva}
            podeSupervisao={podeSupervisao}
          />
        </div>
        <div className="min-w-0 flex-1">
          {ativa ? (
            <ThreadMensagens conversaId={ativa} />
          ) : (
            <div className="flex h-full items-center justify-center p-6 text-center text-sm text-gray-400">
              Selecione uma conversa à esquerda.
            </div>
          )}
        </div>
      </div>

      {nova && (
        <NovaConversaDialog
          onFechar={() => setNova(false)}
          onCriada={(id) => {
            setNova(false);
            setAtiva(id);
          }}
        />
      )}
    </div>
  );
}
