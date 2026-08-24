import { useEffect, useState } from 'react';
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
import {
  useBuscarMedicos,
  useDesativarMedico,
} from '@/features/medicos/api/queries';
import { FormularioMedico } from '@/features/medicos/components/FormularioMedico';
import type { MedicoListItem } from '@/features/medicos/types';

type EstadoModal = { tipo: 'fechado' } | { tipo: 'criar' } | { tipo: 'editar'; id: string };

function useDebounce<T>(valor: T, ms = 300): T {
  const [debounced, setDebounced] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return debounced;
}

export function MedicosPage() {
  const navigate = useNavigate();
  const [termo, setTermo] = useState('');
  const debounced = useDebounce(termo, 300);
  const busca = useBuscarMedicos(debounced, { conselho: 'CRM' });
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
            onClick={() => navigate(`/app/medicos/${m.id}`)}
            className="text-left font-medium text-red-700 hover:underline"
          >
            {m.nomeCompleto}
          </button>
        </div>
      ),
    },
    {
      chave: 'registro',
      cabecalho: 'CRM',
      render: (m) => (
        <span className="text-sm text-gray-900">
          {m.registro}/{m.ufConselho}
        </span>
      ),
    },
    {
      chave: 'especialidade',
      cabecalho: 'Especialidade',
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
          <BotaoLinhaAcao onClick={() => navigate(`/app/medicos/${m.id}`)}>
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

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Médicos</h1>
          <p className="mt-1 text-sm text-gray-600">
            Sem busca, exibe os <strong>10 últimos cadastros</strong>. Para procurar, digite{' '}
            <strong>nome</strong> (qualquer parte, separadas por espaço) ou <strong>CPF</strong>{' '}
            (com ou sem formatação) — até 10 resultados.
          </p>
        </div>
        <Button onClick={() => setEstado({ tipo: 'criar' })}>
          <Plus className="w-4 h-4" />
          Novo médico
        </Button>
      </header>

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
              ? 'Nenhum médico encontrado para essa busca.'
              : 'Nenhum médico cadastrado ainda. Use "Novo médico" para começar.'
            : undefined
        }
      />

      <Modal
        aberto={estado.tipo !== 'fechado'}
        aoFechar={() => setEstado({ tipo: 'fechado' })}
        titulo={estado.tipo === 'criar' ? 'Novo médico' : 'Editar médico'}
        largura="lg"
      >
        {estado.tipo !== 'fechado' ? (
          <FormularioMedico
            modo={estado.tipo}
            idMedico={estado.tipo === 'editar' ? estado.id : null}
            aoConcluir={() => setEstado({ tipo: 'fechado' })}
          />
        ) : null}
      </Modal>

      <ConfirmDialog
        aberto={Boolean(paraDesativar)}
        titulo="Excluir médico"
        mensagem={
          paraDesativar
            ? `Excluir "${paraDesativar.nomeCompleto}"? O médico some das listagens; histórico de atendimentos é preservado.`
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
