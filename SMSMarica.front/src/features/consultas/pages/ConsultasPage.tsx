import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowDownToLine, ArrowUpFromLine, CalendarClock, Search, Stethoscope } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstanteData, hojeSP } from '@/shared/lib/datas';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { TextoLimitado } from '@/shared/ui/TextoLimitado';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { ChecksComunicacao } from '@/features/solicitacoes-exame/components/ChecksComunicacao';
import { useListarConsultas } from '@/features/consultas/api/queries';
import type { ConsultaListItem, FiltroConsultas } from '@/features/consultas/types';

const CHAVE_TOGGLE_HOJE = 'consultas:filtro-hoje';

const ROTULO_CATEGORIA: Record<string, string> = {
  Consulta: 'Consulta',
  Laboratorio: 'Laboratório',
  GraficoFuncional: 'Gráfico/funcional',
  Endoscopia: 'Endoscopia',
  Cirurgia: 'Cirurgia',
  Outro: 'Outro',
};

function DirecaoIcone({ direcao }: { direcao: 'Recebida' | 'Enviada' | null }) {
  if (direcao === 'Recebida')
    return (
      <span title="Recebida — sua unidade é a executora desta consulta" aria-label="Recebida">
        <ArrowDownToLine className="h-4 w-4 text-emerald-600" />
      </span>
    );
  if (direcao === 'Enviada')
    return (
      <span title="Enviada — sua unidade solicitou esta consulta" aria-label="Enviada">
        <ArrowUpFromLine className="h-4 w-4 text-sky-600" />
      </span>
    );
  return null;
}

const STATUS_COR: Record<string, string> = {
  Solicitada: 'bg-amber-50 text-amber-700 ring-amber-200',
  Realizada: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  Cancelada: 'bg-red-50 text-red-700 ring-red-200',
};

function StatusConsultaBadge({ status }: { status: string }) {
  const cor = STATUS_COR[status] ?? 'bg-gray-100 text-gray-700 ring-gray-200';
  return (
    <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${cor}`}>
      {status}
    </span>
  );
}

function CategoriaBadge({ categoria }: { categoria: string }) {
  const cor =
    categoria === 'Consulta'
      ? 'bg-blue-50 text-blue-700 ring-blue-200'
      : categoria === 'Cirurgia'
        ? 'bg-red-50 text-red-700 ring-red-200'
        : 'bg-gray-100 text-gray-700 ring-gray-200';
  return (
    <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${cor}`}>
      {ROTULO_CATEGORIA[categoria] ?? categoria}
    </span>
  );
}

