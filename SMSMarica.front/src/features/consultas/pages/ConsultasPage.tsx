import { useEffect, useState } from 'react';
import { CalendarClock, Search, Stethoscope } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstanteData, hojeSP } from '@/shared/lib/datas';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { useListarConsultas, useObterConsulta } from '@/features/consultas/api/queries';
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
  const [limite, setLimite] = useState(200);
  const [hojeAtivo, setHojeAtivo] = useState<boolean>(() => localStorage.getItem(CHAVE_TOGGLE_HOJE) === '1');
  const [detalheId, setDetalheId] = useState<string | null>(null);

  // Busca pontual ignora período (mesma régua de Exames); Hoje fixa o dia atual; senão, o range.
  const filtro: FiltroConsultas = buscaAplicada
    ? { busca: buscaAplicada, limite }
    : hojeAtivo
      ? { dataInicial: hojeSP(), dataFinal: hojeSP(), limite }
      : { dataInicial: dataInicial || undefined, dataFinal: dataFinal || undefined, limite };

  const lista = useListarConsultas(filtro);
  const detalhe = useObterConsulta(detalheId);

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

  const colunas: Coluna<ConsultaListItem>[] = [
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
          <div className="truncate text-xs text-gray-500">
            {c.codigoSolicitacao ? `Nº ${c.codigoSolicitacao}` : ''}
            {c.solicitanteNome ? `${c.codigoSolicitacao ? ' · ' : ''}Por ${c.solicitanteNome}` : ''}
          </div>
        </div>
      ),
    },
    { chave: 'esp', cabecalho: 'Especialidade / procedimento', render: (c) => c.especialidade ?? '—' },
    { chave: 'cat', cabecalho: 'Natureza', render: (c) => <CategoriaBadge categoria={c.categoria} /> },
    { chave: 'uni', cabecalho: 'Unidade', render: (c) => c.unidadeExecutanteNome || '—' },
    { chave: 'data', cabecalho: 'Agendada', render: (c) => formatarInstanteData(c.dataAgendada) || '—' },
    { chave: 'conf', cabecalho: 'Confirmação', render: (c) => c.statusConfirmacao },
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
        aoClicarLinha={(c) => setDetalheId(c.id)}
      />

      <Modal aberto={!!detalheId} aoFechar={() => setDetalheId(null)} titulo="Detalhe da consulta">
        {detalhe.isPending ? (
          <p className="text-sm text-gray-500">Carregando…</p>
        ) : detalhe.data ? (
          <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
            <Item rotulo="Paciente" valor={detalhe.data.pacienteNome} />
            <Item rotulo="CPF" valor={detalhe.data.pacienteCpf} />
            <Item rotulo="Natureza" valor={ROTULO_CATEGORIA[detalhe.data.categoria] ?? detalhe.data.categoria} />
            <Item rotulo="Especialidade" valor={detalhe.data.especialidade} />
            <Item rotulo="Nº do pedido" valor={detalhe.data.codigoSolicitacao} />
            <Item rotulo="SIGTAP" valor={detalhe.data.procedimentoSigtapCodigo} />
            <Item rotulo="Unidade executante" valor={detalhe.data.unidadeExecutanteNome} />
            <Item rotulo="Unidade solicitante" valor={detalhe.data.unidadeSolicitanteNome} />
            <Item rotulo="Solicitante" valor={detalhe.data.solicitanteNome} />
            <Item rotulo="Agendada" valor={formatarInstanteData(detalhe.data.dataAgendada)} />
            <Item rotulo="Solicitação" valor={detalhe.data.dataSolicitacao} />
            <Item rotulo="Regulação" valor={detalhe.data.dataRegulacao} />
            <Item rotulo="Confirmação" valor={detalhe.data.statusConfirmacao} />
            <Item rotulo="Status" valor={detalhe.data.status} />
            {detalhe.data.observacoes ? (
              <div className="col-span-2">
                <Item rotulo="Observações" valor={detalhe.data.observacoes} />
              </div>
            ) : null}
          </dl>
        ) : (
          <p className="text-sm text-gray-500">Não encontrada.</p>
        )}
        <div className="mt-4 flex items-center gap-2 text-xs text-gray-500">
          <CalendarClock className="h-3.5 w-3.5" />
          Consulta importada do SISREG — não gera exame de imagem nem laudo.
        </div>
      </Modal>
    </div>
  );
}

function Item({ rotulo, valor }: { rotulo: string; valor: string | null | undefined }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-gray-400">{rotulo}</dt>
      <dd className="text-gray-900">{valor || '—'}</dd>
    </div>
  );
}
