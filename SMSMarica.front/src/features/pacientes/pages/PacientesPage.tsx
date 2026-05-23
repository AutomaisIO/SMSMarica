import { useEffect, useMemo, useState } from 'react';
import { Eye, Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Avatar } from '@/shared/ui/Avatar';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Input } from '@/shared/ui/Input';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useBuscarPacientes,
  useDesativarPaciente,
} from '@/features/pacientes/api/queries';
import type { PacienteListItem } from '@/features/pacientes/types';

function useDebounce<T>(valor: T, ms = 300): T {
  const [debounced, setDebounced] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return debounced;
}

function formatarCpf(cpf: string): string {
  const d = cpf.replace(/\D/g, '');
  if (d.length !== 11) return cpf;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

function formatarData(iso?: string | null): string {
  if (!iso) return '—';
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

export function PacientesPage() {
  const navigate = useNavigate();
  const [termo, setTermo] = useState('');
  const debounced = useDebounce(termo, 300);
  const busca = useBuscarPacientes(debounced);
  const desativar = useDesativarPaciente();

  const [paraDesativar, setParaDesativar] = useState<PacienteListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const colunas: Coluna<PacienteListItem>[] = useMemo(() => [
    {
      chave: 'nome', cabecalho: 'Nome',
      render: (p) => (
        <div className="flex items-center gap-2">
          <Avatar src={p.fotoBase64} nome={p.nomeCompleto} tamanho="sm" />
          <button
            type="button"
            onClick={() => navigate(`/app/pacientes/${p.id}`)}
            className="text-left font-medium text-red-700 hover:underline"
          >
            {p.nomeCompleto}
          </button>
        </div>
      ),
    },
    { chave: 'cpf', cabecalho: 'CPF', render: (p) => formatarCpf(p.cpf) },
    { chave: 'nasc', cabecalho: 'Nascimento', render: (p) => formatarData(p.dataNascimento) },
    { chave: 'mae', cabecalho: 'Mãe', render: (p) => p.nomeDaMae ?? '—' },
    { chave: 'tel', cabecalho: 'Telefone', render: (p) => p.telefonePrincipal ?? '—' },
    { chave: 'status', cabecalho: 'Status', render: (p) => <StatusBadge ativo={p.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (p) => (
        <div className="flex items-center justify-end gap-1">
          <BotaoLinhaAcao onClick={() => navigate(`/app/pacientes/${p.id}`)}>
            <Eye className="h-3.5 w-3.5" /> Ver
          </BotaoLinhaAcao>
          <BotaoLinhaAcao onClick={() => navigate(`/app/pacientes/${p.id}/editar`)}>
            <Pencil className="h-3.5 w-3.5" /> Editar
          </BotaoLinhaAcao>
          {p.ativo ? (
            <BotaoLinhaAcao tom="perigo" onClick={() => setParaDesativar(p)}>
              <Trash2 className="h-3.5 w-3.5" /> Excluir
            </BotaoLinhaAcao>
          ) : null}
        </div>
      ),
    },
  ], [navigate]);

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
          <h1 className="text-2xl font-semibold text-gray-900">Pacientes</h1>
          <p className="mt-1 text-sm text-gray-600">
            Sem busca, exibe os <strong>10 últimos cadastros</strong>. Para procurar, digite{' '}
            <strong>nome</strong> (qualquer parte, separadas por espaço) ou <strong>CPF</strong>{' '}
            (com ou sem formatação) — até 10 resultados.
          </p>
        </div>
        <Button onClick={() => navigate('/app/pacientes/novo')}>
          <Plus className="h-4 w-4" />
          Novo paciente
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
        chaveLinha={(p) => p.id}
        carregando={busca.isLoading || (busca.isFetching && !busca.data)}
        vazio={
          semResultado
            ? buscando
              ? 'Nenhum paciente encontrado para essa busca.'
              : 'Nenhum paciente cadastrado ainda. Use "Novo paciente" para começar.'
            : undefined
        }
      />

      <ConfirmDialog
        aberto={Boolean(paraDesativar)}
        titulo="Excluir paciente"
        mensagem={
          paraDesativar
            ? `Excluir "${paraDesativar.nomeCompleto}"? O cadastro deixa de aparecer nas buscas, mas o histórico é preservado e pode ser reativado entrando com o CPF.`
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
