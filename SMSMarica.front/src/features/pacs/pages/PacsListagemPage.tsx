import { useEffect, useState, type FormEvent } from 'react';
import { Eye, FileText, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useBuscarEstudos } from '@/features/pacs/api/queries';
import { abrirJanelaSolta } from '@/features/pacs/lib/janela';
import type { Estudo, FiltroBusca, TipoBuscaNome } from '@/features/pacs/types';

function hojeIso(): string {
  const agora = new Date();
  const ano = agora.getFullYear();
  const mes = String(agora.getMonth() + 1).padStart(2, '0');
  const dia = String(agora.getDate()).padStart(2, '0');
  return `${ano}-${mes}-${dia}`;
}

/**
 * Estudo enriquecido com a informação de laudo (vinda do nosso DB no futuro).
 * Por ora `laudoId` é sempre `null` — o botão de PDF fica oculto até o módulo
 * de laudo existir; quando vier, basta popular este campo.
 */
type ExameRow = Estudo & { laudoId: string | null };

export function PacsListagemPage() {
  const [filtro, setFiltro] = useState<FiltroBusca>({
    nome: '',
    tipoBuscaNome: 'qualquer',
    dataInicial: hojeIso(),
    dataFinal: hojeIso(),
    limite: 10,
  });

  const LIMITES_DISPONIVEIS = [5, 10, 50, 100] as const;

  const busca = useBuscarEstudos();

  // Carga inicial: exames de hoje.
  useEffect(() => {
    busca.mutate(filtro);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function setCampo<K extends keyof FiltroBusca>(k: K, v: FiltroBusca[K]) {
    setFiltro((f) => ({ ...f, [k]: v }));
  }

  function aoBuscar(e: FormEvent) {
    e.preventDefault();
    busca.mutate(filtro);
  }

  function abrirViewer(estudo: Estudo) {
    const hash = encodeURIComponent(JSON.stringify(estudo));
    const ok = abrirJanelaSolta(`/pacs/janela#${hash}`, `pacs-viewer-${estudo.studyInstanceUID}`);
    if (!ok) {
      alert('A janela do visualizador foi bloqueada pelo navegador. Libere os popups para este site.');
    }
  }

  function abrirLaudoPdf(laudoId: string) {
    // O módulo de laudo entrega esta rota; por enquanto só estamos preparados.
    const ok = abrirJanelaSolta(`/laudos/${laudoId}/pdf`, `laudo-${laudoId}`, 1200, 900);
    if (!ok) {
      alert('A janela do PDF foi bloqueada pelo navegador. Libere os popups para este site.');
    }
  }

  const exames: ExameRow[] = (busca.data ?? []).map((e) => ({
    ...e,
    // TODO: popular com o laudoId quando o módulo Laudo estiver pronto
    // (provavelmente vindo de um endpoint paralelo /laudos?studyUIDs=...).
    laudoId: null,
  }));

  const colunas: Coluna<ExameRow>[] = [
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      render: (e) => (
        <div className="min-w-0">
          <div className="truncate font-medium text-gray-900">{e.patientName || 'Sem nome'}</div>
          <div className="truncate text-xs text-gray-500">
            {[e.patientId && `Prontuário ${e.patientId}`, e.patientAge && `${e.patientAge}a`, e.patientSex]
              .filter(Boolean)
              .join(' · ')}
          </div>
        </div>
      ),
    },
    {
      chave: 'data',
      cabecalho: 'Data',
      render: (e) => <span className="text-gray-700">{e.studyDateFormatado || '—'}</span>,
    },
    {
      chave: 'modalidade',
      cabecalho: 'Modalidade',
      render: (e) => (
        <span className="rounded bg-gray-100 px-2 py-0.5 text-xs font-medium uppercase text-gray-700">
          {e.modalidade || '—'}
        </span>
      ),
    },
    {
      chave: 'descricao',
      cabecalho: 'Descrição',
      render: (e) => <span className="text-gray-700">{e.studyDescription || '—'}</span>,
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (e) => (
        <div className="flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={() => abrirViewer(e)}
            title="Abrir visualizador em janela separada"
            className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
          >
            <Eye className="h-3.5 w-3.5" />
            Visualizar
          </button>
          {e.laudoId ? (
            <button
              type="button"
              onClick={() => abrirLaudoPdf(e.laudoId!)}
              title="Abrir laudo em PDF em janela separada"
              className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
            >
              <FileText className="h-3.5 w-3.5" />
              PDF
            </button>
          ) : null}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-5">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Exames de imagem</h1>
        <p className="mt-1 text-sm text-gray-600">
          Filtre os exames disponíveis no PACS e abra o visualizador ou o PDF do laudo em uma janela separada.
        </p>
      </header>

      <form
        onSubmit={aoBuscar}
        className="grid grid-cols-1 gap-3 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-7"
      >
        <Campo label="Nome do paciente" htmlFor="nome" className="sm:col-span-2">
          <Input
            id="nome"
            value={filtro.nome}
            onChange={(e) => setCampo('nome', e.target.value)}
            placeholder={filtro.tipoBuscaNome === 'inicio' ? 'Início do nome…' : 'Qualquer parte do nome…'}
          />
        </Campo>
        <Campo label="Modo" htmlFor="modo">
          <Select
            id="modo"
            value={filtro.tipoBuscaNome}
            onChange={(e) => setCampo('tipoBuscaNome', e.target.value as TipoBuscaNome)}
          >
            <option value="qualquer">Qualquer ocorrência</option>
            <option value="inicio">Somente início</option>
          </Select>
        </Campo>
        <Campo label="Data inicial" htmlFor="di">
          <Input
            id="di"
            type="date"
            value={filtro.dataInicial}
            onChange={(e) => setCampo('dataInicial', e.target.value)}
          />
        </Campo>
        <Campo label="Data final" htmlFor="df">
          <Input
            id="df"
            type="date"
            value={filtro.dataFinal}
            onChange={(e) => setCampo('dataFinal', e.target.value)}
          />
        </Campo>
        <Campo label="Limite" htmlFor="limite">
          <Select
            id="limite"
            value={filtro.limite}
            onChange={(e) => setCampo('limite', Number(e.target.value))}
          >
            {LIMITES_DISPONIVEIS.map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </Select>
        </Campo>
        <div className="flex items-end">
          <Button type="submit" disabled={busca.isPending} className="w-full">
            <Search className="mr-2 h-4 w-4" />
            {busca.isPending ? 'Buscando…' : 'Buscar'}
          </Button>
        </div>
      </form>

      {busca.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(busca.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={exames}
        chaveLinha={(e) => e.studyInstanceUID}
        carregando={busca.isPending}
      />

      {!busca.isPending && exames.length === 0 ? (
        <p className="text-center text-sm text-gray-500">Nenhum exame encontrado com esses filtros.</p>
      ) : null}
    </div>
  );
}
