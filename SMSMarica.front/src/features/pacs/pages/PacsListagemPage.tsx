import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ChevronLeft, ChevronRight, Edit2, FilePlus, FileText, Link2, Loader2, RotateCw, Siren, Trash2, Unlink } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { Button } from '@/shared/ui/Button';
import { notificar } from '@/shared/ui/Notificacoes';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useLaudosPorStudyUIDs } from '@/features/laudos/api/queries';
import { useRegrasIniciarLaudo } from '@/features/laudo-configuracao/queries';
import { abrirPdfLaudo } from '@/features/laudos/lib/pdf';
import { BotaoAnamnese } from '@/features/anamnese/components/BotaoAnamnese';
import type { LaudoPorStudy } from '@/features/laudos/types';
import {
  useAssociacoesPorStudyUIDs,
  useDesassociarExame,
  useExcluirEstudo,
  useOrigemPorStudyUIDs,
  usePesquisaEstudos,
  useResincronizarExames,
} from '@/features/pacs/api/queries';
import { ModalAssociarExame } from '@/features/pacs/components/ModalAssociarExame';
import { FiltroModalidades } from '@/features/pacs/components/FiltroModalidades';
import { FiltroTiposExame } from '@/features/pacs/components/FiltroTiposExame';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { formatarHoraDicom } from '@/features/pacs/lib/dicomJson';
import { abrirJanelaSolta } from '@/features/pacs/lib/janela';
import type { AssociacaoExame, Estudo, FiltroBusca, OrigemEstudo, TipoBuscaNome } from '@/features/pacs/types';
import { useModalidadesExames } from '@/features/pacs/store/modalidadesPreferencia';

/** Estudo enriquecido com laudo e associação (vindos do nosso DB). */
type ExameRow = Estudo & {
  laudo: LaudoPorStudy | null;
  associacao: AssociacaoExame | null;
  /** Só para a linha ÓRFÃ: de qual equipamento/unidade a imagem veio (AE de origem). */
  origem: OrigemEstudo | null;
};

// Persistência do filtro entre navegações e reaberturas do browser (localStorage).
const CHAVE_FILTRO_PACS = 'smsmarica.pacs.filtro';

const FILTRO_PADRAO: FiltroBusca = {
  nome: '',
  tipoBuscaNome: 'qualquer',
  // Carga inicial sem data: traz os últimos N exames independente de quando
  // foram feitos — evita o "Nenhum exame encontrado" na abertura quando não
  // houve exame hoje. (Vale só no 1º acesso; depois restauramos o último filtro.)
  dataInicial: '',
  dataFinal: '',
  limite: 10,
};

function carregarFiltroSalvo(): FiltroBusca {
  try {
    const raw = localStorage.getItem(CHAVE_FILTRO_PACS);
    if (!raw) return FILTRO_PADRAO;
    const p = JSON.parse(raw) as Partial<FiltroBusca>;
    return {
      nome: typeof p.nome === 'string' ? p.nome : FILTRO_PADRAO.nome,
      tipoBuscaNome: p.tipoBuscaNome === 'inicio' ? 'inicio' : 'qualquer',
      dataInicial: typeof p.dataInicial === 'string' ? p.dataInicial : '',
      dataFinal: typeof p.dataFinal === 'string' ? p.dataFinal : '',
      limite: typeof p.limite === 'number' && p.limite > 0 ? p.limite : FILTRO_PADRAO.limite,
    };
  } catch {
    return FILTRO_PADRAO;
  }
}