export function ConsultasPage() {
  const [busca, setBusca] = useState('');
  const [buscaAplicada, setBuscaAplicada] = useState('');
  const [dataInicial, setDataInicial] = useState('');
  const [dataFinal, setDataFinal] = useState('');
  const [status, setStatus] = useState('');
  const [limite, setLimite] = useState(200);
  const [hojeAtivo, setHojeAtivo] = useState<boolean>(() => localStorage.getItem(CHAVE_TOGGLE_HOJE) === '1');
  const navigate = useNavigate();

  // Busca pontual ignora período (mesma régua de Exames); Hoje fixa o dia atual; senão, o range.
  const filtro: FiltroConsultas = buscaAplicada
    ? { busca: buscaAplicada, status: status || undefined, limite }
    : hojeAtivo
      ? { dataInicial: hojeSP(), dataFinal: hojeSP(), status: status || undefined, limite }
      : {
          dataInicial: dataInicial || undefined,
          dataFinal: dataFinal || undefined,
          status: status || undefined,
          limite,
        };

  const lista = useListarConsultas(filtro);

  // Enquanto "Hoje" estiver ligado, mantém a data atual (vira o dia à meia-noite).
  useEffect(() => {
    if (!hojeAtivo) return;
    setDataInicial(hojeSP());
    setDataFinal(hojeSP());
  }, [hojeAtivo]);

  function alternarHoje() {
    setHojeAtivo((atual) => {
      const proximo = !atual;
      localStorage.setItem(CHAVE_TOGGLE_HOJE, proximo ? '1' : '0');
      return proximo;
    });
  }

  // Mesma estrutura de colunas da lista de Exames: Pedido (seta + nº) · Paciente (nome + "Por") ·
  // Consulta (especialidade + natureza + unidade) · Data Agendamento · Situação (status + checks).
  const colunas: Coluna<ConsultaListItem>[] = [
    {
      chave: 'pedido',
      cabecalho: 'Pedido',
      className: 'w-44 whitespace-nowrap',
      render: (c) => (
        <div className="flex items-start gap-1.5">
          <DirecaoIcone direcao={c.direcao} />
          <div className="min-w-0 text-xs text-gray-500">
            {c.codigoSolicitacao ? <span className="font-mono">SISREG {c.codigoSolicitacao}</span> : '—'}
          </div>
        </div>
      ),
    },
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      render: (c) => (
        <div className="min-w-0">
          <NomePacienteComResumo
            pacienteId={c.pacienteId}
            nome={c.pacienteNome}
            className="min-w-0"
            classNameNome="truncate font-medium text-gray-900"
          />
          {c.solicitanteNome ? <div className="truncate text-xs text-gray-500">Por {c.solicitanteNome}</div> : null}
        </div>
      ),
    },
    {
      chave: 'consulta',
      cabecalho: 'Consulta',
      render: (c) => (
        <div className="min-w-0">
          <TextoLimitado texto={c.especialidade} max={40} className="block text-gray-900" />
          <div className="flex items-center gap-1.5 truncate text-xs text-gray-500">
            <CategoriaBadge categoria={c.categoria} />
            {c.unidadeExecutanteNome ? <span className="truncate">{c.unidadeExecutanteNome}</span> : null}
          </div>
        </div>
      ),
    },
    {
      chave: 'data',
      cabecalho: 'Data Agendamento',
      className: 'w-40 whitespace-nowrap',
      render: (c) => formatarInstanteData(c.dataAgendada) || '—',
    },
    {
      chave: 'situacao',
      cabecalho: 'Situação',
      className: 'w-44 whitespace-nowrap',
      render: (c) => (
        <span className="inline-flex items-center gap-1.5">
          <StatusConsultaBadge status={c.status} />
          {c.chipConfirmacao ? (
            <ChecksComunicacao chip={c.chipConfirmacao} finalidade="ConfirmacaoAgendamento" />
          ) : null}
        </span>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <Stethoscope className="h-6 w-6 text-primary-600" />
            Consultas
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            Solicitações de consulta importadas do SISREG (sem imagem/laudo).
          </p>
        </div>
      </header>

      <form
        className="flex flex-wrap items-end gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          setBuscaAplicada(busca.trim());
        }}
      >
        <Campo label="Buscar" htmlFor="consulta-busca">
          <Input
            id="consulta-busca"
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Nome, CPF/CNS, nº do pedido ou especialidade"
            className="w-80"
          />
        </Campo>
        <Campo label="De" htmlFor="consulta-di">
          <Input
            id="consulta-di"
            type="date"
            value={dataInicial}
            disabled={hojeAtivo || !!buscaAplicada}
            onChange={(e) => setDataInicial(e.target.value)}
          />
        </Campo>
        <Campo label="Até" htmlFor="consulta-df">
          <Input
            id="consulta-df"
            type="date"
            value={dataFinal}
            disabled={hojeAtivo || !!buscaAplicada}
            onChange={(e) => setDataFinal(e.target.value)}
          />
        </Campo>
        <Campo label="Status" htmlFor="consulta-status">
          <select
            id="consulta-status"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
            className="rounded-md border border-gray-300 px-2 py-2 text-sm"
          >
            <option value="">Todos</option>
            <option value="Solicitada">Solicitada</option>
            <option value="Realizada">Realizada</option>
            <option value="Cancelada">Cancelada</option>
          </select>
        </Campo>
        <button
          type="button"
          onClick={alternarHoje}
          aria-pressed={hojeAtivo}
          className={`inline-flex items-center gap-1 rounded-md border px-3 py-2 text-sm font-medium ${
            hojeAtivo
              ? 'border-primary-600 bg-primary-50 text-primary-700'
              : 'border-gray-300 bg-white text-gray-700 hover:bg-gray-50'
          }`}
        >
          <CalendarClock className="h-4 w-4" />
          Hoje
        </button>
        <button
          type="submit"
          className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
        >
          <Search className="h-4 w-4" />
          Buscar
        </button>
        {buscaAplicada ? (
          <button
            type="button"
            onClick={() => {
              setBusca('');
              setBuscaAplicada('');
            }}
            className="inline-flex items-center rounded-md px-2 py-2 text-sm text-gray-500 hover:text-gray-700"
          >
            Limpar
          </button>
        ) : null}
        <label className="ml-auto flex items-center gap-1 text-xs text-gray-500">
          Itens
          <select
            value={limite}
            onChange={(e) => setLimite(Number(e.target.value))}
            className="rounded-md border border-gray-300 px-2 py-1 text-sm"
          >
            {[50, 100, 200, 500].map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
        </label>
      </form>

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(c) => c.id}
        carregando={lista.isPending}
        vazio="Nenhuma consulta encontrada."
        aoClicarLinha={(c) => navigate(`/app/consultas/${c.id}`)}
        layoutFixo
      />
    </div>
  );
}
