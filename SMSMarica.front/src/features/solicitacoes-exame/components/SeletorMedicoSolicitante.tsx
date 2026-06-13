import { useEffect, useState } from 'react';
import { Stethoscope, UserPlus } from 'lucide-react';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useBuscarMedicos } from '@/features/medicos/api/queries';
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

function useDebounce<T>(valor: T, ms = 300): T {
  const [v, setV] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setV(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return v;
}

export function SeletorMedicoSolicitante({ valor, aoMudar }: Props) {
  const [aba, setAba] = useState<'interno' | 'externo'>(valor.solicitanteUsuarioId ? 'interno' : 'externo');
  const [filtro, setFiltro] = useState('');
  const debounced = useDebounce(filtro, 300);
  // Sem filtro: 10 últimos cadastros; com filtro: busca por nome/CPF no hub FHIR.
  const medicos = useBuscarMedicos(debounced, { conselho: 'CRM' });

  function escolherInterno(medicoId: string) {
    const m = medicos.data?.find((x) => x.id === medicoId);
    if (!m) {
      aoMudar({ solicitanteUsuarioId: null, solicitanteNome: '', solicitanteCrm: '', solicitanteUfCrm: '' });
      return;
    }
    aoMudar({
      solicitanteUsuarioId: m.id,
      solicitanteNome: m.nomeCompleto,
      solicitanteCrm: m.registro,
      solicitanteUfCrm: m.ufConselho,
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
        <div className="space-y-2">
          <Campo label="Buscar médico" htmlFor="medico-busca">
            <Input
              id="medico-busca"
              value={filtro}
              onChange={(e) => setFiltro(e.target.value)}
              placeholder="Nome ou CPF (qualquer parte) — sem busca, mostra os 10 últimos"
            />
          </Campo>
          <Campo label="Selecionar médico" htmlFor="medico-interno">
            <Select
              id="medico-interno"
              value={valor.solicitanteUsuarioId ?? ''}
              onChange={(e) => escolherInterno(e.target.value)}
            >
              <option value="">— Selecione —</option>
              {/* Mantém o médico já escolhido visível mesmo fora dos resultados atuais. */}
              {valor.solicitanteUsuarioId
                && !(medicos.data ?? []).some((m) => m.id === valor.solicitanteUsuarioId) ? (
                <option value={valor.solicitanteUsuarioId}>
                  {valor.solicitanteNome} (CRM {valor.solicitanteUfCrm}/{valor.solicitanteCrm})
                </option>
              ) : null}
              {(medicos.data ?? []).map((m) => (
                <option key={m.id} value={m.id}>
                  {m.nomeCompleto} (CRM {m.ufConselho}/{m.registro})
                </option>
              ))}
            </Select>
          </Campo>
        </div>
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
