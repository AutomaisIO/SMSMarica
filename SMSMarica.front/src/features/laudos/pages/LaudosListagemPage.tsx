import { useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Edit2, FileText, Loader2, Search, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useExcluirLaudo, useListarLaudos } from '@/features/laudos/api/queries';
import { abrirPdfLaudo } from '@/features/laudos/lib/pdf';
import { StatusBadgeLaudo } from '@/features/laudos/components/StatusBadgeLaudo';
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
          <div className="truncate font-medium text-gray-900">
            {l.pacienteNome || 'Não vinculado'}
          </div>
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
        return new Date(dt).toLocaleString('pt-BR');
      },
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (l) => <StatusBadgeLaudo status={l.status} assinado={l.assinado} />,
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
              <button
                type="button"
                onClick={() => aoAbrirPdf(l.id)}
                title="Abrir PDF"
                className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
              >
                <FileText className="h-3.5 w-3.5" />
                PDF
              </button>
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
        className="grid grid-cols-1 gap-3 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-6"
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
