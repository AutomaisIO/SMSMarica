import { useState } from 'react';
import { Loader2, Pencil, UserRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { cn } from '@/shared/lib/cn';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { usePacientePorId } from '@/features/pacientes/api/queries';
import type { Paciente } from '@/features/pacientes/types';

const SEXO_LABEL: Record<string, string> = {
  NaoInformado: 'Não informado',
  Masculino: 'Masculino',
  Feminino: 'Feminino',
  Outro: 'Outro',
};

function formatarCpf(cpf: string): string {
  const d = (cpf ?? '').replace(/\D/g, '');
  return d.length === 11 ? `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}` : cpf;
}

function formatarData(iso?: string | null): string | null {
  if (!iso) return null;
  const m = String(iso).match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : String(iso);
}

function Linha({ rotulo, valor }: { rotulo: string; valor?: string | null }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs font-medium uppercase tracking-wide text-gray-500">{rotulo}</span>
      <span className="text-sm text-gray-900">{valor || <span className="text-gray-400">—</span>}</span>
    </div>
  );
}

/** Conteúdo do card-resumo — só monta a query quando o modal está aberto. */
function ResumoConteudo({ pacienteId }: { pacienteId: string }) {
  const detalhe = usePacientePorId(pacienteId);
  const p: Paciente | undefined = detalhe.data;

  if (detalhe.isLoading) {
    return (
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Loader2 className="h-4 w-4 animate-spin" /> Carregando dados…
      </div>
    );
  }
  if (detalhe.isError || !p) {
    return (
      <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        Não foi possível carregar os dados do paciente.
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div className="sm:col-span-2">
        <Linha rotulo="Nome completo" valor={p.nomeCompleto} />
      </div>
      <Linha rotulo="CPF" valor={formatarCpf(p.cpf)} />
      <Linha rotulo="CNS (Cartão SUS)" valor={p.cns} />
      <Linha rotulo="Data de nascimento" valor={formatarData(p.dataNascimento)} />
      <Linha rotulo="Sexo" valor={SEXO_LABEL[String(p.sexo)] ?? String(p.sexo)} />
      <Linha rotulo="Telefone" valor={p.telefonePrincipal} />
      <Linha rotulo="Nome da mãe" valor={p.nomeDaMae} />
    </div>
  );
}

type Props = {
  /** Id do paciente (GET /pacientes/{id}). */
  pacienteId: string;
  /** Nome já conhecido — exibido ao lado do ícone. Se omitido, mostra só o ícone. */
  nome?: string | null;
  /** Classe extra do wrapper. */
  className?: string;
  /** Classe extra do texto do nome. */
  classNameNome?: string;
};

/**
 * Renderiza o nome do paciente (opcional) com um ícone pequeno ao lado. Ao clicar
 * no ícone abre um modal com um card-resumo dos dados (nome, CPF, CNS, nascimento,
 * sexo, telefone, nome da mãe) e um botão "Editar" que leva à edição do paciente.
 * Componente genérico/reutilizável dentro da feature de pacientes.
 */
export function NomePacienteComResumo({ pacienteId, nome, className, classNameNome }: Props) {
  const navigate = useNavigate();
  const [aberto, setAberto] = useState(false);

  return (
    <span className={cn('inline-flex items-center gap-1.5', className)}>
      {nome ? <span className={classNameNome}>{nome}</span> : null}
      <button
        type="button"
        onClick={() => setAberto(true)}
        className="rounded p-0.5 text-gray-400 hover:bg-gray-100 hover:text-red-700"
        aria-label="Ver resumo do paciente"
        title="Ver resumo do paciente"
      >
        <UserRound className="h-4 w-4" />
      </button>

      <Modal
        aberto={aberto}
        aoFechar={() => setAberto(false)}
        titulo="Resumo do paciente"
        largura="md"
      >
        {aberto ? <ResumoConteudo pacienteId={pacienteId} /> : null}
        <div className="mt-6 flex justify-between gap-3 border-t border-gray-100 pt-4">
          <Button
            variante="ghost"
            onClick={() => {
              setAberto(false);
              navigate(`/app/pacientes/${pacienteId}/editar`);
            }}
          >
            <Pencil className="h-4 w-4" /> Editar
          </Button>
          <Button onClick={() => setAberto(false)}>Fechar</Button>
        </div>
      </Modal>
    </span>
  );
}
