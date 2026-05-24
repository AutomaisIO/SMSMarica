import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Edit2, Eye, FilePlus, FileText, Loader2, Search, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useLaudosPorStudyUIDs } from '@/features/laudos/api/queries';
import { abrirPdfLaudo } from '@/features/laudos/lib/pdf';
import type { LaudoPorStudy } from '@/features/laudos/types';
import { useBuscarEstudos, useExcluirEstudo } from '@/features/pacs/api/queries';
import { formatarHoraDicom } from '@/features/pacs/lib/dicomJson';
import { abrirJanelaSolta } from '@/features/pacs/lib/janela';
import type { Estudo, FiltroBusca, TipoBuscaNome } from '@/features/pacs/types';

/** Estudo enriquecido com a informação do laudo (vindo do nosso DB). */
type ExameRow = Estudo & { laudo: LaudoPorStudy | null };

export function PacsListagemPage() {
  // Carga inicial sem data: traz os últimos N exames independente de quando
  // foram feitos — evita o "Nenhum exame encontrado" na abertura quando não
  // houve exame hoje (que é o caso comum). Quem quiser filtrar pela data
  // preenche o campo e clica Buscar.
  const [filtro, setFiltro] = useState<FiltroBusca>({
    nome: '',
    tipoBuscaNome: 'qualquer',
    dataInicial: '',
    dataFinal: '',
    limite: 10,
  });

  const LIMITES_DISPONIVEIS = [5, 10, 50, 100] as const;

  const navigate = useNavigate();
  const podeAbrir = usePermissao('Pacs', 'Consulta');
  const podeExcluir = usePermissao('Pacs', 'Exclusao');
  const podeCriarLaudo = usePermissao('Laudos', 'Inclusao');
  const podeEditarLaudo = usePermissao('Laudos', 'Edicao');

  const busca = useBuscarEstudos();
  const exclusao = useExcluirEstudo();
  const [excluindoUid, setExcluindoUid] = useState<string | null>(null);
  const [erroExclusao, setErroExclusao] = useState<string | null>(null);
  const [erroPdf, setErroPdf] = useState<string | null>(null);

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

  async function abrirLaudoPdf(laudoId: string) {
    setErroPdf(null);
    try {
      await abrirPdfLaudo(laudoId);
    } catch (e) {
      setErroPdf(extrairMensagemDeErro(e));
    }
  }

  function criarLaudoPara(estudo: Estudo) {
    const params = new URLSearchParams({
      studyUID: estudo.studyInstanceUID,
      ...(estudo.patientId ? { patientId: estudo.patientId } : {}),
      ...(estudo.modalidade ? { modalidade: estudo.modalidade } : {}),
    });
    navigate(`/app/laudos/novo?${params.toString()}`);
  }

  function excluirExame(estudo: Estudo) {
    const nome = estudo.patientName || 'sem nome';
    const dataHora = [
      estudo.studyDateFormatado || estudo.studyDate,
      formatarHoraDicom(estudo.studyTime),
    ]
      .filter(Boolean)
      .join(' ');
    const confirmou = window.confirm(
      `Excluir definitivamente o exame de "${nome}"${dataHora ? ` (${dataHora})` : ''}?\n` +
        'Esta ação não pode ser desfeita.',
    );
    if (!confirmou) return;
    setErroExclusao(null);
    setExcluindoUid(estudo.studyInstanceUID);
    exclusao.mutate(estudo.studyInstanceUID, {
      onSuccess: () => busca.mutate(filtro),
      onError: (e) => setErroExclusao(extrairMensagemDeErro(e)),
      onSettled: () => setExcluindoUid(null),
    });
  }

  const studyUids = useMemo(
    () => (busca.data ?? []).map((e) => e.studyInstanceUID).filter(Boolean),
    [busca.data],
  );
  const laudosLookup = useLaudosPorStudyUIDs(studyUids);
  const mapaLaudos = useMemo(() => {
    const m = new Map<string, LaudoPorStudy>();
    for (const l of laudosLookup.data ?? []) m.set(l.studyInstanceUID, l);
    return m;
  }, [laudosLookup.data]);

  const exames: ExameRow[] = (busca.data ?? []).map((e) => ({
    ...e,
    laudo: mapaLaudos.get(e.studyInstanceUID) ?? null,
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
      cabecalho: 'Data / Hora',
      render: (e) => {
        const hora = formatarHoraDicom(e.studyTime);
        return (
          <div className="leading-tight">
            <div className="font-medium text-gray-900">{e.studyDateFormatado || '—'}</div>
            <div className="text-xs text-gray-500">{hora || '—'}</div>
          </div>
        );
      },
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
      render: (e) => {
        const excluindoEste = excluindoUid === e.studyInstanceUID;
        const laudoFinalizado = e.laudo?.status === 'Finalizado';
        return (
          <div className="flex items-center justify-end gap-2">
            {podeAbrir ? (
              <button
                type="button"
                onClick={() => abrirViewer(e)}
                title="Abrir visualizador em janela separada"
                className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
              >
                <Eye className="h-3.5 w-3.5" />
                Visualizar
              </button>
            ) : null}
            {e.laudo && podeEditarLaudo ? (
              <button
                type="button"
                onClick={() => navigate(`/app/laudos/${e.laudo!.laudoId}`)}
                title={laudoFinalizado ? 'Abrir laudo finalizado' : 'Editar rascunho do laudo'}
                className="inline-flex items-center gap-1 rounded-md border border-amber-300 bg-amber-50 px-2.5 py-1 text-xs font-medium text-amber-800 hover:bg-amber-100"
              >
                <Edit2 className="h-3.5 w-3.5" />
                {laudoFinalizado ? 'Laudo' : 'Rascunho'}
              </button>
            ) : null}
            {!e.laudo && podeCriarLaudo ? (
              <button
                type="button"
                onClick={() => criarLaudoPara(e)}
                title="Criar laudo para este exame"
                className="inline-flex items-center gap-1 rounded-md border border-green-300 bg-green-50 px-2.5 py-1 text-xs font-medium text-green-800 hover:bg-green-100"
              >
                <FilePlus className="h-3.5 w-3.5" />
                Laudar
              </button>
            ) : null}
            {laudoFinalizado ? (
              <button
                type="button"
                onClick={() => abrirLaudoPdf(e.laudo!.laudoId)}
                title="Abrir PDF do laudo em janela separada"
                className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
              >
                <FileText className="h-3.5 w-3.5" />
                PDF
              </button>
            ) : null}
            {podeExcluir ? (
              <button
                type="button"
                onClick={() => excluirExame(e)}
                disabled={excluindoEste}
                title="Excluir exame do PACS"
                className="inline-flex items-center gap-1 rounded-md border border-red-300 bg-white px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-50 disabled:cursor-wait disabled:opacity-60"
              >
                {excluindoEste ? (
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

      {erroExclusao ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          Falha ao excluir o exame: {erroExclusao}
        </div>
      ) : null}

      {erroPdf ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          Falha ao abrir o PDF: {erroPdf}
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
