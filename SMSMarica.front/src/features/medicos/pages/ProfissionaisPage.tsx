import { useEffect, useMemo, useState } from 'react';
import { Eye, Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Avatar } from '@/shared/ui/Avatar';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useBuscarMedicos, useDesativarMedico } from '@/features/medicos/api/queries';
import { FormularioMedico } from '@/features/medicos/components/FormularioMedico';
import { CONSELHOS_PROFISSIONAIS, type FiltroConselho, type MedicoListItem } from '@/features/medicos/types';

type EstadoModal = { tipo: 'fechado' } | { tipo: 'criar' } | { tipo: 'editar'; id: string };

/** Aba "Todos" + uma por conselho não-médico. */
const ABAS = [{ sigla: 'TODOS', nome: 'Todos' }, ...CONSELHOS_PROFISSIONAIS] as const;

function useDebounce<T>(valor: T, ms = 300): T {
  const [debounced, setDebounced] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return debounced;
}

export function ProfissionaisPage() {
  const navigate = useNavigate();
  const [abaAtiva, setAbaAtiva] = useState<string>('TODOS');
  const [termo, setTermo] = useState('');
  const debounced = useDebounce(termo, 300);

  const filtro: FiltroConselho = useMemo(
    () => (abaAtiva === 'TODOS' ? { conselhoExceto: 'CRM' } : { conselho: abaAtiva }),
    [abaAtiva],
  );

  const busca = useBuscarMedicos(debounced, filtro);
  const desativar = useDesativarMedico();
  const [estado, setEstado] = useState<EstadoModal>({ tipo: 'fechado' });
  const [paraDesativar, setParaDesativar] = useState<MedicoListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const colunas: Coluna<MedicoListItem>[] = [
    {
      chave: 'nome',
      cabecalho: 'Nome',
      render: (m) => (
        <div className="flex items-center gap-2">
          <Avatar src={m.fotoBase64} nome={m.nomeCompleto} tamanho="sm" />
          <button
            type="button"
            onClick={() => navigate(`/app/profissionais/${m.id}`)}
            className="text-left font-medium text-red-700 hover:underline"
          >
            {m.nomeCompleto}
          </button>
        </div>
      ),
    },
    {
      chave: 'conselho',
      cabecalho: 'Conselho',
      render: (m) => (
        <span className="text-sm text-gray-900">
          {m.conselho}-{m.ufConselho} {m.registro}
        </span>
      ),
    },
    {
      chave: 'especialidade',
      cabecalho: 'Categoria / Especialidade',
      render: (m) => m.especialidade ?? <span className="text-gray-400">—</span>,
    },
    { chave: 'cpf', cabecalho: 'CPF', render: (m) => m.cpf },
    { chave: 'status', cabecalho: 'Status', render: (m) => <StatusBadge ativo={m.usuarioAtivo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (m) => (
        <div className="flex items-center justify-end gap-1">
          <BotaoLinhaAcao onClick={() => navigate(`/app/profissionais/${m.id}`)}>
            <Eye className="w-3.5 h-3.5" /> Ver
          </BotaoLinhaAcao>
          <BotaoLinhaAcao onClick={() => setEstado({ tipo: 'editar', id: m.id })}>
            <Pencil className="w-3.5 h-3.5" /> Editar
          </BotaoLinhaAcao>
          <BotaoLinhaAcao tom="perigo" onClick={() => setParaDesativar(m)}>
            <Trash2 className="w-3.5 h-3.5" /> Excluir
          </BotaoLinhaAcao>
        </div>
      ),
    },
  ];

  async function confirmarDesativar() {
    if (!paraDesativar) return;
    setErroAcao(null);
    try {
      await desativar.mutateAsync(paraDesativar.id);
      setParaDesativar(null);
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  const buscando = debounced.trim().length > 0;
  const semResultado = !busca.isLoading && (busca.data?.length ?? 0) === 0;
  // Aba ativa não-médica vira o conselho pré-selecionado ao cadastrar.
  const conselhoNovo = abaAtiva === 'TODOS' ? 'COREN' : abaAtiva;

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Profissionais de saúde</h1>
          <p className="mt-1 text-sm text-gray-600">
            Enfermagem, nutrição, odontologia e demais conselhos (não-médicos). Médicos ficam no
            menu <strong>Médicos</strong>. Sem busca, exibe os últimos cadastros; para procurar,
            digite <strong>nome</strong> ou <strong>CPF</strong>.
          </p>
        </div>
        <Button onClick={() => setEstado({ tipo: 'criar' })}>
          <Plus className="w-4 h-4" />
          Novo profissional
        </Button>
      </header>

      <div className="flex flex-wrap gap-1 border-b border-gray-200">
        {ABAS.map((a) => (
          <button
            key={a.sigla}
            type="button"
            onClick={() => setAbaAtiva(a.sigla)}
            className={
              'rounded-t-md px-3 py-2 text-sm font-medium transition-colors ' +
              (abaAtiva === a.sigla
                ? 'border-b-2 border-red-600 text-red-700'
                : 'text-gray-500 hover:text-gray-800')
            }
            title={a.nome}
          >
            {a.sigla === 'TODOS' ? 'Todos' : a.sigla}
          </button>
        ))}
      </div>

      <div className="relative">
        <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
        <Input
          autoFocus
          value={termo}
          onChange={(e) => setTermo(e.target.value)}
          placeholder="Buscar por nome ou CPF…"
          className="pl-9"
        />
      </div>

      {busca.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(busca.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={busca.data ?? []}
        chaveLinha={(m) => m.id}
        carregando={busca.isLoading || (busca.isFetching && !busca.data)}
        vazio={
          semResultado
            ? buscando
              ? 'Nenhum profissional encontrado para essa busca.'
              : 'Nenhum profissional cadastrado nesta categoria ainda.'
            : undefined
        }
      />

      <Modal
        aberto={estado.tipo !== 'fechado'}
        aoFechar={() => setEstado({ tipo: 'fechado' })}
        titulo={estado.tipo === 'criar' ? 'Novo profissional' : 'Editar profissional'}
        largura="lg"
      >
        {estado.tipo !== 'fechado' ? (
          <FormularioMedico
            modo={estado.tipo}
            idMedico={estado.tipo === 'editar' ? estado.id : null}
            conselhoInicial={conselhoNovo}
            permitirEscolherConselho
            substantivo="profissional"
            aoConcluir={() => setEstado({ tipo: 'fechado' })}
          />
        ) : null}
      </Modal>

      <ConfirmDialog
        aberto={Boolean(paraDesativar)}
        titulo="Excluir profissional"
        mensagem={
          paraDesativar
            ? `Excluir "${paraDesativar.nomeCompleto}"? O profissional some das listagens; histórico é preservado.`
            : ''
        }
        destrutivo
        rotuloConfirmar="Excluir"
        carregando={desativar.isPending}
        aoConfirmar={confirmarDesativar}
        aoCancelar={() => {
          setParaDesativar(null);
          setErroAcao(null);
        }}
      />

      {erroAcao ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroAcao}
        </div>
      ) : null}
    </div>
  );
}
