import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import {
  ArrowLeft,
  CheckCircle2,
  Download,
  FileText,
  Loader2,
  RefreshCw,
  Save,
  ScanLine,
  ShieldAlert,
  ShieldCheck,
} from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useEhMedico, usePermissao } from '@/shared/auth/authStore';
import { abrirJanelaSolta } from '@/shared/lib/janela';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { EditorRichText } from '@/shared/ui/EditorRichText';
import { CabecalhoLaudo } from '@/features/laudos/components/CabecalhoLaudo';
import { SeletorTemplate } from '@/features/laudos/components/SeletorTemplate';
import { StatusBadgeLaudo } from '@/features/laudos/components/StatusBadgeLaudo';
import { useSolicitacaoPorStudy } from '@/features/solicitacoes-exame/api/queries';
import { useAnexosExamePaciente } from '@/features/pacientes/api/queries';
import { useContextoAnamnese } from '@/features/anamnese/api/queries';
import { ClipboardCheck, ClipboardList, History } from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  useAtualizarLaudo,
  useCadastrarLaudo,
  useCriarNovaVersao,
  useFinalizarLaudo,
  laudosKeys,
  useIniciarAssinatura,
  useLaudoPorId,
  useStatusAssinatura,
} from '@/features/laudos/api/queries';
import { abrirPdfLaudo, baixarPdfLaudo } from '@/features/laudos/lib/pdf';
import { useAvisoSaidaNaoSalva } from '@/shared/hooks/useAvisoSaidaNaoSalva';
import { PainelChecklist } from '@/features/laudos/checklist/PainelChecklist';
import { useListarTemplates } from '@/features/laudo-templates/api/queries';
import { obterTemplate } from '@/features/laudo-templates/api/laudoTemplatesApi';
import { coletarContribuicoes, gerarHtmlLaudo } from '@/features/laudos/checklist/gerarTexto';
import type { EstruturaChecklist, RespostasChecklist } from '@/features/laudos/checklist/types';
import type { ChecklistLaudoInput } from '@/features/laudos/types';
import {
  TIMEOUT_AGENTE_SEGUNDOS,
  lancarAgenteAssinatura,
  urlDownloadAssinador,
} from '@/features/laudos/lib/assinatura';

