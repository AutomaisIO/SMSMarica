import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ClipboardList, FileDown, Loader2, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante, formatarInstanteData, hojeSP } from '@/shared/lib/datas';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Paginacao } from '@/shared/ui/Paginacao';
import { Select } from '@/shared/ui/Select';
import { SelecaoMultipla } from '@/shared/ui/SelecaoMultipla';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { ModalSolicitacao } from '@/features/solicitacoes/components/ModalSolicitacao';
import { ROTULO_CATEGORIA } from '@/features/solicitacoes/components/CategoriaBadge';
import {
  SITUACOES_SISREG,
  baixarPdfAgendamentosBase,
  buscarAgendamentosBase,
  mensagemErroPdfBase,
  obterOpcoesConsultaBase,
  rotuloSituacao,
  type AgendamentoBaseSisreg,
  type EixoDataConsultaSisreg,
  type FiltroConsultaBase,
  type SituacaoAgendamentoSisreg,
} from '@/features/sisreg/api/sisregBaseApi';

const TAMANHOS = [100, 200, 500] as const;

const COR_SITUACAO: Record<SituacaoAgendamentoSisreg, string> = {
  NaFila: 'bg-slate-100 text-slate-700',
  Agendada: 'bg-blue-50 text-blue-700',
  Pendente: 'bg-amber-100 text-amber-800',
  Compareceu: 'bg-emerald-50 text-emerald-700',
  Faltou: 'bg-red-50 text-red-700',
  Cancelada: 'bg-gray-100 text-gray-500',
};

function SituacaoBadge({ situacao }: { situacao: SituacaoAgendamentoSisreg }) {
  return (
    <span className={`inline-flex whitespace-nowrap rounded px-2 py-0.5 text-xs font-medium ${COR_SITUACAO[situacao]}`}>
      {rotuloSituacao(situacao)}
    </span>
  );
}

const numero = (n: number) => n.toLocaleString('pt-BR');

/**
 * SISREG → Consultar: os agendamentos do SISREG que já estão na NOSSA base (importação +
 * sincronismo diário), com a situação de cada um — inclusive "Pendente de atualização", quando a
 * data passou e a unidade não apontou nem chegada nem falta. Não fala com o SISREG.
 *
 * O filtro só vale ao clicar em Pesquisar (mexer nas listas não dispara consulta a cada clique);
 * página e tamanho mudam na hora, sobre o filtro já aplicado.
 */
