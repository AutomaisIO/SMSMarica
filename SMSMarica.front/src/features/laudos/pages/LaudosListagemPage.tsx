import { useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Download, Edit2, FileText, Loader2, Search, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante } from '@/shared/lib/datas';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useExcluirLaudo, useListarLaudos } from '@/features/laudos/api/queries';
import { abrirPdfLaudo, baixarPdfLaudo } from '@/features/laudos/lib/pdf';
import { StatusBadgeLaudo } from '@/features/laudos/components/StatusBadgeLaudo';
import { ChecksComunicacao } from '@/features/solicitacoes-exame/components/ChecksComunicacao';
import { CATEGORIAS_BIRADS, corBiRads } from '@/features/laudos/checklist/birads';
import type { FiltroLaudos, LaudoListItem, StatusLaudo } from '@/features/laudos/types';

export function LaudosListagemPage() {
  const navigate = useNavigate();
  const [filtro, setFiltro] = useState<FiltroLaudos>({ limite: 50 });
  const [filtroDigitado, setFiltroDigitado] = useState<FiltroLaudos>({ limite: 50 });
  const [excluindoId, setExcluindoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  const lista = useListarLaudos(filtro);
  const excluir = useExcluirLaudo();

  const podeEditar = usePermissao('Laudos', 'Edicao');
  const podeExcluir = usePermissao('Laudos', 'Exclusao');

  function aoBuscar(e: FormEvent) {
    e.preventDefault();
    setFiltro(filtroDigitado);
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
      chave: 'paciente',
      cabecalho: 'Paciente',
      render: (l) => (
        <div className="min-w-0">
          {l.pacienteNome ? (
            <div className="truncate font-medium text-gray-900">{l.pacienteNome}</div>
          ) : l.pacienteId ? (
            // Vinculado, mas o nome não resolveu no hub FHIR (indisponível). É vínculo
            // real — nunca rotular como "não vinculado".
            <div className="truncate font-medium text-gray-500">Paciente vinculado</div>
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
          <div className="truncate text-xs text-gray-500">{l.titulo}</div>
        </div>
      ),
    },
    {
      chave: 'medico',
      cabecalho: 'Médico',
      render: (l) => <span className="text-gray-700">{l.medicoNome}</span>,
    },
    {
      chave: 'data',
      cabecalho: 'Emissão',
      render: (l) => {
        const dt = l.finalizadoEm ?? l.criadoEm;
        return formatarInstante(dt);
      },
    },
    {
      chave: 'birads',
      cabecalho: 'BI-RADS',
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
      className: 'text-right',
      render: (l) => {
        const ehRascunho = l.status === 'Rascunho';
        return (
          <div className="flex items-center justify-end gap-2">
            {podeEditar ? (
              <button
                type="button"
                onClick={() => navigate(`/app/laudos/${l.id}`)}
                title={ehRascunho ? 'Editar rascunho' : 'Visualizar / nova versão'}
                className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
              >
                <Edit2 className="h-3.5 w-3.5" />
                {ehRascunho ? 'Editar' : 'Abrir'}
              </button>
            ) : null}
            {!ehRascunho ? (
              <>
                <button
                  type="button"
                  onClick={() => aoAbrirPdf(l.id)}
                  title="Abrir PDF"
                  className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
                >
                  <FileText className="h-3.5 w-3.5" />
                  PDF
                </button>
                {l.assinado ? (
                  <button
                    type="button"
                    onClick={() => aoBaixarPdf(l.id)}
                    title="Baixar o PDF assinado digitalmente (ICP-Brasil)"
                    className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
                  >
                    <Download className="h-3.5 w-3.5" />
                    Baixar
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
                className="inline-flex items-center gap-1 rounded-md border border-red-300 bg-white px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-50 disabled:cursor-wait disabled:opacity-60"
              >
                {excluindoId === l.id ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                ) : (
                  <Trash2 className="h-3.5 w-3.5" />
                )}
                Excluir
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
        <div className="flex items-end">
          <Button type="submit" disabled={lista.isPending} className="w-full">
            <Search className="mr-2 h-4 w-4" />
            Buscar
          </Button>
        </div>
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

      <Tabela
        colunas={colunas}
        dados={lista.data ?? []}
        chaveLinha={(l) => l.id}
        carregando={lista.isPending}
        vazio="Nenhum laudo encontrado para os filtros."
      />
    </div>
  );
}
