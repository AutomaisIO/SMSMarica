import { useState } from 'react';
import { Stethoscope, UserPlus } from 'lucide-react';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useListarMedicos } from '@/features/medicos/api/queries';
import { cn } from '@/shared/lib/cn';

type Valor = {
  solicitanteUsuarioId: string | null;
  solicitanteNome: string;
  solicitanteCrm: string;
  solicitanteUfCrm: string;
};

type Props = {
  valor: Valor;
  aoMudar: (v: Valor) => void;
};

const UFS = ['AC','AL','AM','AP','BA','CE','DF','ES','GO','MA','MG','MS','MT','PA','PB','PE','PI','PR','RJ','RN','RO','RR','RS','SC','SE','SP','TO'];

export function SeletorMedicoSolicitante({ valor, aoMudar }: Props) {
  const [aba, setAba] = useState<'interno' | 'externo'>(valor.solicitanteUsuarioId ? 'interno' : 'externo');
  const medicos = useListarMedicos();

  function escolherInterno(usuarioId: string) {
    const m = medicos.data?.find((x) => x.usuarioId === usuarioId);
    if (!m) {
      aoMudar({ solicitanteUsuarioId: null, solicitanteNome: '', solicitanteCrm: '', solicitanteUfCrm: '' });
      return;
    }
    aoMudar({
      solicitanteUsuarioId: m.usuarioId,
      solicitanteNome: m.nomeCompleto,
      solicitanteCrm: m.crm,
      solicitanteUfCrm: m.ufCrm,
    });
  }

  return (
    <div className="space-y-3">
      <div className="inline-flex rounded-md border border-gray-200 bg-gray-50 p-0.5">
        <button
          type="button"
          onClick={() => setAba('interno')}
          className={cn(
            'inline-flex items-center gap-1.5 rounded px-3 py-1.5 text-sm font-medium',
            aba === 'interno' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-600 hover:text-gray-900',
          )}
        >
          <Stethoscope className="h-4 w-4" />
          Médico cadastrado
        </button>
        <button
          type="button"
          onClick={() => {
            setAba('externo');
            // Limpa o vínculo com Médico interno mas preserva nome digitado.
            aoMudar({ ...valor, solicitanteUsuarioId: null });
          }}
          className={cn(
            'inline-flex items-center gap-1.5 rounded px-3 py-1.5 text-sm font-medium',
            aba === 'externo' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-600 hover:text-gray-900',
          )}
        >
          <UserPlus className="h-4 w-4" />
          Médico externo
        </button>
      </div>

      {aba === 'interno' ? (
        <Campo label="Selecionar médico" htmlFor="medico-interno">
          <Select
            id="medico-interno"
            value={valor.solicitanteUsuarioId ?? ''}
            onChange={(e) => escolherInterno(e.target.value)}
          >
            <option value="">— Selecione —</option>
            {(medicos.data ?? []).map((m) => (
              <option key={m.id} value={m.usuarioId}>
                {m.nomeCompleto} (CRM {m.ufCrm}/{m.crm})
              </option>
            ))}
          </Select>
        </Campo>
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-4">
          <Campo label="Nome do médico" htmlFor="solicitante-nome" className="sm:col-span-2">
            <Input
              id="solicitante-nome"
              value={valor.solicitanteNome}
              onChange={(e) => aoMudar({ ...valor, solicitanteNome: e.target.value })}
              placeholder="Nome completo"
            />
          </Campo>
          <Campo label="CRM" htmlFor="solicitante-crm">
            <Input
              id="solicitante-crm"
              value={valor.solicitanteCrm}
              onChange={(e) => aoMudar({ ...valor, solicitanteCrm: e.target.value.replace(/\D/g, '') })}
              placeholder="123456"
            />
          </Campo>
          <Campo label="UF" htmlFor="solicitante-uf">
            <Select
              id="solicitante-uf"
              value={valor.solicitanteUfCrm}
              onChange={(e) => aoMudar({ ...valor, solicitanteUfCrm: e.target.value })}
            >
              <option value="">UF</option>
              {UFS.map((uf) => (
                <option key={uf} value={uf}>
                  {uf}
                </option>
              ))}
            </Select>
          </Campo>
        </div>
      )}
    </div>
  );
}