export function PacsListagemPage() {
  // Restaura o último filtro usado (data, nome, modo, limite). No 1º acesso cai
  // no FILTRO_PADRAO (sem data).
  const [filtro, setFiltro] = useState<FiltroBusca>(carregarFiltroSalvo);

  const LIMITES_DISPONIVEIS = [5, 10, 50, 100] as const;

  const navigate = useNavigate();
  const podeAbrir = usePermissao('Pacs', 'Consulta');
  const podeExcluir = usePermissao('Pacs', 'Exclusao');
  const podeCriarLaudo = usePermissao('Laudos', 'Inclusao');
  const podeEditarLaudo = usePermissao('Laudos', 'Edicao');
  const podeAssociar = usePermissao('Pacs', 'Edicao');

  // Regras de iniciar laudo (config global, leitura leve sem permissão de Configuração de
  // Laudo). Padrão seguro: exige associação e anamnese.
  const regras = useRegrasIniciarLaudo().data;
  const permitirLaudarSemAssociacao = regras?.permitirLaudarSemAssociacao ?? false;
  const permitirLaudarSemAnamnese = regras?.permitirLaudarSemAnamnese ?? false;

  // Recorte de modalidade: preferência do USUÁRIO (salva no servidor), não do navegador — a
  // médica que lauda MG e OT abre a tela já filtrada em qualquer máquina.
  const modalidades = useModalidadesExames((s) => s.modalidades);
  const definirModalidades = useModalidadesExames((s) => s.definirModalidades);
  const tipos = useModalidadesExames((s) => s.tipos);
  const definirTipos = useModalidadesExames((s) => s.definirTipos);

  const [pagina, setPagina] = useState(1);
  const exclusao = useExcluirEstudo();
  const desassociar = useDesassociarExame();
  const resync = useResincronizarExames();
  const [excluindoUid, setExcluindoUid] = useState<string | null>(null);
  const [erroExclusao, setErroExclusao] = useState<string | null>(null);
  const [erroPdf, setErroPdf] = useState<string | null>(null);
  const [erroAssoc, setErroAssoc] = useState<string | null>(null);
  const [associarEstudo, setAssociarEstudo] = useState<Estudo | null>(null);

  // Busca AO VIVO: aplica 500ms após a última mudança (nome/data/modo/limite) — sem botão
  // "Buscar". O QIDO-RS não devolve total, então a paginação é por offset com "próxima"
  // liberada quando a página vem cheia.
  const filtroDebounced = useDebounce(
    useMemo(() => ({ ...filtro, modalidades, tipoExameIds: tipos }), [filtro, modalidades, tipos]),
    500,
  );
  const limiteAtual = filtroDebounced.limite || 10;
  const busca = usePesquisaEstudos({ ...filtroDebounced, offset: (pagina - 1) * limiteAtual });

  // Volta à página 1 sempre que o recorte muda.
  useEffect(() => {
    setPagina(1);
  }, [filtroDebounced]);

  // Salva o filtro a cada mudança — sobrevive à troca de tela e ao fechar/abrir o browser.
  useEffect(() => {
    try {
      localStorage.setItem(CHAVE_FILTRO_PACS, JSON.stringify(filtro));
    } catch {
      /* localStorage indisponível — ignora */
    }
  }, [filtro]);

  function setCampo<K extends keyof FiltroBusca>(k: K, v: FiltroBusca[K]) {
    setFiltro((f) => ({ ...f, [k]: v }));
  }

  // Rede de segurança (PACS-driven): varre os exames recentes do PACS e concilia cada
  // um com a solicitação pelo accession/nº do pedido. Idempotente; nada destrutivo.
  function aoResincronizar() {
    setErroAssoc(null);
    resync.mutate(undefined, {
      onSuccess: (r) => {
        const sufixoFalha = r.falhas ? `, ${r.falhas} falha(s)` : '';
        const sufixoTeto = r.limiteAtingido
          ? ' Atenção: varredura truncada no teto — parte da janela não foi verificada.'
          : '';
        const msg =
          (r.associadas > 0
            ? `Resincronização: ${r.associadas} exame(s) conciliado(s) (${r.varridas} estudo(s) verificados${sufixoFalha}).`
            : `Resincronização: nenhum vínculo novo — ${r.semExameNoPacs} estudo(s) sem pedido correspondente${sufixoFalha}.`) +
          sufixoTeto;
        notificar(msg);
        busca.refetch(); // recarrega a lista para refletir os novos vínculos
      },
      onError: (e) => setErroAssoc(extrairMensagemDeErro(e)),
    });
  }

  // Enter não recarrega a página (busca já é ao vivo).
  function aoBuscar(e: FormEvent) {
    e.preventDefault();
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
    // Atenção: estudo.patientId vem do DICOM (00100020) e é texto livre — não
    // é um Guid do nosso DB. Não dá para mandar como pacienteId direto;
    // futuramente, resolver por CPF/nome via /pacientes e popular o vínculo.
    const params = new URLSearchParams({
      studyUID: estudo.studyInstanceUID,
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
      onSuccess: () => {
        busca.refetch();
      },
      onError: (e) => setErroExclusao(extrairMensagemDeErro(e)),
      onSettled: () => setExcluindoUid(null),
    });
  }

  const estudosDaPagina = useMemo(() => busca.data?.estudos ?? [], [busca.data]);

  const studyUids = useMemo(
    () => estudosDaPagina.map((e) => e.studyInstanceUID).filter(Boolean),
    [estudosDaPagina],
  );
  const laudosLookup = useLaudosPorStudyUIDs(studyUids);
  const mapaLaudos = useMemo(() => {
    const m = new Map<string, LaudoPorStudy>();
    for (const l of laudosLookup.data ?? []) m.set(l.studyInstanceUID, l);
    return m;
  }, [laudosLookup.data]);

  const associacoesLookup = useAssociacoesPorStudyUIDs(studyUids);
  const mapaAssociacoes = useMemo(() => {
    const m = new Map<string, AssociacaoExame>();
    for (const a of associacoesLookup.data ?? []) m.set(a.studyInstanceUID, a);
    return m;
  }, [associacoesLookup.data]);

  // Órfãos da página: sem vínculo não há pedido de onde tirar a unidade, mas o AE que enviou as
  // imagens está no próprio estudo. Só estes UIDs vão ao endpoint (uma consulta ao PACS cada).
  // Só depois que o lote de associações chega: antes disso TODA linha parece órfã e a página
  // dispararia uma consulta ao PACS por linha, à toa.
  const uidsOrfaos = useMemo(
    () => (associacoesLookup.isSuccess ? studyUids.filter((u) => !mapaAssociacoes.has(u)) : []),
    [associacoesLookup.isSuccess, studyUids, mapaAssociacoes],
  );
  const origemLookup = useOrigemPorStudyUIDs(uidsOrfaos);
  const mapaOrigem = useMemo(() => {
    const m = new Map<string, OrigemEstudo>();
    for (const o of origemLookup.data ?? []) m.set(o.studyInstanceUID, o);
    return m;
  }, [origemLookup.data]);

  const exames: ExameRow[] = estudosDaPagina
    // Descarta exames de PHANTOM: estudos de calibração/teste criados automaticamente
    // pelo equipamento de imagem (não são pacientes reais e poluem a lista).
    .filter((e) => !/phanto[nm]/i.test(e.patientName ?? ''))
    .map((e) => ({
      ...e,
      laudo: mapaLaudos.get(e.studyInstanceUID) ?? null,
      associacao: mapaAssociacoes.get(e.studyInstanceUID) ?? null,
      origem: mapaOrigem.get(e.studyInstanceUID) ?? null,
    }))
    // Exames de solicitação URGENTE sempre no topo, independente da data (sort estável).
    .map((e, i) => ({ e, i }))
    .sort((a, b) => {
      const ua = a.e.associacao?.prioridade === 'Urgente' ? 0 : 1;
      const ub = b.e.associacao?.prioridade === 'Urgente' ? 0 : 1;
      return ua - ub || a.i - b.i;
    })
    .map((x) => x.e);

  function aoDesassociar(estudo: ExameRow) {
    if (!estudo.associacao) return;
    const ok = window.confirm(
      `Desassociar o exame de "${estudo.associacao.pacienteNome ?? estudo.patientName}" ` +
        `do pedido ${estudo.associacao.accessionNumber}?`,
    );
    if (!ok) return;
    setErroAssoc(null);
    desassociar.mutate(estudo.studyInstanceUID, {
      onError: (e) => setErroAssoc(extrairMensagemDeErro(e)),
    });
  }

  const colunas: Coluna<ExameRow>[] = [
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      ordenar: (e) => e.associacao?.pacienteNome ?? e.patientName ?? null,
      render: (e) => {
        const assoc = e.associacao;
        const nome = assoc?.pacienteNome ?? e.patientName;
        const ehAuto = assoc?.explicita && assoc.origem === 'Automatica';
        return (
          <div className="min-w-0">
            <div className="flex items-center gap-1.5">
              {assoc?.prioridade === 'Urgente' ? (
                <span
                  title="Solicitação URGENTE"
                  className="inline-flex shrink-0 items-center gap-1 rounded-full bg-red-100 px-1.5 py-0.5 text-[10px] font-bold text-red-700"
                >
                  <Siren className="h-3 w-3" /> Urgente
                </span>
              ) : null}
              {assoc?.pacienteId ? (
                <NomePacienteComResumo
                  pacienteId={assoc.pacienteId}
                  nome={nome || 'Sem nome'}
                  className="min-w-0"
                  classNameNome="truncate font-medium text-gray-900"
                />
              ) : (
                <span className="truncate font-medium text-gray-900">{nome || 'Sem nome'}</span>
              )}
              {assoc ? (
                <span
                  className="shrink-0 rounded bg-emerald-100 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-emerald-700"
                  title={
                    !assoc.explicita
                      ? 'Vinculado pela worklist'
                      : ehAuto
                        ? 'Associado automaticamente (Patient ID)'
                        : 'Associado manualmente'
                  }
                >
                  {ehAuto ? 'auto' : 'assoc.'}
                </span>
              ) : null}
            </div>
            <div className="truncate text-xs text-gray-500">
              {assoc?.pacienteNome && assoc.pacienteNome !== e.patientName
                ? `DICOM: ${e.patientName || 'sem nome'}`
                : [e.patientId && `Prontuário ${e.patientId}`, e.patientAge && `${e.patientAge}a`, e.patientSex]
                    .filter(Boolean)
                    .join(' · ')}
            </div>
          </div>
        );
      },
    },
    {
      chave: 'data',
      cabecalho: 'Data / Hora',
      // studyDate/studyTime são texto DICOM (YYYYMMDD/HHMMSS): concatenar ordena cronologicamente.
      ordenar: (e) => (e.studyDate ? `${e.studyDate}${e.studyTime ?? ''}` : null),
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
      chave: 'pedido',
      cabecalho: 'Pedido',
      ordenar: (e) => e.accessionNumber?.trim() || null,
      render: (e) => {
        const acc = e.accessionNumber?.trim() ?? '';
        return acc ? (
          <span className="font-mono text-xs text-gray-600">{acc}</span>
        ) : (
          <span className="text-xs text-gray-400">—</span>
        );
      },
    },
    {
      chave: 'modalidade',
      cabecalho: 'Modalidade',
      ordenar: (e) => e.modalidade || null,
      render: (e) => (
        <span className="rounded bg-gray-100 px-2 py-0.5 text-xs font-medium uppercase text-gray-700">
          {e.modalidade || '—'}
        </span>
      ),
    },
    {
      chave: 'descricao',
      cabecalho: 'Descrição',
      // Ordena pelo que está VISÍVEL: o procedimento do pedido quando existe, senão o texto do
      // equipamento — senão a ordenação discordaria da coluna.
      ordenar: (e) => e.associacao?.tipoExameNome || e.studyDescription || null,
      render: (e) => {
        // A tag DICOM StudyDescription é escrita pelo EQUIPAMENTO e vem genérica
        // ("ULTRASSONOGRAFIA", "Mamografia"): ela não sabe qual procedimento foi pedido. Quando o
        // exame está associado, quem manda é o nome do tipo (SISREG) — o texto do aparelho vira
        // sublinha, e só quando acrescenta algo. Nada é reescrito no DICOM.
        const doPedido = e.associacao?.tipoExameNome?.trim();
        const doAparelho = e.studyDescription?.trim();
        const mostrarAparelho =
          !!doPedido && !!doAparelho && doAparelho.toLowerCase() !== doPedido.toLowerCase();
        return (
          <div className="min-w-0 leading-tight">
            <div className="truncate text-gray-800">{doPedido || doAparelho || '—'}</div>
            {mostrarAparelho ? (
              <div className="truncate text-xs text-gray-400" title={`Descrição do equipamento: ${doAparelho}`}>
                {doAparelho}
              </div>
            ) : null}
          </div>
        );
      },
    },
    {
      chave: 'unidade',
      cabecalho: 'Unidade',
      ordenar: (e) => e.associacao?.unidadeExecutanteNome ?? e.origem?.unidadeNome ?? null,
      render: (e) => {
        // Associado: a unidade vem do PEDIDO (executante, com o solicitante embaixo). Órfão: vem
        // do EQUIPAMENTO que enviou as imagens — é o que responde "de quem é este exame?" antes
        // de alguém associar.
        const assoc = e.associacao;
        if (assoc?.unidadeExecutanteNome) {
          const solicitante = assoc.unidadeSolicitanteNome;
          return (
            <div className="min-w-0 leading-tight">
              <div className="truncate text-gray-800">{assoc.unidadeExecutanteNome}</div>
              {solicitante && solicitante !== assoc.unidadeExecutanteNome ? (
                <div className="truncate text-xs text-gray-500" title="Unidade solicitante">
                  Pedido: {solicitante}
                </div>
              ) : null}
            </div>
          );
        }
        const origem = e.origem;
        if (!origem) {
          return <span className="text-xs text-gray-400">{origemLookup.isFetching ? '…' : '—'}</span>;
        }
        return (
          <div className="min-w-0 leading-tight">
            <div className="truncate text-gray-700">{origem.unidadeNome ?? 'Origem não cadastrada'}</div>
            <div className="truncate text-xs text-gray-500" title={`AE de origem: ${origem.aeTitle}`}>
              {origem.equipamentoNome ?? origem.aeTitle}
            </div>
          </div>
        );
      },
    },
    {
      chave: 'acoes',
      cabecalho: 'Ações',
      className: 'text-right',
      render: (e) => {
        const excluindoEste = excluindoUid === e.studyInstanceUID;
        const laudoFinalizado = e.laudo?.status === 'Finalizado';
        // Só o laudo ASSINADO trava a (re)associação; finalizado-sem-assinatura é livre.
        const laudoAssinado = e.laudo?.assinado === true;
        return (
          <div className="flex items-center justify-end gap-2">
            <BotaoAnamnese accessionNumber={e.accessionNumber} somenteLeitura />
            {!e.associacao && !laudoAssinado && podeAssociar ? (
              <button
                type="button"
                onClick={() => setAssociarEstudo(e)}
                title="Associar este exame a um pedido (e paciente)"
                className="inline-flex items-center gap-1 rounded-md border border-indigo-300 bg-indigo-50 px-2.5 py-1 text-xs font-medium text-indigo-800 hover:bg-indigo-100"
              >
                <Link2 className="h-3.5 w-3.5" />
                Associar
              </button>
            ) : null}
            {e.associacao?.explicita && e.associacao.origem !== 'Automatica' && !laudoAssinado && podeAssociar ? (
              <button
                type="button"
                onClick={() => aoDesassociar(e)}
                disabled={desassociar.isPending}
                title="Desassociar do pedido (enquanto o laudo não estiver assinado)"
                className="inline-flex items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-60"
              >
                <Unlink className="h-3.5 w-3.5" />
                Desassociar
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
            {!e.laudo && podeCriarLaudo && (e.associacao || permitirLaudarSemAssociacao) ? (
              (() => {
                const faltaAnamnese =
                  !!e.associacao && !e.associacao.temAnamnese && !permitirLaudarSemAnamnese;
                return (
                  <span
                    title={
                      faltaAnamnese
                        ? 'Preencha a anamnese da solicitação antes de iniciar o laudo'
                        : 'Criar laudo para este exame'
                    }
                    className="inline-flex"
                  >
                    <button
                      type="button"
                      onClick={() => criarLaudoPara(e)}
                      disabled={faltaAnamnese}
                      className="inline-flex items-center gap-1 rounded-md border border-green-300 bg-green-50 px-2.5 py-1 text-xs font-medium text-green-800 hover:bg-green-100 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      <FilePlus className="h-3.5 w-3.5" />
                      Laudar
                    </button>
                  </span>
                );
              })()
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
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Exames de imagem</h1>
          <p className="mt-1 text-sm text-gray-600">
            Filtre os exames disponíveis no PACS e abra o visualizador ou o PDF do laudo em uma janela separada.
          </p>
        </div>
        {podeAssociar ? (
          <Button
            variante="outline"
            onClick={aoResincronizar}
            disabled={resync.isPending}
            title="Varre os exames recentes do PACS (30 dias pela data do exame) e concilia cada um com o pedido pelo accession/nº da solicitação (seguro/idempotente)"
          >
            {resync.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <RotateCw className="mr-2 h-4 w-4" />
            )}
            Resincronizar
          </Button>
        ) : null}
      </header>

      <form
        onSubmit={aoBuscar}
        className="grid grid-cols-1 gap-3 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-8"
      >
        <Campo label="Nome do paciente" htmlFor="nome" className="sm:col-span-2">
          <div className="relative">
            <Input
              id="nome"
              value={filtro.nome}
              onChange={(e) => setCampo('nome', e.target.value)}
              placeholder={filtro.tipoBuscaNome === 'inicio' ? 'Início do nome…' : 'Qualquer parte do nome…'}
            />
            {busca.isFetching ? (
              <Loader2 className="absolute right-2 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-gray-400" />
            ) : null}
          </div>
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
        <Campo label="Modalidade" htmlFor="modalidade">
          <FiltroModalidades id="modalidade" selecionadas={modalidades} aoMudar={definirModalidades} />
        </Campo>
        <Campo label="Tipo de exame" htmlFor="tipo">
          <FiltroTiposExame
            id="tipo"
            modalidades={modalidades}
            selecionados={tipos}
            aoMudar={definirTipos}
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

      {busca.data?.orfaosOcultos ? (
        <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          {busca.data.orfaosOcultos} exame(s) sem associação ficaram de fora — o filtro de tipo só
          alcança o que já tem pedido casado. Limpe o filtro de tipo para vê-los.
        </div>
      ) : null}

      {busca.data?.truncado ? (
        <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          A varredura parou no teto antes de completar a página — pode haver mais exames além
          destes. Estreite o período ou o filtro para chegar ao resto.
        </div>
      ) : null}

      {erroAssoc ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erroAssoc}</div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={exames}
        chaveLinha={(e) => e.studyInstanceUID}
        carregando={busca.isPending}
        redimensionavel
        idTabela="pacs"
        aoClicarLinha={podeAbrir ? (e) => abrirViewer(e) : undefined}
        dicaLinha="Clique para visualizar"
      />

      {!busca.isPending && exames.length === 0 ? (
        <p className="text-center text-sm text-gray-500">Nenhum exame encontrado com esses filtros.</p>
      ) : null}

      {/* Paginação offset. O QIDO-RS do dcm4chee não devolve total, então não há "página X de Y":
          "Próxima" fica liberada enquanto a página vier cheia (pode haver mais). */}
      {pagina > 1 || estudosDaPagina.length >= limiteAtual ? (
        <div className="flex items-center justify-center gap-3 text-sm text-gray-600">
          <button
            type="button"
            onClick={() => setPagina((p) => Math.max(1, p - 1))}
            disabled={pagina <= 1 || busca.isFetching}
            className="inline-flex h-8 items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-50"
          >
            <ChevronLeft className="h-4 w-4" />
            Anterior
          </button>
          <span>Página {pagina}</span>
          <button
            type="button"
            onClick={() => setPagina((p) => p + 1)}
            disabled={estudosDaPagina.length < limiteAtual || busca.isFetching}
            className="inline-flex h-8 items-center gap-1 rounded-md border border-gray-300 bg-white px-2.5 font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-50"
          >
            Próxima
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      ) : null}

      <ModalAssociarExame estudo={associarEstudo} aoFechar={() => setAssociarEstudo(null)} />
    </div>
  );
}