export function SisregConsultaPage() {
  const hoje = hojeSP();
  const [inicio, setInicio] = useState(`${hoje.slice(0, 8)}01`);
  const [fim, setFim] = useState(hoje);
  const [eixo, setEixo] = useState<EixoDataConsultaSisreg>('Agendamento');
  const [unidadeIds, setUnidadeIds] = useState<string[]>([]);
  const [situacoes, setSituacoes] = useState<SituacaoAgendamentoSisreg[]>([]);
  const [procedimentos, setProcedimentos] = useState<string[]>([]);
  const [incluirExames, setIncluirExames] = useState(true);
  const [incluirConsultas, setIncluirConsultas] = useState(true);

  const [aplicado, setAplicado] = useState<FiltroConsultaBase | null>(null);
  const [detalheId, setDetalheId] = useState<string | null>(null);
  const [exportando, setExportando] = useState(false);
  const [erroPdf, setErroPdf] = useState<string | null>(null);

  const periodoValido = Boolean(inicio && fim && inicio <= fim);

  const opcoes = useQuery({
    queryKey: ['sisreg-base', 'opcoes', inicio, fim, eixo],
    queryFn: () => obterOpcoesConsultaBase(inicio, fim, eixo),
    enabled: periodoValido,
    staleTime: 5 * 60_000,
    placeholderData: keepPreviousData,
  });

  const resultado = useQuery({
    queryKey: ['sisreg-base', 'agendamentos', aplicado],
    queryFn: () => buscarAgendamentosBase(aplicado!),
    enabled: aplicado !== null,
    placeholderData: keepPreviousData,
  });

  function pesquisar(e: React.FormEvent) {
    e.preventDefault();
    setErroPdf(null);
    setAplicado({
      inicio,
      fim,
      eixo,
      unidadeIds,
      situacoes,
      procedimentos,
      incluirExames,
      incluirConsultas,
      pagina: 1,
      tamanho: aplicado?.tamanho ?? 100,
    });
  }

  async function exportarPdf() {
    if (!aplicado) return;
    setErroPdf(null);
    setExportando(true);
    try {
      await baixarPdfAgendamentosBase({ ...aplicado, pagina: 1 });
    } catch (err) {
      setErroPdf(await mensagemErroPdfBase(err));
    } finally {
      setExportando(false);
    }
  }

  const dados = resultado.data;
  const semTipo = !incluirExames && !incluirConsultas;

  const colunas: Coluna<AgendamentoBaseSisreg>[] = [
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      render: (r) => (
        <div className="min-w-0">
          <div className="truncate font-medium text-gray-900">{r.pacienteNome ?? '(não encontrado no cadastro)'}</div>
          {r.pacienteCpf ? <div className="truncate text-xs text-gray-500">CPF {r.pacienteCpf}</div> : null}
        </div>
      ),
    },
    {
      chave: 'data',
      cabecalho: aplicado?.eixo === 'Solicitacao' ? 'Agendamento / solicitação' : 'Agendamento',
      render: (r) => (
        <div className="whitespace-nowrap">
          <div className="text-gray-900">{r.dataAgendada ? formatarInstante(r.dataAgendada) : 'Sem data'}</div>
          {aplicado?.eixo === 'Solicitacao' ? (
            <div className="text-xs text-gray-500">pedido em {formatarInstanteData(r.dataSolicitacao)}</div>
          ) : null}
        </div>
      ),
    },
    {
      chave: 'procedimento',
      cabecalho: 'Procedimento',
      render: (r) => (
        <div className="min-w-0">
          <div className="text-gray-900">{r.procedimento}</div>
          <div className="text-xs text-gray-500">{ROTULO_CATEGORIA[r.categoria] ?? r.categoria}</div>
        </div>
      ),
    },
    { chave: 'unidade', cabecalho: 'Unidade executante', render: (r) => r.unidadeExecutante ?? '—' },
    { chave: 'situacao', cabecalho: 'Situação', render: (r) => <SituacaoBadge situacao={r.situacao} /> },
    {
      chave: 'codigo',
      cabecalho: 'Solicitação',
      render: (r) => <span className="font-mono text-xs text-gray-600">{r.codigoSolicitacao ?? '—'}</span>,
    },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <ClipboardList className="h-6 w-6 text-primary-600" />
          Consultar SISREG
          <AjudaManual artigo="sisreg-consultar" />
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Agendamentos do SISREG que já estão na nossa base (importação e sincronismo diário). A consulta não
          acessa o SISREG.
        </p>
      </header>

      <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <form onSubmit={pesquisar} className="grid grid-cols-1 items-end gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Campo label="Período pela data" htmlFor="sb-eixo">
            <Select id="sb-eixo" value={eixo} onChange={(e) => setEixo(e.target.value as EixoDataConsultaSisreg)}>
              <option value="Agendamento">do agendamento</option>
              <option value="Solicitacao">da solicitação</option>
            </Select>
          </Campo>
          <Campo label="De" htmlFor="sb-inicio">
            <Input id="sb-inicio" type="date" value={inicio} onChange={(e) => setInicio(e.target.value)} />
          </Campo>
          <Campo label="Até" htmlFor="sb-fim">
            <Input id="sb-fim" type="date" value={fim} onChange={(e) => setFim(e.target.value)} />
          </Campo>
          <Campo label="Tipo" htmlFor="sb-exames">
            <div className="flex h-[38px] items-center gap-4 text-sm text-gray-700">
              <label className="inline-flex items-center gap-2">
                <input
                  id="sb-exames"
                  type="checkbox"
                  checked={incluirExames}
                  onChange={(e) => setIncluirExames(e.target.checked)}
                />
                Exames
              </label>
              <label className="inline-flex items-center gap-2">
                <input type="checkbox" checked={incluirConsultas} onChange={(e) => setIncluirConsultas(e.target.checked)} />
                Consultas
              </label>
            </div>
          </Campo>

          <Campo label="Unidades executantes" htmlFor="sb-unidades">
            <SelecaoMultipla
              id="sb-unidades"
              opcoes={(opcoes.data?.unidades ?? []).map((u) => ({
                valor: u.id,
                rotulo: u.nome,
                detalhe: numero(u.quantidade),
              }))}
              selecionados={unidadeIds}
              aoMudar={setUnidadeIds}
              placeholderBusca="Filtrar unidades…"
              comBusca
            />
          </Campo>
          <Campo label="Situação" htmlFor="sb-situacoes">
            <SelecaoMultipla
              id="sb-situacoes"
              opcoes={SITUACOES_SISREG.map((s) => ({ valor: s.valor, rotulo: s.rotulo }))}
              selecionados={situacoes}
              aoMudar={(v) => setSituacoes(v as SituacaoAgendamentoSisreg[])}
              comBusca={false}
              larguraLista="w-72"
            />
          </Campo>
          <Campo label="Procedimentos" htmlFor="sb-procedimentos" className="sm:col-span-2">
            <SelecaoMultipla
              id="sb-procedimentos"
              opcoes={(opcoes.data?.procedimentos ?? []).map((p) => ({
                valor: p.nome,
                rotulo: p.nome,
                detalhe: numero(p.quantidade),
              }))}
              selecionados={procedimentos}
              aoMudar={setProcedimentos}
              rotuloVazio="Todos"
              placeholderBusca="Filtrar procedimentos (ex.: fisioterap)…"
              comBusca
              larguraLista="w-[36rem]"
            />
          </Campo>

          <div className="flex flex-wrap items-center justify-end gap-3 sm:col-span-2 lg:col-span-4">
            {!periodoValido ? <span className="text-sm text-amber-700">A data final é anterior à inicial.</span> : null}
            {semTipo ? <span className="text-sm text-amber-700">Marque Exames, Consultas ou os dois.</span> : null}
            {opcoes.isFetching ? <Loader2 className="h-4 w-4 animate-spin text-gray-400" /> : null}
            <Button type="submit" disabled={!periodoValido || semTipo || resultado.isFetching}>
              {resultado.isFetching ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Search className="mr-2 h-4 w-4" />
              )}
              Pesquisar
            </Button>
          </div>
        </form>
      </section>

      {resultado.isError ? (
        <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          {extrairMensagemDeErro(resultado.error)}
        </div>
      ) : null}

      {aplicado && dados ? (
        <section className="space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
              <p className="text-sm text-gray-700">
                <span className="text-lg font-semibold text-gray-900">{numero(dados.totalPessoas)}</span>{' '}
                {dados.totalPessoas === 1 ? 'pessoa' : 'pessoas'} ·{' '}
                <span className="font-medium">{numero(dados.totalAtendimentos)}</span>{' '}
                {dados.totalAtendimentos === 1 ? 'atendimento' : 'atendimentos'}
              </p>
              <div className="flex flex-wrap gap-1.5">
                {dados.porSituacao.map((c) => (
                  <span
                    key={c.situacao}
                    className={`rounded px-2 py-0.5 text-xs ${COR_SITUACAO[c.situacao]}`}
                    title={SITUACOES_SISREG.find((s) => s.valor === c.situacao)?.dica}
                  >
                    {rotuloSituacao(c.situacao)}: {numero(c.quantidade)}
                  </span>
                ))}
              </div>
            </div>
            <div className="flex items-center gap-3">
              <label className="flex items-center gap-2 text-sm text-gray-600" htmlFor="sb-tamanho">
                Mostrar
                <Select
                  id="sb-tamanho"
                  value={aplicado.tamanho}
                  onChange={(e) => setAplicado({ ...aplicado, tamanho: Number(e.target.value), pagina: 1 })}
                  className="!w-auto"
                >
                  {TAMANHOS.map((t) => (
                    <option key={t} value={t}>
                      {t}
                    </option>
                  ))}
                </Select>
                por página
              </label>
              <Button
                variante="outline"
                onClick={exportarPdf}
                disabled={exportando || dados.totalAtendimentos === 0}
                title="Nome, data do agendamento, procedimento e situação — o resultado inteiro, não só esta página"
              >
                {exportando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileDown className="mr-2 h-4 w-4" />}
                Exportar PDF
              </Button>
            </div>
          </div>

          {erroPdf ? (
            <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">{erroPdf}</div>
          ) : null}

          <Tabela
            colunas={colunas}
            dados={dados.itens}
            chaveLinha={(r) => r.solicitacaoId}
            carregando={resultado.isFetching}
            aoClicarLinha={(r) => setDetalheId(r.detalheId)}
            dicaLinha="Abrir a solicitação"
            vazio="Nenhum atendimento para os filtros informados."
          />

          {dados.totalAtendimentos > dados.tamanho ? (
            <Paginacao
              pagina={aplicado.pagina}
              tamanho={aplicado.tamanho}
              total={dados.totalAtendimentos}
              tamanhos={TAMANHOS}
              aoMudarPagina={(pagina) => setAplicado({ ...aplicado, pagina })}
              aoMudarTamanho={(tamanho) => setAplicado({ ...aplicado, tamanho, pagina: 1 })}
            />
          ) : null}
        </section>
      ) : null}

      <ModalSolicitacao solicitacaoId={detalheId} aoFechar={() => setDetalheId(null)} />
    </div>
  );
}
