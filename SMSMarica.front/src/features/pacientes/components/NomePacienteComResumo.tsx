import { useState, type FormEvent, type ReactNode } from 'react';
import { Check, Loader2, Pencil, UserRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { cn } from '@/shared/lib/cn';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { CodigoCopiavel } from '@/shared/ui/CodigoCopiavel';
import { usePacientePorId } from '@/features/pacientes/api/queries';
import { definirTelefonePrincipal } from '@/features/telefone-validacao/api/telefoneValidacaoApi';
import { BotaoVerificarTelefonePaciente } from '@/features/telefone-validacao/components/BotaoVerificarTelefonePaciente';
import type { Paciente } from '@/features/pacientes/types';

/** Logo do WhatsApp (lucide não traz ícones de marca). */
function WhatsappIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" className={className}>
      <path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.198.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.297-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51l-.57-.01c-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.71.306 1.263.489 1.694.626.712.226 1.36.194 1.872.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.002-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413Z" />
    </svg>
  );
}

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

/** Rótulo + valor copiável (só dígitos vão para a área de transferência). */
function LinhaCopiavel({ rotulo, valor, copiar }: { rotulo: string; valor?: string | null; copiar?: string }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs font-medium uppercase tracking-wide text-gray-500">{rotulo}</span>
      {valor ? (
        <span className="-ml-1.5 text-sm text-gray-900">
          <CodigoCopiavel codigo={valor} valorCopiar={copiar} dica={`Copiar ${rotulo} (só números)`} />
        </span>
      ) : (
        <span className="text-sm text-gray-400">—</span>
      )}
    </div>
  );
}

/** Conteúdo do card-resumo — só monta a query quando o modal está aberto. */
function ResumoConteudo({ pacienteId }: { pacienteId: string }) {
  const detalhe = usePacientePorId(pacienteId);
  const p: Paciente | undefined = detalhe.data;

  // Alteração rápida do telefone principal (ticket #16): salva direto no cadastro,
  // mesmo sem verificar — trocar o número derruba o selo; verificar depois é opcional.
  const [editandoFone, setEditandoFone] = useState(false);
  const [numeroNovo, setNumeroNovo] = useState('');
  const [salvandoFone, setSalvandoFone] = useState(false);
  const [erroFone, setErroFone] = useState<string | null>(null);

  // Situação do contato principal (WhatsApp): o verificado JÁ VEM no objeto do paciente
  // (marcador no telecom FHIR) — sem request extra, sem "piscada" de não-verificado.
  const telefoneValidado = Boolean(p?.telefoneVerificado);

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

  async function salvarTelefone(e: FormEvent) {
    e.preventDefault();
    if (!p || salvandoFone) return;
    setErroFone(null);
    setSalvandoFone(true);
    try {
      await definirTelefonePrincipal(p.cpf, numeroNovo);
      setEditandoFone(false);
      await detalhe.refetch();
    } catch (err) {
      setErroFone(extrairMensagemDeErro(err));
    } finally {
      setSalvandoFone(false);
    }
  }

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div className="sm:col-span-2">
        <Linha rotulo="Nome completo" valor={p.nomeCompleto} />
      </div>
      <LinhaCopiavel rotulo="CPF" valor={formatarCpf(p.cpf)} copiar={(p.cpf ?? '').replace(/\D/g, '')} />
      <LinhaCopiavel rotulo="CNS (Cartão SUS)" valor={p.cns} copiar={(p.cns ?? '').replace(/\D/g, '')} />
      <Linha rotulo="Data de nascimento" valor={formatarData(p.dataNascimento)} />
      <Linha rotulo="Sexo" valor={SEXO_LABEL[String(p.sexo)] ?? String(p.sexo)} />
      <div className="flex flex-col gap-0.5 sm:col-span-2">
        <span className="text-xs font-medium uppercase tracking-wide text-gray-500">Telefone</span>
        {!editandoFone ? (
          <span className="flex flex-wrap items-center gap-1.5 text-sm text-gray-900">
            {p.telefonePrincipal || <span className="text-gray-400">—</span>}
            {telefoneValidado ? (
              <span
                className="inline-flex items-center gap-0.5 text-emerald-600"
                title="Contato verificado no WhatsApp"
              >
                <WhatsappIcon className="h-4 w-4" />
                <Check className="h-3.5 w-3.5" strokeWidth={3} />
              </span>
            ) : (
              <BotaoVerificarTelefonePaciente
                cpf={p.cpf}
                numeroInicial={p.telefonePrincipal}
                aoValidado={() => detalhe.refetch()}
              />
            )}
            <button
              type="button"
              onClick={() => {
                setNumeroNovo(p.telefonePrincipal ?? '');
                setErroFone(null);
                setEditandoFone(true);
              }}
              className="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs font-medium text-primary-700 hover:bg-primary-50"
              title="Alterar o telefone principal (salva mesmo sem verificar)"
            >
              <Pencil className="h-3 w-3" /> Alterar telefone
            </button>
          </span>
        ) : (
          <form onSubmit={salvarTelefone} className="flex flex-col gap-1.5">
            <div className="flex flex-wrap items-center gap-2">
              <Input
                value={numeroNovo}
                onChange={(e) => setNumeroNovo(e.target.value)}
                placeholder="(21) 99999-0000"
                autoFocus
                className="w-44"
              />
              <Button type="submit" disabled={salvandoFone}>
                {salvandoFone ? <Loader2 className="h-4 w-4 animate-spin" /> : 'Salvar'}
              </Button>
              <Button type="button" variante="ghost" onClick={() => setEditandoFone(false)}>
                Cancelar
              </Button>
            </div>
            <span className="text-xs text-gray-500">
              Salva como telefone principal mesmo sem verificar (o selo de verificado cai; dá para verificar depois).
            </span>
            {erroFone ? <span className="text-xs text-red-700">{erroFone}</span> : null}
          </form>
        )}
      </div>
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
  /** Conteúdo opcional renderizado ao lado do ícone do paciente (ex.: alerta de urgência). */
  sufixo?: ReactNode;
};

/**
 * Renderiza o nome do paciente (opcional) com um ícone pequeno ao lado. Ao clicar
 * no ícone abre um modal com um card-resumo dos dados (nome, CPF, CNS, nascimento,
 * sexo, telefone, nome da mãe) e um botão "Editar" que leva à edição do paciente.
 * Componente genérico/reutilizável dentro da feature de pacientes.
 */
export function NomePacienteComResumo({ pacienteId, nome, className, classNameNome, sufixo }: Props) {
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
      {sufixo}

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
