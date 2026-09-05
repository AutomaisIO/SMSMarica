import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ChevronLeft, ChevronRight, Download, Edit2, FileText, Loader2, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante } from '@/shared/lib/datas';
import { usePermissao } from '@/shared/auth/authStore';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { TextoLimitado } from '@/shared/ui/TextoLimitado';
import { CodigoCopiavel } from '@/shared/ui/CodigoCopiavel';
import { useExcluirLaudo, useListarLaudos } from '@/features/laudos/api/queries';
import { abrirPdfLaudo, baixarPdfLaudo } from '@/features/laudos/lib/pdf';
import { StatusBadgeLaudo } from '@/features/laudos/components/StatusBadgeLaudo';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { ChecksComunicacao } from '@/features/solicitacoes-exame/components/ChecksComunicacao';
import { CATEGORIAS_BIRADS, corBiRads } from '@/features/laudos/checklist/birads';
import type { FiltroLaudos, LaudoListItem, StatusLaudo } from '@/features/laudos/types';

export function LaudosListagemPage() {
  const navigate = useNavigate();
  const [filtroDigitado, setFiltroDigitado] = useState<FiltroLaudos>({ limite: 50 });
  const [pagina, setPagina] = useState(1);
  const [excluindoId, setExcluindoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  // Busca AO VIVO: o filtro digitado é aplicado sozinho 500ms após a última mudança —
  // sem botão "Buscar". Mudar data/status/limite também reaplica na hora.
  const filtro = useDebounce(filtroDigitado, 500);

  // Volta à página 1 sempre que o recorte muda (senão a pessoa fica presa numa página inexistente).
  useEffect(() => {
    setPagina(1);
  }, [filtro]);

  const lista = useListarLaudos({ ...filtro, pagina });
  const excluir = useExcluirLaudo();

  const podeEditar = usePermissao('Laudos', 'Edicao');
  const podeExcluir = usePermissao('Laudos', 'Exclusao');

  const limiteAtual = filtro.limite ?? 50;
  const total = lista.data?.total ?? 0;
  const totalPaginas = Math.max(1, Math.ceil(total / limiteAtual));

  // Enter não recarrega a página (busca já é ao vivo).
  function aoBuscar(e: FormEvent) {
    e.preventDefault();
  }

  function setCampo<K extends keyof FiltroLaudos>(k: K, v: FiltroLaudos[K]) {
    setFiltroDigitado((f) => ({ ...f, [k]: v }));
  }

  async function aoAbrirPdf(id: string) {
    setErro(null);
    try {
      await abrirPdfLaudo(id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoBaixarPdf(id: string) {
    setErro(null);
    try {
      await baixarPdfLaudo(id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  function aoExcluir(l: LaudoListItem) {
    if (!window.confirm(`Excluir o rascunho do laudo "${l.titulo}"?`)) return;
    setErro(null);
    setExcluindoId(l.id);
    excluir.mutate(l.id, {
      onError: (e) => setErro(extrairMensagemDeErro(e)),
      onSettled: () => setExcluindoId(null),
    });
  }

  const colunas: Coluna<LaudoListItem>[] = useMemo(() => [
    {
      chave: 'pedido',
      cabecalho: 'Pedido',
      className: 'w-44 whitespace-nowrap',
      // Mesma primeira coluna de Solicitações e de Exames: nº SMS copiável, SISREG embaixo.
      // O laudo se liga ao exame só pelo StudyInstanceUID; sem pedido casado (laudo órfão) não
      // há número nenhum a mostrar.
      ordenar: (l) => l.accessionNumber,
      render: (l) =>
        l.accessionNumber ? (
          <div className="min-w-0">
            <CodigoCopiavel codigo={l.accessionNumber} />
            {l.codigoSolicitacao ? (
              <div className="truncate text-xs text-gray-500">SISREG {l.codigoSolicitacao}</div>
            ) : null}
          </div>
        ) : (
          <span className="text-xs text-gray-400">Sem pedido</span>
        ),
    },
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      ordenar: (l) => l.pacienteNome ?? l.pacienteNomeDicom ?? null,
      render: (l) => (
        <div className="min-w-0">
          {l.pacienteId ? (
            // Paciente cadastrado e navegável: nome + bonequinho (resumo) + WhatsApp.
            // Quando o nome não resolveu no hub FHIR (indisponível), ainda é vínculo
            // real — rotula "Paciente vinculado", nunca "não vinculado".
            <NomePacienteComResumo
              pacienteId={l.pacienteId}
              nome={l.pacienteNome ?? 'Paciente vinculado'}
              className="min-w-0"
              classNameNome={`truncate font-medium ${l.pacienteNome ? 'text-gray-900' : 'text-gray-500'}`}
            />
          ) : l.pacienteNomeDicom ? (
            <div
              className="flex items-center gap-1.5"
              title="Nome informado no equipamento (DICOM). O exame ainda não está vinculado a um paciente cadastrado — associe-o para confirmar."
            >
              <span className="truncate font-medium italic text-gray-400">
                {l.pacienteNomeDicom}
              </span>
              <span className="shrink-0 rounded bg-gray-100 px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide text-gray-400">
                não vinculado
              </span>
            </div>
          ) : (
            <div className="truncate font-medium text-gray-400">Não vinculado</div>
          )}
          {/* Quem laudou, na linha de apoio — mesmo lugar do "Por {solicitante}" em
              Solicitações. Dispensa a coluna Médico. */}
          <div className="truncate text-xs text-gray-500">{l.medicoNome}</div>
        </div>
      ),
    },
    {
      chave: 'exame',
      cabecalho: 'Exame',
      // Mesma coluna "Exame" das outras duas telas: procedimento em cima, "MODALIDADE — unidade"
      // em cinza embaixo. Sem pedido casado sobra o título do laudo, que é o que o médico deu.
      ordenar: (l) => l.tipoExameNome ?? l.titulo ?? null,
      render: (l) => {
        const nome = l.tipoExameNome?.trim() || l.titulo?.trim();
        const apoio = [l.modalidade ?? null, l.unidadeExecutanteNome ?? null].filter(Boolean);
        return (
          <div className="min-w-0" title={nome || undefined}>
            <TextoLimitado texto={nome} max={40} className="block truncate text-gray-900" />
            {apoio.length > 0 ? (
              <div className="truncate text-xs text-gray-500">
                <span className="uppercase">{l.modalidade ?? ''}</span>
                {l.modalidade && l.unidadeExecutanteNome ? ' — ' : ''}
                {l.unidadeExecutanteNome ?? ''}
              </div>
            ) : null}
          </div>
        );
      },
    },
    {
      chave: 'data',
      cabecalho: 'Emissão',
      ordenar: (l) => l.finalizadoEm ?? l.criadoEm,
      render: (l) => {
        const dt = l.finalizadoEm ?? l.criadoEm;
        return formatarInstante(dt);
      },
    },
    {
      chave: 'birads',
      cabecalho: 'BI-RADS',
      ordenar: (l) => l.biRads,
      render: (l) =>
        l.biRads ? (
          <span
            className={`inline-block rounded border px-2 py-0.5 text-xs font-semibold ${corBiRads(l.biRads)}`}
          >
            {l.biRads}
          </span>
        ) : (
          <span className="text-xs text-gray-300">—</span>
        ),
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      ordenar: (l) => (l.assinado ? 'Assinado' : l.status),
      render: (l) => (
        <span className="inline-flex items-center gap-1.5">
          <StatusBadgeLaudo status={l.status} assinado={l.assinado} />
          {/* Checks do aviso "laudo pronto" enviado ao paciente pelo WhatsApp. */}
          <ChecksComunicacao chip={l.chipLaudoPronto} finalidade="LaudoPronto" />
        </span>
      ),
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      // Só ícones, como em Solicitações: os rótulos ("Editar", "PDF", "Baixar", "Excluir")
      // custavam metade da largura útil da tabela para dizer o que o ícone já diz. O texto
      // continua no title/aria-label, então tooltip e leitor de tela não perdem nada.
      className: 'w-28 whitespace-nowrap text-right',
      render: (l) => {
        const ehRascunho = l.status === 'Rascunho';
        return (
          <div className="flex items-center justify-end gap-2">
            {podeEditar ? (
              <button
                type="button"
                onClick={() => navigate(`/app/laudos/${l.id}`)}
                title={ehRascunho ? 'Editar rascunho' : 'Visualizar / nova versão'}
                aria-label={ehRascunho ? 'Editar rascunho' : 'Visualizar laudo'}
                className="inline-flex items-center rounded p-0.5 text-primary-700 transition-colors hover:text-primary-900"
              >
                <Edit2 className="h-4 w-4" />
              </button>
            ) : null}
            {!ehRascunho ? (
              <>
                <button
                  type="button"
                  onClick={() => aoAbrirPdf(l.id)}
                  title="Abrir PDF"
                  aria-label="Abrir PDF do laudo"
                  className="inline-flex items-center rounded p-0.5 text-gray-500 transition-colors hover:text-gray-800"
                >
                  <FileText className="h-4 w-4" />
                </button>
                {l.assinado ? (
                  <button
                    type="button"
                    onClick={() => aoBaixarPdf(l.id)}
                    title="Baixar o PDF assinado digitalmente (ICP-Brasil)"
                    aria-label="Baixar PDF assinado"
                    className="inline-flex items-center rounded p-0.5 text-gray-500 transition-colors hover:text-gray-800"
                  >
                    <Download className="h-4 w-4" />
                  </button>
                ) : null}
              </>
            ) : null}
            {podeExcluir && ehRascunho ? (
              <button
                type="button"
                onClick={() => aoExcluir(l)}
                disabled={excluindoId === l.id}
                title="Excluir rascunho"
                aria-label="Excluir rascunho"
                className="inline-flex items-center rounded p-0.5 text-red-600 transition-colors hover:text-red-800 disabled:cursor-wait disabled:opacity-60"
              >
                {excluindoId === l.id ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Trash2 className="h-4 w-4" />
                )}
              </button>
            ) : null}
          </div>
        );
      },
    },

  ], [podeEditar, podeExcluir, excluindoId, navigate]);

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <FileText className="h-6 w-6 text-primary-600" />
            Laudos
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            Liste rascunhos e laudos emitidos para os exames do PACS.
          </p>
        </div>
        <Link to="/app/pacs">
          <Button variante="outline">Abrir exames de imagem</Button>
        </Link>
      </header>

      <form
        onSubmit={aoBuscar}
        className="grid grid-cols-2 gap-3 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-4 lg:grid-cols-8"
      >
        <Campo label="Buscar" htmlFor="termo" className="sm:col-span-2">
          <div className="relative">
            <Input
              id="termo"
              value={filtroDigitado.termo ?? ''}
              onChange={(e) => setCampo('termo', e.target.value)}
              placeholder="Nome, CPF, CNS, nº SISREG ou nº do pedido"
            />
            {lista.isFetching ? (
              <Loader2 className="absolute right-2 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-gray-400" />
            ) : null}
          </div>
        </Campo>
        <Campo label="Study UID" htmlFor="study" className="sm:col-span-2">
          <Input
            id="study"
            value={filtroDigitado.studyInstanceUID ?? ''}
            onChange={(e) => setCampo('studyInstanceUID', e.target.value)}
            placeholder="1.2.840…"
          />
        </Campo>
        <Campo label="Status" htmlFor="status">
          <Select
            id="status"
            value={filtroDigitado.status ?? ''}
            onChange={(e) =>
              setCampo('status', (e.target.value || undefined) as StatusLaudo | undefined)
            }
          >
            <option value="">Todos</option>
            <option value="Rascunho">Rascunho</option>
            <option value="Finalizado">Finalizado</option>
          </Select>
        </Campo>
        <Campo label="BI-RADS" htmlFor="birads">
          <Select
            id="birads"
            value={filtroDigitado.biRads ?? ''}
            onChange={(e) => setCampo('biRads', e.target.value || undefined)}
          >
            <option value="">Todos</option>
            {CATEGORIAS_BIRADS.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </Select>
        </Campo>
        <Campo label="Vínculo" htmlFor="vinculo">
          <Select
            id="vinculo"
            value={
              filtroDigitado.vinculado === undefined ? '' : filtroDigitado.vinculado ? 'sim' : 'nao'
            }
            onChange={(e) =>
              setCampo('vinculado', e.target.value === '' ? undefined : e.target.value === 'sim')
            }
          >
            <option value="">Todos</option>
            <option value="sim">Vinculados</option>
            <option value="nao">Não vinculados</option>
          </Select>
        </Campo>
        <Campo label="Assinatura" htmlFor="assinatura">
          <Select
            id="assinatura"
            value={
              filtroDigitado.assinado === undefined ? '' : filtroDigitado.assinado ? 'sim' : 'nao'
            }
            onChange={(e) =>
              setCampo('assinado', e.target.value === '' ? undefined : e.target.value === 'sim')
            }
          >
            <option value="">Todos</option>
            <option value="sim">Assinados</option>
            <option value="nao">Não assinados</option>
          </Select>
        </Campo>
        <Campo label="Data inicial" htmlFor="di">
          <Input
            id="di"
            type="date"
            value={filtroDigitado.dataInicial ?? ''}
            onChange={(e) => setCampo('dataInicial', e.target.value || undefined)}
          />
        </Campo>
        <Campo label="Data final" htmlFor="df">
          <Input
            id="df"
            type="date"
            value={filtroDigitado.dataFinal ?? ''}
            onChange={(e) => setCampo('dataFinal', e.target.value || undefined)}
          />
        </Campo>
        <Campo label="Limite" htmlFor="limite">
          <Select
            id="limite"
            value={filtroDigitado.limite ?? 50}
            onChange={(e) => setCampo('limite', Number(e.target.value))}
          >
            {[50, 100, 200, 500].map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </Select>
        </Campo>
      </form>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      <p className="text-sm text-gray-500">
        {lista.data ? (
          <>
            {total} {total === 1 ? 'laudo' : 'laudos'}
            {totalPaginas > 1 ? (
              <span className="text-gray-400"> · página {pagina} de {totalPaginas}</span>
            ) : null}
          </>
        ) : null}
      </p>

      <Tabela
        colunas={colunas}
        dados={lista.data?.itens ?? []}
        chaveLinha={(l) => l.id}
        carregando={lista.isPending}
        vazio="Nenhum laudo encontrado para os filtros."
        redimensionavel
        idTabela="laudos"
        scrollXFlutuante
      />

      {totalPaginas > 1 ? (
        <div className="flex items-center justify-center gap-3 text-sm text-gray-600">
          <button
            type="button"
            onClick={() => setPagina((p) => Math.max(1, p - 1))}
            disabled={pagina <= 1 || lista.isFetching}
            className="inline-flex h-8 items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-50"
          >
            <ChevronLeft className="h-4 w-4" />
            Anterior
          </button>
          <span>
            Página {pagina} de {totalPaginas}
          </span>
          <button
            type="button"
            onClick={() => setPagina((p) => Math.min(totalPaginas, p + 1))}
            disabled={pagina >= totalPaginas || lista.isFetching}
            className="inline-flex h-8 items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-50"
          >
            Próxima
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      ) : null}
    </div>
  );
}
