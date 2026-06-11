import { useEffect, useState } from 'react';
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
import { ClipboardCheck } from 'lucide-react';
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
import { abrirPdfLaudo } from '@/features/laudos/lib/pdf';
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
  const [erro, setErro] = useState<string | null>(null);
  const [aguardandoAgente, setAguardandoAgente] = useState(false);
  const [agenteNaoEncontrado, setAgenteNaoEncontrado] = useState(false);

  const studyParam = params.get('studyUID') ?? '';
  const modalidadeParam = params.get('modalidade') ?? undefined;
  const templateIdParam = params.get('templateId');

  // Hidrata o form com o detalhe carregado.
  useEffect(() => {
    if (detalhe.data) {
      setTitulo(detalhe.data.titulo);
      setHtml(detalhe.data.conteudoHtml);
      setJson(detalhe.data.conteudoJson);
    }
  }, [detalhe.data]);

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

  async function aoSalvarRascunho() {
    setErro(null);
    try {
      if (ehNovo) {
        const novoId = await cadastrar.mutateAsync({
          studyInstanceUID,
          pacienteId: null,
          laudoTemplateId: templateIdParam || null,
          titulo,
          conteudoJson: json,
          conteudoHtml: html,
        });
        navigate(`/app/laudos/${novoId}`, { replace: true });
      } else if (id) {
        await atualizar.mutateAsync({
          id,
          payload: {
            pacienteId: detalhe.data?.pacienteId ?? null,
            titulo,
            conteudoJson: json,
            conteudoHtml: html,
          },
        });
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
          laudoTemplateId: templateIdParam || null,
          titulo,
          conteudoJson: json,
          conteudoHtml: html,
        });
      }
      if (!alvoId) return;
      await finalizar.mutateAsync({
        id: alvoId,
        payload: { titulo, conteudoJson: json, conteudoHtml: html },
      });
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

  const salvando = cadastrar.isPending || atualizar.isPending;
  const finalizando = finalizar.isPending;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <button
            type="button"
            onClick={() => navigate(-1)}
            className="inline-flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
          >
            <ArrowLeft className="h-4 w-4" />
            Voltar
          </button>
          <h1 className="mt-1 flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <FileText className="h-6 w-6 text-primary-600" />
            {ehNovo ? 'Novo laudo' : `Laudo (v${detalhe.data?.versao ?? '?'})`}
            {!ehNovo && detalhe.data ? <StatusBadgeLaudo status={detalhe.data.status} /> : null}
          </h1>
        </div>
        <div className="flex items-center gap-2">
          {studyInstanceUID ? (
            <Button variante="outline" onClick={abrirVisualizadorPacs}>
              <ScanLine className="mr-2 h-4 w-4" />
              Abrir visualizador
            </Button>
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
              ) : ehMedico && podeFinalizar ? (
                <Button onClick={aoAssinar} disabled={iniciarAssinatura.isPending}>
                  <ShieldCheck className="mr-2 h-4 w-4" />
                  {assinaturaFalhou ? 'Tentar assinar de novo' : 'Assinar'}
                </Button>
              ) : null}
              <Button variante="outline" onClick={() => abrirPdfLaudo(id!)}>
                <FileText className="mr-2 h-4 w-4" />
                PDF
              </Button>
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
                  Solicitado por {solicitacao.data.solicitanteNome} (CRM{' '}
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
                  setHtml(t.conteudoHtml);
                  setJson(t.conteudoJson);
                  if (!titulo || titulo === 'Laudo') setTitulo(t.nome);
                }}
              />
            ) : null}
          </div>

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
