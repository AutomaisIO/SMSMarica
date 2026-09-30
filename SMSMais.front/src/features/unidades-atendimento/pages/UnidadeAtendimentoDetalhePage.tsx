import type { ReactNode } from 'react';
import { ArrowLeft, MapPinOff, Pencil, Phone } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao, useTemConsulta } from '@/shared/auth/authStore';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { MapaSeletor } from '@/shared/ui/MapaSeletor';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useUnidadeAtendimento } from '@/features/unidades-atendimento/api/queries';
import { enderecoEmLinha } from '@/features/unidades-atendimento/lib/formatar';
import { useListarTratamentos } from '@/features/tratamentos/api/queries';
import { formatarDataBr } from '@/features/tratamentos/lib/agenda';
import { formatarDuracao } from '@/shared/lib/tempoMedio';
import type { TratamentoListItem } from '@/features/tratamentos/types';

export function UnidadeAtendimentoDetalhePage() {
  const navigate = useNavigate();
  const { id = '' } = useParams<{ id: string }>();
  const detalhe = useUnidadeAtendimento(id || null);
  const podeEditar = usePermissao('UnidadesAtendimento', 'Edicao');
  // A lista de tratamentos é de outro módulo: sem ele, a seção nem aparece (evita 403 solto).
  const podeVerTratamentos = useTemConsulta('Tratamentos');
  const tratamentos = useListarTratamentos(
    { unidadeAtendimentoId: id },
    { habilitado: podeVerTratamentos && Boolean(id) },
  );

  if (detalhe.isLoading) {
    return <p className="text-sm text-gray-500">Carregando unidade de atendimento…</p>;
  }
  if (detalhe.isError || !detalhe.data) {
    return (
      <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {extrairMensagemDeErro(detalhe.error) || 'Unidade de atendimento não encontrada.'}
      </div>
    );
  }

  const u = detalhe.data;
  const coordenada = u.latitude != null && u.longitude != null ? { lat: u.latitude, lng: u.longitude } : null;

  const colunas: Coluna<TratamentoListItem>[] = [
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      render: (t) => <span className="font-medium text-gray-900">{t.pacienteNome}</span>,
    },
    { chave: 'tipo', cabecalho: 'Tipo', render: (t) => t.tipoTratamentoNome ?? t.descricao },
    { chave: 'tempo', cabecalho: 'Tempo médio', render: (t) => formatarDuracao(t.tempoMedioMinutos) },
    {
      chave: 'proxima',
      cabecalho: 'Próxima sessão',
      render: (t) => (t.proximaSessao ? formatarDataBr(t.proximaSessao) : '—'),
    },
    {
      chave: 'progresso',
      cabecalho: 'Progresso',
      render: (t) => (
        <span className="text-xs text-gray-600">
          {t.sessoesRealizadas}/{t.totalSessoes}
        </span>
      ),
    },
    { chave: 'status', cabecalho: 'Status', render: (t) => <StatusBadge ativo={t.ativo} /> },
  ];

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <button
            type="button"
            onClick={() => navigate('/app/unidades-atendimento')}
            className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
            aria-label="Voltar"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-semibold text-gray-900">{u.nome}</h1>
              <StatusBadge ativo={u.ativo} />
              {u.externa ? <span className="badge badge-gray">Fora do município</span> : null}
              <AjudaManual artigo="unidades-atendimento" />
            </div>
            <p className="mt-1 text-sm text-gray-600">{enderecoEmLinha(u.endereco)}</p>
          </div>
        </div>
        {podeEditar && u.ativo ? (
          <Button variante="outline" onClick={() => navigate(`/app/unidades-atendimento/${u.id}/editar`)}>
            <Pencil className="h-4 w-4" /> Editar
          </Button>
        ) : null}
      </header>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <section className="space-y-4 rounded-lg border border-gray-200 bg-white p-6 shadow-sm lg:col-span-1">
          <Dado rotulo="Ponto de referência" valor={u.endereco?.pontoReferencia} />
          <Dado
            rotulo="Telefone"
            valor={
              u.telefone ? (
                <span className="inline-flex items-center gap-1.5">
                  <Phone className="h-3.5 w-3.5 text-gray-400" /> {u.telefone}
                </span>
              ) : null
            }
          />
          <Dado rotulo="Observações para o motorista" valor={u.observacoes} />
          <Dado rotulo="Atendimentos ativos" valor={String(u.tratamentosAtivos)} />
        </section>

        <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm lg:col-span-2">
          <h2 className="mb-3 text-base font-semibold text-gray-900">Ponto de chegada da van</h2>
          {coordenada ? (
            <MapaSeletor valor={coordenada} aoMudar={() => undefined} altura={320} desabilitado />
          ) : (
            <p className="inline-flex items-center gap-2 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">
              <MapPinOff className="h-4 w-4" /> Sem coordenada — a rota não tem para onde ir. Edite e marque no mapa.
            </p>
          )}
        </section>
      </div>

      {podeVerTratamentos ? (
        <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-3 text-base font-semibold text-gray-900">Atendimentos com destino nesta unidade</h2>
          {tratamentos.isError ? (
            <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {extrairMensagemDeErro(tratamentos.error)}
            </div>
          ) : null}
          <Tabela
            colunas={colunas}
            dados={tratamentos.data ?? []}
            chaveLinha={(t) => t.id}
            carregando={tratamentos.isLoading}
            aoClicarLinha={(t) => navigate(`/app/tratamentos/${t.id}`)}
            dicaLinha="Abrir o atendimento"
            vazio="Nenhum atendimento aponta para esta unidade."
          />
        </section>
      ) : null}
    </div>
  );
}

function Dado({ rotulo, valor }: { rotulo: string; valor: ReactNode }) {
  return (
    <div>
      <p className="text-xs uppercase tracking-wide text-gray-500">{rotulo}</p>
      <div className="mt-0.5 whitespace-pre-line text-sm text-gray-900">{valor || '—'}</div>
    </div>
  );
}
