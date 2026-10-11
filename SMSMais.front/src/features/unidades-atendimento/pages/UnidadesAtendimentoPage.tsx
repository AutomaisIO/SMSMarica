import { useMemo, useState } from 'react';
import { Hospital, MapPinOff, Pencil, Plus, RotateCcw, Search, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Input } from '@/shared/ui/Input';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useDesativarUnidadeAtendimento,
  useListarUnidadesAtendimento,
  useReativarUnidadeAtendimento,
} from '@/features/unidades-atendimento/api/queries';
import type { UnidadeAtendimentoListItem } from '@/features/unidades-atendimento/types';
import { cidadeUf, semAcento } from '@/features/unidades-atendimento/lib/formatar';

export function UnidadesAtendimentoPage() {
  const navigate = useNavigate();
  const [mostrarDesativadas, setMostrarDesativadas] = useState(false);
  const [busca, setBusca] = useState('');
  const lista = useListarUnidadesAtendimento(mostrarDesativadas);
  const desativar = useDesativarUnidadeAtendimento();
  const reativar = useReativarUnidadeAtendimento();
  const podeIncluir = usePermissao('UnidadesAtendimento', 'Inclusao');
  const podeEditar = usePermissao('UnidadesAtendimento', 'Edicao');
  const podeExcluir = usePermissao('UnidadesAtendimento', 'Exclusao');
  const [paraDesativar, setParaDesativar] = useState<UnidadeAtendimentoListItem | null>(null);
  const [erroAcao, setErroAcao] = useState<string | null>(null);

  const visiveis = useMemo(() => {
    const termo = semAcento(busca.trim());
    const dados = lista.data ?? [];
    if (!termo) return dados;
    return dados.filter((u) =>
      [u.nome, u.bairro, u.cidade, u.logradouro].some((c) => c && semAcento(c).includes(termo)),
    );
  }, [lista.data, busca]);

  const colunas: Coluna<UnidadeAtendimentoListItem>[] = [
    {
      chave: 'nome',
      cabecalho: 'Unidade',
      ordenar: (u) => u.nome,
      render: (u) => (
        <div>
          <span className="font-medium text-gray-900">{u.nome}</span>
          {u.logradouro ? (
            <p className="text-xs text-gray-500">
              {u.logradouro}
              {u.numero ? `, ${u.numero}` : ''}
              {u.bairro ? ` — ${u.bairro}` : ''}
            </p>
          ) : null}
        </div>
      ),
    },
    {
      chave: 'cidade',
      cabecalho: 'Cidade/UF',
      ordenar: (u) => u.cidade,
      render: (u) => (
        <div className="flex flex-wrap items-center gap-1.5">
          <span>{cidadeUf(u.cidade, u.uf)}</span>
          {u.externa ? <span className="badge badge-gray">Fora do município</span> : null}
        </div>
      ),
    },
    {
      chave: 'mapa',
      cabecalho: 'Localização',
      render: (u) =>
        u.temCoordenada ? (
          <span className="text-xs text-gray-600">No mapa</span>
        ) : (
          <span
            className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800"
            title="Sem coordenada, a rota não tem ponto de chegada. Edite e marque no mapa."
          >
            <MapPinOff className="h-3 w-3" /> Sem coordenada
          </span>
        ),
    },
    {
      chave: 'tratamentos',
      cabecalho: 'Atendimentos ativos',
      className: 'text-right',
      ordenar: (u) => u.tratamentosAtivos,
      render: (u) => <span className="tabular-nums">{u.tratamentosAtivos}</span>,
    },
    { chave: 'status', cabecalho: 'Status', render: (u) => <StatusBadge ativo={u.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (u) => (
        <div className="flex items-center justify-end gap-1">
          {podeEditar && u.ativo ? (
            <BotaoLinhaAcao onClick={() => navigate(`/app/unidades-atendimento/${u.id}/editar`)}>
              <Pencil className="h-3.5 w-3.5" /> Editar
            </BotaoLinhaAcao>
          ) : null}
          {podeExcluir && u.ativo ? (
            <BotaoLinhaAcao
              tom="perigo"
              onClick={() => setParaDesativar(u)}
              disabled={u.tratamentosAtivos > 0}
              title={
                u.tratamentosAtivos > 0
                  ? 'Há atendimento ativo indo para esta unidade. Encerre-o ou troque o destino antes de desativar.'
                  : undefined
              }
              className="disabled:cursor-not-allowed disabled:opacity-50"
            >
              <Trash2 className="h-3.5 w-3.5" /> Desativar
            </BotaoLinhaAcao>
          ) : null}
          {podeEditar && !u.ativo ? (
            <BotaoLinhaAcao onClick={() => reativarUnidade(u)} disabled={reativar.isPending}>
              <RotateCcw className="h-3.5 w-3.5" /> Reativar
            </BotaoLinhaAcao>
          ) : null}
        </div>
      ),
    },
  ];

  async function reativarUnidade(u: UnidadeAtendimentoListItem) {
    setErroAcao(null);
    try {
      await reativar.mutateAsync(u.id);
    } catch (e) {
      setErroAcao(extrairMensagemDeErro(e));
    }
  }

  async function confirmarDesativar() {
    if (!paraDesativar) return;
    setErroAcao(null);
    try {
      await desativar.mutateAsync(paraDesativar.id);
    } catch (e) {
      // O diálogo não tem espaço para erro: fecha e o motivo aparece no aviso acima da tabela,
      // em vez de ficar escondido atrás do modal.
      setErroAcao(extrairMensagemDeErro(e));
    } finally {
      setParaDesativar(null);
    }
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <Hospital className="mt-1 h-6 w-6 text-red-600" />
          <div>
            <div className="flex items-center gap-1.5">
              <h1 className="text-2xl font-semibold text-gray-900">Unidades de Atendimento</h1>
              <AjudaManual artigo="unidades-atendimento" />
            </div>
            <p className="mt-1 text-sm text-gray-600">
              Destinos do transporte: onde o paciente é atendido. O endereço e o ponto no mapa são o
              fim da rota calculada para a van.
            </p>
          </div>
        </div>
        {podeIncluir ? (
          <Button onClick={() => navigate('/app/unidades-atendimento/novo')}>
            <Plus className="h-4 w-4" />
            Nova unidade de atendimento
          </Button>
        ) : null}
      </header>

      <div className="flex flex-wrap items-center gap-4">
        <div className="relative w-full max-w-sm">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
          <Input
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Buscar por nome, bairro, cidade ou rua"
            className="pl-9"
            aria-label="Buscar unidade de atendimento"
          />
        </div>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={mostrarDesativadas}
            onChange={(e) => setMostrarDesativadas(e.target.checked)}
          />
          Mostrar desativadas
        </label>
      </div>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      {erroAcao ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroAcao}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={visiveis}
        chaveLinha={(u) => u.id}
        carregando={lista.isLoading}
        aoClicarLinha={(u) => navigate(`/app/unidades-atendimento/${u.id}`)}
        dicaLinha="Clique para ver endereço, mapa e atendimentos"
        vazio={
          busca.trim()
            ? 'Nenhuma unidade de atendimento com esse termo.'
            : 'Nenhuma unidade de atendimento cadastrada ainda. Cadastre os destinos antes dos atendimentos.'
        }
      />

      <ConfirmDialog
        aberto={Boolean(paraDesativar)}
        titulo="Desativar unidade de atendimento"
        mensagem={
          paraDesativar
            ? `Desativar "${paraDesativar.nome}"? Ela sai das opções de destino do atendimento; o histórico é preservado e dá para reativar depois.`
            : ''
        }
        destrutivo
        rotuloConfirmar="Desativar"
        carregando={desativar.isPending}
        aoConfirmar={confirmarDesativar}
        aoCancelar={() => {
          setParaDesativar(null);
          setErroAcao(null);
        }}
      />
    </div>
  );
}