export function LaudoEditorPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { id } = useParams<{ id: string }>();
  const [params] = useSearchParams();
  const ehNovo = !id || id === 'novo';

  const detalhe = useLaudoPorId(ehNovo ? null : id ?? null);
  const cadastrar = useCadastrarLaudo();
  const atualizar = useAtualizarLaudo();
  const finalizar = useFinalizarLaudo();
  const novaVersao = useCriarNovaVersao();

  const podeFinalizar = usePermissao('Laudos', 'Edicao');
  const ehMedico = useEhMedico();
  const iniciarAssinatura = useIniciarAssinatura();

  const [titulo, setTitulo] = useState('Laudo');
  const [html, setHtml] = useState('');
  const [json, setJson] = useState('{}');
  const [respostas, setRespostas] = useState<RespostasChecklist | null>(null);
  const [templateEscolhidoId, setTemplateEscolhidoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [aguardandoAgente, setAguardandoAgente] = useState(false);
  const [agenteNaoEncontrado, setAgenteNaoEncontrado] = useState(false);
  // Baseline (título/html/json/checklist) para detectar alterações não salvas.
  const [baseline, setBaseline] = useState(() =>
    JSON.stringify({ t: 'Laudo', h: '', j: '{}', r: null }),
  );
  const snap = (t: string, h: string, j: string, r: RespostasChecklist | null) =>
    JSON.stringify({ t, h, j, r });

  const studyParam = params.get('studyUID') ?? '';
  const modalidadeParam = params.get('modalidade') ?? undefined;
  const templateIdParam = params.get('templateId');

  // Hidrata o form com o detalhe carregado.
  useEffect(() => {
    if (detalhe.data) {
      let respostasCarregadas: RespostasChecklist | null = null;
      if (detalhe.data.respostasChecklist) {
        try {
          respostasCarregadas = JSON.parse(detalhe.data.respostasChecklist) as RespostasChecklist;
        } catch {
          respostasCarregadas = null;
        }
      }
      setTitulo(detalhe.data.titulo);
      setHtml(detalhe.data.conteudoHtml);
      setJson(detalhe.data.conteudoJson);
      setRespostas(respostasCarregadas);
      setBaseline(
        JSON.stringify({
          t: detalhe.data.titulo,
          h: detalhe.data.conteudoHtml,
          j: detalhe.data.conteudoJson,
          r: respostasCarregadas,
        }),
      );
    }
  }, [detalhe.data]);

  // Laudo "checklist-only" (por ora só mamografia): ao criar um laudo novo,
  // carrega automaticamente o template de checklist disponível, em vez de abrir
  // no texto livre. Gating por modalidade evita carregar mamografia em estudo de
  // outra natureza; quando houver mais templates, casar por modalidade/categoria.
  const autoCarregado = useRef(false);
  const ehMamografia = !modalidadeParam || modalidadeParam === 'MG';
  const templatesDisponiveis = useListarTemplates(undefined, false);
  useEffect(() => {
    if (!ehNovo || autoCarregado.current || respostas || !ehMamografia) return;
    const lista = templatesDisponiveis.data;
    if (!lista) return;
    const tpl = lista.find((t) => t.temChecklist);
    autoCarregado.current = true;
    if (!tpl) return;
    void obterTemplate(tpl.id)
      .then((t) => {
        if (!t.estruturaJson) return;
        const estrutura = JSON.parse(t.estruturaJson) as EstruturaChecklist;
        const r: RespostasChecklist = { estrutura, marcados: {}, biRadsFinal: null };
        const novoHtml = gerarHtmlLaudo(r);
        const novoTitulo = !titulo || titulo === 'Laudo' ? t.nome : titulo;
        setTemplateEscolhidoId(t.id);
        setRespostas(r);
        setHtml(novoHtml);
        setJson('{}');
        setTitulo(novoTitulo);
        // Template auto-carregado não é "alteração do usuário": atualiza o baseline
        // para o modal de "não salvo" só acusar edições reais em cima do template.
        setBaseline(snap(novoTitulo, novoHtml, '{}', r));
      })
      .catch(() => {
        /* sem template/checklist → segue no texto livre */
      });
  }, [ehNovo, respostas, ehMamografia, templatesDisponiveis.data]);

  // Marcar/desmarcar no checklist regenera o texto do laudo (TipTap fica para ajuste fino).
  function aoMudarRespostas(r: RespostasChecklist) {
    setRespostas(r);
    setHtml(gerarHtmlLaudo(r));
    setJson('{}');
  }

  function montarChecklist(): ChecklistLaudoInput | null {
    if (!respostas) return null;
    return {
      respostasJson: JSON.stringify(respostas),
      contribuicoes: coletarContribuicoes(respostas.estrutura, respostas.marcados),
      biRadsFinal: respostas.biRadsFinal,
    };
  }

  const studyInstanceUID = ehNovo ? studyParam : (detalhe.data?.studyInstanceUID ?? studyParam);
  const finalizado = !ehNovo && detalhe.data?.status === 'Finalizado';

  // Assinatura digital: faz polling enquanto o agente do médico assina.
  const statusAssinatura = useStatusAssinatura(ehNovo ? null : (id ?? null), finalizado);
  const assinado = (detalhe.data?.assinado ?? false) || statusAssinatura.data?.status === 'Concluida';
  const assinando =
    iniciarAssinatura.isPending ||
    statusAssinatura.data?.status === 'Iniciada' ||
    statusAssinatura.data?.status === 'AguardandoAssinatura';
  const assinaturaFalhou = statusAssinatura.data?.status === 'Falhou';

  // Watchdog: depois de lançar o protocolo, se em N segundos o job não saiu de
  // "Iniciada" (o agente não reivindicou), concluímos que o Assinador não está instalado.
  // Assim que o job avança (o agente reivindicou), o agente existe — então cancela o
  // watchdog E fecha o modal "não encontrado" caso ele tenha aparecido por atraso.
  const statusAtual = statusAssinatura.data?.status;
  const agenteReivindicou =
    statusAtual === 'AguardandoAssinatura' ||
    statusAtual === 'Concluida' ||
    statusAtual === 'Falhou';
  useEffect(() => {
    if (agenteReivindicou) {
      setAguardandoAgente(false);
      setAgenteNaoEncontrado(false);
      return;
    }
    if (!aguardandoAgente) return;
    const t = setTimeout(() => {
      setAguardandoAgente(false);
      setAgenteNaoEncontrado(true);
    }, TIMEOUT_AGENTE_SEGUNDOS * 1000);
    return () => clearTimeout(t);
  }, [aguardandoAgente, agenteReivindicou]);

  // Quando a assinatura conclui, atualiza listagem/detalhe para o cadeado refletir.
  const concluiu = statusAtual === 'Concluida';
  useEffect(() => {
    if (concluiu && id) {
      queryClient.invalidateQueries({ queryKey: laudosKeys.porId(id) });
      queryClient.invalidateQueries({ queryKey: laudosKeys.raiz });
    }
  }, [concluiu, id, queryClient]);

  // Puxa o pedido (Solicitação de Exame) associado ao Study para mostrar contexto clínico.
  const solicitacao = useSolicitacaoPorStudy(studyInstanceUID || null);

  // Guarda de alterações não salvas (o painel usa BrowserRouter, sem useBlocker).
  // Laudo finalizado é somente-leitura → nunca fica "sujo".
  const sujo = !finalizado && snap(titulo, html, json, respostas) !== baseline;
  const { elemento: modalSaida, permitir, protegerAcao } = useAvisoSaidaNaoSalva({
    sujo,
    aoSalvar: persistirLaudo,
    mensagem:
      'O laudo tem alterações que ainda não foram salvas. Deseja salvar o rascunho antes de sair?',
  });

  // Persiste o rascunho SEM navegar (usado pelo "Salvar e sair" do guard); lança em erro
  // para o guard não prosseguir com dados perdidos. Nunca toca laudo finalizado/assinado.
  async function persistirLaudo() {
    if (ehNovo) {
      await cadastrar.mutateAsync({
        studyInstanceUID,
        pacienteId: null,
        laudoTemplateId: templateIdParam || templateEscolhidoId || null,
        titulo,
        conteudoJson: json,
        conteudoHtml: html,
        checklist: montarChecklist(),
      });
    } else if (id) {
      await atualizar.mutateAsync({
        id,
        payload: {
          pacienteId: detalhe.data?.pacienteId ?? null,
          titulo,
          conteudoJson: json,
          conteudoHtml: html,
          checklist: montarChecklist(),
        },
      });
    }
    setBaseline(snap(titulo, html, json, respostas));
  }

  async function aoSalvarRascunho() {
    setErro(null);
    try {
      if (ehNovo) {
        const novoId = await cadastrar.mutateAsync({
          studyInstanceUID,
          pacienteId: null,
          laudoTemplateId: templateIdParam || templateEscolhidoId || null,
          titulo,
          conteudoJson: json,
          conteudoHtml: html,
          checklist: montarChecklist(),
        });
        setBaseline(snap(titulo, html, json, respostas));
        permitir();
        navigate(`/app/laudos/${novoId}`, { replace: true });
      } else if (id) {
        await atualizar.mutateAsync({
          id,
          payload: {
            pacienteId: detalhe.data?.pacienteId ?? null,
            titulo,
            conteudoJson: json,
            conteudoHtml: html,
            checklist: montarChecklist(),
          },
        });
        setBaseline(snap(titulo, html, json, respostas));
      }
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoFinalizar() {
    setErro(null);
    if (!html.trim()) {
      setErro('Conteúdo do laudo não pode estar vazio.');
      return;
    }
    if (!window.confirm('Finalizar o laudo? Após finalizado ele não pode mais ser editado.')) return;
    try {
      let alvoId = id;
      if (ehNovo) {
        alvoId = await cadastrar.mutateAsync({
          studyInstanceUID,
          pacienteId: null,
          laudoTemplateId: templateIdParam || templateEscolhidoId || null,
          titulo,
          conteudoJson: json,
          conteudoHtml: html,
          checklist: montarChecklist(),
        });
      }
      if (!alvoId) return;
      await finalizar.mutateAsync({
        id: alvoId,
        payload: { titulo, conteudoJson: json, conteudoHtml: html, checklist: montarChecklist() },
      });
      permitir();
      navigate(`/app/laudos/${alvoId}`, { replace: true });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoCriarNovaVersao() {
    if (!id) return;
    setErro(null);
    try {
      const novoId = await novaVersao.mutateAsync(id);
      permitir();
      navigate(`/app/laudos/${novoId}`, { replace: true });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoAssinar() {
    if (!id) return;
    setErro(null);
    setAgenteNaoEncontrado(false);
    try {
      const { chave } = await iniciarAssinatura.mutateAsync(id);
      // Lança o agente via protocolo e arma o watchdog que detecta se ele não está instalado.
      lancarAgenteAssinatura(chave);
      setAguardandoAgente(true);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  function abrirVisualizadorPacs() {
    if (!studyInstanceUID) return;
    const ok = abrirJanelaSolta(
      `/pacs/janela#${encodeURIComponent(JSON.stringify({ studyInstanceUID }))}`,
      `pacs-viewer-${studyInstanceUID}`,
    );
    if (!ok) {
      alert('O visualizador foi bloqueado pelo navegador. Libere os popups para este site.');
    }
  }

  // Paciente do pedido associado — alimenta a anamnese e os exames anteriores.
  const pacienteIdPedido = solicitacao.data?.pacienteId ?? null;
  const anexosPaciente = useAnexosExamePaciente(pacienteIdPedido);
  const qtdExamesAnteriores = anexosPaciente.data?.length ?? 0;

  // Anamnese: só habilita o botão quando já existe uma salva (abre só leitura).
  const anamneseCtx = useContextoAnamnese({ solicitacaoExameId: solicitacao.data?.id });
  const semAnamnese = anamneseCtx.isSuccess && !anamneseCtx.data?.anamnese;

  function abrirAnamneseJanela() {
    const solId = solicitacao.data?.id;
    if (!solId || semAnamnese) return;
    const ok = abrirJanelaSolta(`/anamnese/janela?solicitacaoId=${solId}`, `anamnese-${solId}`, 1100, 900);
    if (!ok) alert('A janela foi bloqueada pelo navegador. Libere os popups para este site.');
  }

  function abrirExamesAnterioresJanela() {
    const pid = solicitacao.data?.pacienteId;
    if (!pid) return;
    const payload = encodeURIComponent(
      JSON.stringify({ pacienteId: pid, nome: solicitacao.data?.pacienteNome }),
    );
    const ok = abrirJanelaSolta(
      `/exames-anteriores/janela#${payload}`,
      `exames-anteriores-${pid}`,
      1200,
      900,
    );
    if (!ok) alert('A janela foi bloqueada pelo navegador. Libere os popups para este site.');
  }

  const salvando = cadastrar.isPending || atualizar.isPending;
  const finalizando = finalizar.isPending;

  return (
    <div className="space-y-5">
      {modalSaida}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <button
            type="button"
            onClick={protegerAcao(() => navigate(-1))}
            className="inline-flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
          >
            <ArrowLeft className="h-4 w-4" />
            Voltar
          </button>
          <h1 className="mt-1 flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <FileText className="h-6 w-6 text-primary-600" />
            {ehNovo ? 'Novo laudo' : `Laudo (v${detalhe.data?.versao ?? '?'})`}
            {!ehNovo && detalhe.data ? (
              <StatusBadgeLaudo status={detalhe.data.status} assinado={assinado} />
            ) : null}
          </h1>
        </div>
        <div className="flex items-center gap-2">
          {studyInstanceUID ? (
            <Button variante="outline" onClick={abrirVisualizadorPacs}>
              <ScanLine className="mr-2 h-4 w-4" />
              Abrir visualizador
            </Button>
          ) : null}
          {solicitacao.data ? (
            <>
              <span title={semAnamnese ? 'Sem anamnese' : undefined} className="inline-flex">
                <Button variante="outline" onClick={abrirAnamneseJanela} disabled={semAnamnese}>
                  <ClipboardList className="mr-2 h-4 w-4" />
                  Anamnese
                </Button>
              </span>
              <Button variante="outline" onClick={abrirExamesAnterioresJanela}>
                <History className="mr-2 h-4 w-4" />
                Exames anteriores
                {qtdExamesAnteriores > 0 ? (
                  <span className="ml-2 inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-primary-600 px-1.5 text-xs font-semibold text-white">
                    {qtdExamesAnteriores}
                  </span>
                ) : null}
              </Button>
            </>
          ) : null}
          {!ehNovo && finalizado ? (
            <>
              {assinado ? (
                <span className="inline-flex items-center gap-1.5 rounded-md border border-emerald-200 bg-emerald-50 px-2.5 py-1.5 text-sm font-medium text-emerald-800">
                  <ShieldCheck className="h-4 w-4" />
                  Assinado digitalmente
                </span>
              ) : ehMedico && podeFinalizar && assinando && !agenteNaoEncontrado ? (
                <span className="inline-flex items-center gap-1.5 rounded-md border border-amber-200 bg-amber-50 px-2.5 py-1.5 text-sm text-amber-800">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Aguardando autorização no seu agente (VIDaaS Connect)…
                </span>
              ) : ehMedico && podeFinalizar && detalhe.data?.podeAssinar ? (
                <Button onClick={aoAssinar} disabled={iniciarAssinatura.isPending}>
                  <ShieldCheck className="mr-2 h-4 w-4" />
                  {assinaturaFalhou ? 'Tentar assinar de novo' : 'Assinar'}
                </Button>
              ) : ehMedico && detalhe.data?.motivoBloqueioAssinatura ? (
                <span
                  className="inline-flex items-center gap-1.5 rounded-md border border-amber-200 bg-amber-50 px-2.5 py-1.5 text-sm text-amber-800"
                  title={detalhe.data.motivoBloqueioAssinatura}
                >
                  <ShieldAlert className="h-4 w-4" />
                  Rubrica não cadastrada
                </span>
              ) : null}
              {ehMedico ? (
                <a
                  href={urlDownloadAssinador}
                  className="inline-flex items-center gap-1.5 rounded-md border border-gray-300 bg-white px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
                  title="Baixar o instalador do Automais Assinador (necessário só uma vez por máquina)"
                >
                  <Download className="h-4 w-4" />
                  Baixar Assinador
                </a>
              ) : null}
              <Button variante="outline" onClick={() => abrirPdfLaudo(id!)}>
                <FileText className="mr-2 h-4 w-4" />
                PDF
              </Button>
              {assinado ? (
                <Button
                  variante="outline"
                  onClick={() => baixarPdfLaudo(id!)}
                  title="Baixar o PDF assinado digitalmente (ICP-Brasil)"
                >
                  <Download className="mr-2 h-4 w-4" />
                  Baixar
                </Button>
              ) : null}
              {podeFinalizar ? (
                <Button onClick={aoCriarNovaVersao} disabled={novaVersao.isPending}>
                  <RefreshCw className="mr-2 h-4 w-4" />
                  Nova versão
                </Button>
              ) : null}
            </>
          ) : (
            <>
              <Button variante="outline" onClick={aoSalvarRascunho} disabled={salvando || finalizando}>
                {salvando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                Salvar rascunho
              </Button>
              {podeFinalizar ? (
                <Button onClick={aoFinalizar} disabled={salvando || finalizando}>
                  {finalizando ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                  )}
                  Finalizar
                </Button>
              ) : null}
            </>
          )}
        </div>
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      {detalhe.isPending && !ehNovo ? (
        <div className="flex items-center justify-center rounded-lg border border-gray-200 bg-white py-10 text-gray-500">
          <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Carregando laudo…
        </div>
      ) : (
        <>
          {solicitacao.data ? (
            <div className="flex flex-wrap items-center gap-3 rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-900">
              <ClipboardCheck className="h-4 w-4 flex-shrink-0" />
              <div className="flex-1 min-w-0">
                <span className="font-medium">Pedido:</span>{' '}
                <Link
                  to={`/app/solicitacoes-exame/${solicitacao.data.id}`}
                  className="font-mono hover:underline"
                >
                  {solicitacao.data.accessionNumber}
                </Link>
                {' · '}
                <span>{solicitacao.data.tipoExameNome}</span>
                {' · '}
                <span>
                  Solicitado por {solicitacao.data.solicitanteNome} (
                  {(solicitacao.data.solicitanteConselho || 'CRM').toUpperCase()}{' '}
                  {solicitacao.data.solicitanteUfCrm}/{solicitacao.data.solicitanteCrm})
                </span>
                {solicitacao.data.justificativa ? (
                  <div className="mt-1 text-xs text-emerald-800">
                    Justificativa: {solicitacao.data.justificativa}
                  </div>
                ) : null}
              </div>
            </div>
          ) : null}

          <CabecalhoLaudo
            pacienteNome={detalhe.data?.pacienteNome ?? solicitacao.data?.pacienteNome}
            pacienteCpf={detalhe.data?.pacienteCpf ?? solicitacao.data?.pacienteCpf}
            studyInstanceUID={studyInstanceUID}
            medicoNome={detalhe.data?.medicoNome}
            medicoCrm={detalhe.data?.medicoCrm}
            medicoUfCrm={detalhe.data?.medicoUfCrm}
            modalidade={modalidadeParam}
          />

          <div className="flex flex-wrap items-end justify-between gap-3 rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
            <Campo label="Título do laudo" htmlFor="titulo" className="flex-1 min-w-[250px]">
              <Input
                id="titulo"
                value={titulo}
                onChange={(e) => setTitulo(e.target.value)}
                placeholder="Ex.: Mamografia bilateral — BI-RADS"
                disabled={finalizado}
              />
            </Campo>
            {!finalizado ? (
              <SeletorTemplate
                aoEscolher={(t) => {
                  setTemplateEscolhidoId(t.id);
                  let estrutura: EstruturaChecklist | null = null;
                  if (t.estruturaJson) {
                    try {
                      estrutura = JSON.parse(t.estruturaJson) as EstruturaChecklist;
                    } catch {
                      estrutura = null;
                    }
                  }
                  if (estrutura) {
                    const r: RespostasChecklist = { estrutura, marcados: {}, biRadsFinal: null };
                    setRespostas(r);
                    setHtml(gerarHtmlLaudo(r));
                    setJson('{}');
                  } else {
                    setRespostas(null);
                    setHtml(t.conteudoHtml);
                    setJson(t.conteudoJson);
                  }
                  if (!titulo || titulo === 'Laudo') setTitulo(t.nome);
                }}
              />
            ) : null}
          </div>

          {respostas ? (
            <>
              <PainelChecklist
                respostas={respostas}
                somenteLeitura={finalizado}
                aoMudar={aoMudarRespostas}
              />
              <p className="text-xs text-gray-500">
                O texto abaixo é gerado pelo checklist — ajustes manuais são sobrescritos ao
                remarcar uma opção.
              </p>
            </>
          ) : null}

          <EditorRichText
            valorHtml={html}
            aoMudar={(v) => {
              setHtml(v.html);
              setJson(v.json);
            }}
            placeholder="Escreva o laudo aqui ou carregue um template…"
            somenteLeitura={finalizado}
            alturaMinima="500px"
          />

          {finalizado ? (
            <p className="text-xs text-gray-500">
              Laudo finalizado — para correções, use <strong>Nova versão</strong>.
            </p>
          ) : null}
        </>
      )}

      {agenteNaoEncontrado ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h3 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
              <ShieldCheck className="h-5 w-5 text-red-600" />
              Assinador não encontrado
            </h3>
            <p className="mt-2 text-sm text-gray-600">
              Não detectamos o <strong>Automais Assinador</strong> instalado nesta máquina.
              Instale o aplicativo e tente assinar novamente. Se o problema persistir,
              entre em contato com o suporte.
            </p>
            <div className="mt-5 flex items-center justify-between gap-3">
              <a
                href={urlDownloadAssinador}
                className="inline-flex items-center gap-2 rounded-md bg-primary-600 px-3 py-2 text-sm font-medium text-white hover:bg-primary-700"
              >
                <Download className="h-4 w-4" />
                Baixar o Assinador
              </a>
              <button
                type="button"
                onClick={() => setAgenteNaoEncontrado(false)}
                className="text-sm text-gray-600 hover:text-gray-900"
              >
                Fechar
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
