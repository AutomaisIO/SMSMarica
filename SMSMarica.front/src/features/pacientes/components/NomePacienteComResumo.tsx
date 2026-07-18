import { useState, type FormEvent, type ReactNode } from 'react';
import { Check, Eye, Loader2, Pencil, UserRound } from 'lucide-react';
import { useLocation, useNavigate } from 'react-router-dom';
import { cn } from '@/shared/lib/cn';
import { pedirNavegacaoJanelaPrincipal } from '@/shared/lib/janela';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { CodigoCopiavel } from '@/shared/ui/CodigoCopiavel';
import { TelefoneCopiavel } from '@/shared/ui/TelefoneCopiavel';
import { WhatsappIcon } from '@/shared/ui/WhatsappIcon';
import { usePacientePorId } from '@/features/pacientes/api/queries';
import { definirTelefonePrincipal } from '@/features/telefone-validacao/api/telefoneValidacaoApi';
import { BotaoVerificarTelefonePaciente } from '@/features/telefone-validacao/components/BotaoVerificarTelefonePaciente';
import { UltimaSolicitacaoPaciente } from '@/features/solicitacoes-exame/components/UltimaSolicitacaoPaciente';
import { BotaoWhatsAppPaciente } from '@/features/conversas/components/BotaoWhatsAppPaciente';
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
            <span className="-ml-1.5">
              <TelefoneCopiavel numero={p.telefonePrincipal} />
            </span>
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
  /**
   * Atalho do WhatsApp ao lado do bonequinho. Desligue dentro do próprio chat, onde a
   * conversa já está aberta e o botão seria ruído.
   */
  mostrarWhatsApp?: boolean;
};

/**
 * Renderiza o nome do paciente (opcional) com um ícone pequeno ao lado. Ao clicar
 * no ícone abre um modal com um card-resumo dos dados (nome, CPF, CNS, nascimento,
 * sexo, telefone, nome da mãe) e um botão "Editar" que leva à edição do paciente.
 * Componente genérico/reutilizável dentro da feature de pacientes.
 */
export function NomePacienteComResumo({
  pacienteId, nome, className, classNameNome, sufixo, mostrarWhatsApp = true,
}: Props) {
  const navigate = useNavigate();
  const location = useLocation();
  const [aberto, setAberto] = useState(false);

  // Fecha o modal e navega. Fora de /app este componente roda numa JANELA SOLTA
  // (chat/PACS) — quem navega é a janela principal, via BroadcastChannel. Dentro
  // de /app, navega na própria janela. Vale p/ Editar e p/ a última solicitação.
  function navegarNaJanelaCerta(rota: string) {
    setAberto(false);
    if (!location.pathname.startsWith('/app') && pedirNavegacaoJanelaPrincipal(rota)) return;
    navigate(rota);
  }

  return (
    <span className={cn('inline-flex items-center gap-1.5', className)}>
      {/* min-w-0 deixa o nome encolher e truncar (…) dentro do flex; sem isso ele vaza a coluna. */}
      {nome ? <span className={cn('min-w-0', classNameNome)}>{nome}</span> : null}
      <button
        type="button"
        onClick={() => setAberto(true)}
        className="shrink-0 rounded p-0.5 text-gray-400 hover:bg-gray-100 hover:text-red-700"
        aria-label="Ver resumo do paciente"
        title="Ver resumo do paciente"
      >
        <UserRound className="h-4 w-4" />
      </button>
      {mostrarWhatsApp ? <BotaoWhatsAppPaciente pacienteId={pacienteId} /> : null}
      {sufixo}

      <Modal
        aberto={aberto}
        aoFechar={() => setAberto(false)}
        titulo="Resumo do paciente"
        largura="md"
      >
        {aberto ? <ResumoConteudo pacienteId={pacienteId} /> : null}
        {aberto ? (
          <UltimaSolicitacaoPaciente
            pacienteId={pacienteId}
            aoAbrir={(id) => navegarNaJanelaCerta(`/app/solicitacoes-exame/${id}`)}
          />
        ) : null}
        <div className="mt-6 flex flex-wrap items-center justify-between gap-3 border-t border-gray-100 pt-4">
          <div className="flex flex-wrap gap-2">
            <Button
              variante="outline"
              onClick={() => navegarNaJanelaCerta(`/app/pacientes/${pacienteId}`)}
            >
              <Eye className="h-4 w-4" /> Visualizar cadastro
            </Button>
            <Button
              variante="ghost"
              onClick={() => navegarNaJanelaCerta(`/app/pacientes/${pacienteId}/editar`)}
            >
              <Pencil className="h-4 w-4" /> Editar
            </Button>
          </div>
          <Button onClick={() => setAberto(false)}>Fechar</Button>
        </div>
      </Modal>
    </span>
  );
}
