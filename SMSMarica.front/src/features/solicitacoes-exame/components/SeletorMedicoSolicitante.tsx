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
  /** "CRM" (médico) ou "COREN" (enfermeiro). */
  solicitanteConselho: string;
};

type Props = {
  valor: Valor;
  aoMudar: (v: Valor) => void;
};

const UFS = ['AC','AL','AM','AP','BA','CE','DF','ES','GO','MA','MG','MS','MT','PA','PB','PE','PI','PR','RJ','RN','RO','RR','RS','SC','SE','SP','TO'];

const CONSELHOS = [
  { conselho: 'CRM', profissao: 'Médico' },
  { conselho: 'COREN', profissao: 'Enfermeiro' },
] as const;

function useDebounce<T>(valor: T, ms = 300): T {
  const [v, setV] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setV(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return v;
}

export function SeletorMedicoSolicitante({ valor, aoMudar }: Props) {
  const conselho = (valor.solicitanteConselho || 'CRM').toUpperCase();
  const ehEnfermeiro = conselho === 'COREN';
  const profissao = ehEnfermeiro ? 'Enfermeiro' : 'Médico';
  const profissaoLower = ehEnfermeiro ? 'enfermeiro' : 'médico';
  const registroLabel = ehEnfermeiro ? 'COREN' : 'CRM';

  const [aba, setAba] = useState<'interno' | 'externo'>(valor.solicitanteUsuarioId ? 'interno' : 'externo');
  const [filtro, setFiltro] = useState('');
  const debounced = useDebounce(filtro, 300);
  // Busca filtrada pelo conselho escolhido (médicos OU enfermeiros).
  const medicos = useBuscarMedicos(debounced, { conselho });

  function trocarConselho(novo: string) {
    if (novo === conselho) return; // clicar no já-ativo não apaga nada
    // Conselho diferente = registro de OUTRO conselho: limpa o solicitante inteiro
    // (nome/registro/UF/vínculo) para forçar reentrada coerente — evita publicar o
    // nº de CRM de um médico rotulado como COREN (e vice-versa) no laudo médico-legal.
    aoMudar({
      ...valor,
      solicitanteConselho: novo,
      solicitanteUsuarioId: null,
      solicitanteNome: '',
      solicitanteCrm: '',
      solicitanteUfCrm: '',
    });
  }

  function escolherInterno(profId: string) {
    const m = medicos.data?.find((x) => x.id === profId);
    if (!m) {
      aoMudar({ ...valor, solicitanteUsuarioId: null, solicitanteNome: '', solicitanteCrm: '', solicitanteUfCrm: '' });
      return;
    }
    aoMudar({
      solicitanteUsuarioId: m.id,
      solicitanteNome: m.nomeCompleto,
      solicitanteCrm: m.registro,
      solicitanteUfCrm: m.ufConselho,
      solicitanteConselho: m.conselho || conselho,
    });
  }

  return (
    <div className="space-y-3">
      {/* Conselho: Médico (CRM) / Enfermeiro (COREN) */}
      <div className="inline-flex rounded-md border border-gray-200 bg-gray-50 p-0.5">
        {CONSELHOS.map((c) => (
          <button
            key={c.conselho}
            type="button"
            onClick={() => trocarConselho(c.conselho)}
            className={cn(
              'rounded px-3 py-1.5 text-sm font-medium',
              conselho === c.conselho ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-600 hover:text-gray-900',
            )}
          >
            {c.profissao} ({c.conselho})
          </button>
        ))}
      </div>

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
          {profissao} cadastrado
        </button>
        <button
          type="button"
          onClick={() => {
            setAba('externo');
            // Limpa o vínculo com o profissional interno mas preserva nome digitado.
            aoMudar({ ...valor, solicitanteUsuarioId: null });
          }}
          className={cn(
            'inline-flex items-center gap-1.5 rounded px-3 py-1.5 text-sm font-medium',
            aba === 'externo' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-600 hover:text-gray-900',
          )}
        >
          <UserPlus className="h-4 w-4" />
          {profissao} externo
        </button>
      </div>

      {aba === 'interno' ? (
        <div className="space-y-2">
          <Campo label={`Buscar ${profissaoLower}`} htmlFor="prof-busca">
            <Input
              id="prof-busca"
              value={filtro}
              onChange={(e) => setFiltro(e.target.value)}
              placeholder="Nome ou CPF (qualquer parte) — sem busca, mostra os 10 últimos"
            />
          </Campo>
          <Campo label={`Selecionar ${profissaoLower}`} htmlFor="prof-interno">
            <Select
              id="prof-interno"
              value={valor.solicitanteUsuarioId ?? ''}
              onChange={(e) => escolherInterno(e.target.value)}
            >
              <option value="">— Selecione —</option>
              {/* Mantém o profissional já escolhido visível mesmo fora dos resultados atuais. */}
              {valor.solicitanteUsuarioId
                && !(medicos.data ?? []).some((m) => m.id === valor.solicitanteUsuarioId) ? (
                <option value={valor.solicitanteUsuarioId}>
                  {valor.solicitanteNome} ({registroLabel} {valor.solicitanteUfCrm}/{valor.solicitanteCrm})
                </option>
              ) : null}
              {(medicos.data ?? []).map((m) => (
                <option key={m.id} value={m.id}>
                  {m.nomeCompleto} ({m.conselho || registroLabel} {m.ufConselho}/{m.registro})
                </option>
              ))}
            </Select>
          </Campo>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-4">
          <Campo label={`Nome do ${profissaoLower}`} htmlFor="solicitante-nome" className="sm:col-span-2">
            <Input
              id="solicitante-nome"
              value={valor.solicitanteNome}
              onChange={(e) => aoMudar({ ...valor, solicitanteNome: e.target.value })}
              placeholder="Nome completo"
            />
          </Campo>
          <Campo label={registroLabel} htmlFor="solicitante-crm">
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
