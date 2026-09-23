import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  ArrowLeft,
  ClipboardList,
  FileCheck2,
  Loader2,
  Save,
  ShieldAlert,
  User,
} from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { notificar } from '@/shared/ui/Notificacoes';
import { useAvisoSaidaNaoSalva } from '@/shared/hooks/useAvisoSaidaNaoSalva';
import { useContextoAnamnese, useSalvarAnamnese } from '@/features/anamnese/api/queries';
import { AnamneseLeitura } from '@/features/anamnese/components/AnamneseLeitura';
import { AnexosExameSecao } from '@/features/anamnese/components/AnexosExameSecao';
import { DiagramaMamas } from '@/features/anamnese/components/DiagramaMamas';
import { ModalGerarRequisicaoSiscan } from '@/features/anamnese/components/ModalGerarRequisicaoSiscan';
import { ModalLoginSiscan } from '@/features/anamnese/components/ModalLoginSiscan';
import { SecaoSiscan } from '@/features/anamnese/components/SecaoSiscan';
import { useSessaoSiscan } from '@/features/anamnese/api/siscanApi';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Modal } from '@/shared/ui/Modal';
import {
  CRITERIOS_RISCO,
  PERGUNTAS_HISTORICO,
  ROTULOS_SINTOMAS,
  SINTOMAS_QUEIXA,
  conteudoVazio,
  type AnamneseMamografiaConteudo,
  type ClassificacaoRisco,
} from '@/features/anamnese/types';

function calcularIdade(nascimento: string | null): number | null {
  if (!nascimento) return null;
  const nasc = new Date(nascimento);
  if (Number.isNaN(nasc.getTime())) return null;
  const hoje = new Date();
  let idade = hoje.getFullYear() - nasc.getFullYear();
  const aniversarioPassou =
    hoje.getMonth() > nasc.getMonth() ||
    (hoje.getMonth() === nasc.getMonth() && hoje.getDate() >= nasc.getDate());
  if (!aniversarioPassou) idade -= 1;
  return idade;
}

/** Cabeçalho de seção no estilo do formulário em papel (pílula colorida). */
function TituloSecao({ numero, titulo, cor }: { numero: number; titulo: string; cor: string }) {
  return (
    <div className={`inline-flex items-center gap-2 rounded-full px-4 py-1.5 text-sm font-bold text-white ${cor}`}>
      <span className="flex h-5 w-5 items-center justify-center rounded-full bg-white/25 text-xs">
        {numero}
      </span>
      {titulo}
    </div>
  );
}

export function AnamnesePage({ janela = false }: { janela?: boolean } = {}) {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const solicitacaoId = params.get('solicitacaoId') ?? undefined;
  const accession = params.get('accession') ?? undefined;
  // Fora da tela de Solicitações (janela do Laudar, ou `?leitura=1` vindo do PACS)
  // a anamnese abre só para leitura — nunca em edição.
  const leitura = params.get('leitura') === '1';

  const contexto = useContextoAnamnese({
    solicitacaoExameId: solicitacaoId,
    accessionNumber: accession,
  });
  const salvar = useSalvarAnamnese();
  // Anexar documento NÃO é edição do questionário: o médico precisa disso
  // justamente enquanto lauda (janela solta) ou olhando o exame pelo PACS
  // (`?leitura=1`). O gate é só a permissão — que o backend também exige no
  // endpoint. Já o questionário continua somente-leitura fora de Solicitações.
  const podeAnexar = usePermissao('SolicitacoesExame', 'Edicao');
  const podeEditar = podeAnexar && !janela && !leitura;

  const [conteudo, setConteudo] = useState<AnamneseMamografiaConteudo>(conteudoVazio);
  const [erro, setErro] = useState<string | null>(null);

  // ---- Requisição no SISCAN ----
  // A sessão do SISCAN é do próprio operador e não é guardada: se não houver, o botão pede a
  // senha antes de qualquer coisa. `pendente` é o que retomar depois do login.
  const sessaoSiscan = useSessaoSiscan(podeEditar);
  const [loginSiscan, setLoginSiscan] = useState(false);
  const [gerarSiscan, setGerarSiscan] = useState(false);
  const [perguntarSiscan, setPerguntarSiscan] = useState<'salvou' | 'saindo' | null>(null);
  const [saindoApos, setSaindoApos] = useState(false);
  // Baseline para detectar alterações não salvas (comparação com o estado atual).
  const [baseline, setBaseline] = useState(() => JSON.stringify(conteudoVazio()));

  // Em janela solta, o nome do paciente vira o título da janela do SO.
  const pacienteNome = contexto.data?.pacienteNome;
  useEffect(() => {
    if (janela && pacienteNome) document.title = `Anamnese — ${pacienteNome}`;
  }, [janela, pacienteNome]);

  // Hidrata o form com a anamnese existente (merge defensivo sobre o shape vazio).
  useEffect(() => {
    const json = contexto.data?.anamnese?.conteudoJson;
    if (!json) return;
    try {
      const carregado = JSON.parse(json) as Partial<AnamneseMamografiaConteudo>;
      const base = conteudoVazio();
      const merged: AnamneseMamografiaConteudo = {
        avaliacaoClinica: { ...base.avaliacaoClinica, ...carregado.avaliacaoClinica },
        historicoClinico: { ...base.historicoClinico, ...carregado.historicoClinico },
        queixas: {
          ...base.queixas,
          ...carregado.queixas,
          sintomas: { ...base.queixas.sintomas, ...carregado.queixas?.sintomas },
        },
        avaliacaoRisco: { ...base.avaliacaoRisco, ...carregado.avaliacaoRisco },
        saudeReprodutiva: {
          ...base.saudeReprodutiva,
          ...carregado.saudeReprodutiva,
          usoAnticoncepcional: {
            ...base.saudeReprodutiva.usoAnticoncepcional,
            ...carregado.saudeReprodutiva?.usoAnticoncepcional,
          },
          aindaMenstrua: {
            ...base.saudeReprodutiva.aindaMenstrua,
            ...carregado.saudeReprodutiva?.aindaMenstrua,
          },
        },
        // Anamnese v1 não tem o bloco `siscan`: o merge sobre o shape vazio abre
        // as perguntas novas em branco, e o registro antigo continua abrindo.
        siscan: {
          ...base.siscan,
          ...carregado.siscan,
          radioterapia: { ...base.siscan.radioterapia, ...carregado.siscan?.radioterapia },
          cirurgias: carregado.siscan?.cirurgias ?? base.siscan.cirurgias,
        },
      };
      setConteudo(merged);
      setBaseline(JSON.stringify(merged));
    } catch {
      setErro('Não foi possível ler a anamnese gravada; o formulário foi aberto em branco.');
    }
  }, [contexto.data?.anamnese?.conteudoJson]);

  const idade = useMemo(
    () => calcularIdade(contexto.data?.pacienteNascimento ?? null),
    [contexto.data?.pacienteNascimento],
  );

  // Persiste sem navegar (usado pelo botão Salvar e pelo "Salvar e sair" do guard).
  async function persistir() {
    if (!contexto.data) return;
    setErro(null);
    await salvar.mutateAsync({
      solicitacaoExameId: contexto.data.solicitacaoExameId,
      payload: {
        tipo: 'mamografia',
        // v2 (22/09/2026): ganhou o bloco `siscan` — as perguntas que a
        // requisição do SISCAN exige e o formulário de papel não tinha.
        versao: 2,
        conteudoJson: JSON.stringify(conteudo),
        classificacaoRisco: conteudo.avaliacaoRisco.classificacao,
      },
    });
    setBaseline(JSON.stringify(conteudo));
  }

  async function aoSalvar() {
    try {
      await persistir();

      // Salvou e ainda não há requisição no SISCAN: avisa ANTES de sair da tela. Quem preencheu
      // está aqui agora, com a paciente na cabeça — depois vira uma pendência que ninguém vê.
      if (podeEditar && !contexto.data?.siscanProtocolo) {
        setPerguntarSiscan('salvou');
        return;
      }

      notificar('Anamnese salva com sucesso.');
      navigate(-1);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  /** Sair da anamnese: se não gerou a requisição, pergunta antes de deixar ir. */
  function sair() {
    if (podeEditar && !contexto.data?.siscanProtocolo) {
      setPerguntarSiscan('saindo');
      return;
    }

    navigate(-1);
  }

  /** Abre a geração — pedindo a senha do SISCAN antes, se a sessão não estiver de pé. */
  function abrirGeracao() {
    setPerguntarSiscan(null);
    if (sessaoSiscan.data?.autenticado) setGerarSiscan(true);
    else setLoginSiscan(true);
  }

  // Guarda de alterações não salvas (o painel usa BrowserRouter, sem useBlocker).
  const sujo = podeEditar && JSON.stringify(conteudo) !== baseline;
  const { elemento: modalSaida, protegerAcao } = useAvisoSaidaNaoSalva({
    sujo,
    aoSalvar: persistir,
    mensagem:
      'A anamnese tem alterações que ainda não foram salvas. Deseja salvar antes de sair?',
  });

  function mudarHistorico(
    pergunta: (typeof PERGUNTAS_HISTORICO)[number][0],
    mudanca: Partial<{ resposta: boolean | null; observacao: string }>,
  ) {
    setConteudo((c) => ({
      ...c,
      historicoClinico: {
        ...c.historicoClinico,
        [pergunta]: { ...c.historicoClinico[pergunta], ...mudanca },
      },
    }));
  }

  if (contexto.isPending) {
    return (
      <div className="flex items-center justify-center rounded-lg border border-gray-200 bg-white py-12 text-gray-500">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Carregando anamnese…
      </div>
    );
  }

  if (contexto.isError || !contexto.data) {
    return (
      <div className={`space-y-3 ${janela ? 'min-h-screen bg-gray-50 p-6' : ''}`}>
        <button
          type="button"
          onClick={() => (janela ? window.close() : navigate(-1))}
          className="inline-flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
        >
          <ArrowLeft className="h-4 w-4" /> {janela ? 'Fechar' : 'Voltar'}
        </button>
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {contexto.error ? extrairMensagemDeErro(contexto.error) : 'Solicitação não encontrada.'}
        </div>
      </div>
    );
  }

  const ctx = contexto.data;
  const somenteLeitura = !podeEditar;

  return (
    <div className={`mx-auto max-w-5xl space-y-6 pb-10 ${janela ? 'min-h-screen bg-gray-50 p-6' : ''}`}>
      {modalSaida}
      {/* Cabeçalho */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          {janela ? null : (
            <button
              type="button"
              onClick={protegerAcao(sair)}
              className="inline-flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
            >
              <ArrowLeft className="h-4 w-4" /> Voltar
            </button>
          )}
          <h1 className="mt-1 flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <ClipboardList className="h-6 w-6 text-primary-600" />
            Questionário para Exame de Mamografia
            <AjudaManual artigo="anamnese" />
          </h1>
          <p className="text-sm text-gray-500">
            Pedido <span className="font-mono">{ctx.accessionNumber}</span> · {ctx.tipoExameNome}
            {ctx.anamnese?.preenchidoPorNome ? (
              <> · preenchida por {ctx.anamnese.preenchidoPorNome}</>
            ) : null}
          </p>
          {/* O carimbo do SISCAN: é o que a médica leva para laudar. Fica no cabeçalho porque é
              informação de identidade do pedido, não uma resposta do questionário. */}
          {ctx.siscanProtocolo ? (
            <p className="mt-1 inline-flex flex-wrap items-center gap-x-2 rounded-md bg-teal-50 px-2 py-1 text-xs text-teal-900">
              <FileCheck2 className="h-3.5 w-3.5" />
              SISCAN · protocolo <span className="font-mono font-semibold">{ctx.siscanProtocolo}</span>
              {ctx.siscanNumeroExame ? (
                <>
                  · exame <span className="font-mono font-semibold">{ctx.siscanNumeroExame}</span>
                </>
              ) : null}
            </p>
          ) : null}
        </div>
        {podeEditar ? (
          <div className="flex flex-wrap gap-2">
            {ctx.siscanProtocolo ? null : (
              <Button variante="secundaria" onClick={abrirGeracao}>
                <FileCheck2 className="mr-2 h-4 w-4" />
                Gerar Requisição SISCAN
              </Button>
            )}
            <Button onClick={aoSalvar} disabled={salvar.isPending}>
              {salvar.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-2 h-4 w-4" />
              )}
              Salvar anamnese
            </Button>
          </div>
        ) : null}
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      {/* 1. Identificação do paciente (somente leitura — vem do cadastro) */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <TituloSecao numero={1} titulo="IDENTIFICAÇÃO DO PACIENTE" cor="bg-sky-600" />
        <div className="mt-3 grid grid-cols-1 gap-x-6 gap-y-2 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <div className="lg:col-span-2">
            <span className="text-gray-500">Nome completo</span>
            <p className="flex items-center gap-1.5 font-medium text-gray-900">
              <User className="h-4 w-4 text-gray-400" />
              {ctx.pacienteNome || '—'}
            </p>
          </div>
          <div>
            <span className="text-gray-500">Data de nascimento</span>
            <p className="font-medium text-gray-900">
              {ctx.pacienteNascimento
                ? new Date(ctx.pacienteNascimento).toLocaleDateString('pt-BR', { timeZone: 'UTC' })
                : '—'}
              {idade !== null ? <span className="text-gray-500"> · {idade} anos</span> : null}
            </p>
          </div>
          <div>
            <span className="text-gray-500">CNS / CPF</span>
            <p className="font-mono font-medium text-gray-900">
              {ctx.pacienteCns || ctx.pacienteCpf || '—'}
            </p>
          </div>
        </div>
      </section>

      {somenteLeitura ? (
        <AnamneseLeitura conteudo={conteudo} />
      ) : (
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* 2. Avaliação clínica pelo profissional */}
        <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <TituloSecao numero={2} titulo="AVALIAÇÃO CLÍNICA PELO PROFISSIONAL" cor="bg-cyan-600" />
          <div className="mt-4">
            <DiagramaMamas
              marcacoes={conteudo.avaliacaoClinica.marcacoes}
              tracos={conteudo.avaliacaoClinica.tracos ?? []}
              aoMudar={(marcacoes) =>
                setConteudo((c) => ({
                  ...c,
                  avaliacaoClinica: { ...c.avaliacaoClinica, marcacoes },
                }))
              }
              aoMudarTracos={(tracos) =>
                setConteudo((c) => ({
                  ...c,
                  avaliacaoClinica: { ...c.avaliacaoClinica, tracos },
                }))
              }
              somenteLeitura={somenteLeitura}
            />
          </div>
          <fieldset className="mt-4 rounded-md border border-gray-200 p-3" disabled={somenteLeitura}>
            <legend className="px-1 text-sm font-semibold text-cyan-700">Achados clínicos</legend>
            <label className="flex items-center gap-2 text-sm text-gray-800">
              <input
                type="checkbox"
                checked={conteudo.avaliacaoClinica.semAlteracoes}
                onChange={(e) =>
                  setConteudo((c) => ({
                    ...c,
                    avaliacaoClinica: { ...c.avaliacaoClinica, semAlteracoes: e.target.checked },
                  }))
                }
                className="h-4 w-4 rounded border-gray-300"
              />
              Sem alterações
            </label>
            <label className="mt-1.5 flex items-center gap-2 text-sm text-gray-800">
              <input
                type="checkbox"
                checked={conteudo.avaliacaoClinica.alteracoesPalpaveis}
                onChange={(e) =>
                  setConteudo((c) => ({
                    ...c,
                    avaliacaoClinica: { ...c.avaliacaoClinica, alteracoesPalpaveis: e.target.checked },
                  }))
                }
                className="h-4 w-4 rounded border-gray-300"
              />
              Alterações palpáveis
            </label>
            <label className="mt-2 block text-sm text-gray-600">
              Especificar:
              <input
                type="text"
                value={conteudo.avaliacaoClinica.especificar}
                onChange={(e) =>
                  setConteudo((c) => ({
                    ...c,
                    avaliacaoClinica: { ...c.avaliacaoClinica, especificar: e.target.value },
                  }))
                }
                className="mt-0.5 w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm"
              />
            </label>
            <label className="mt-2 block text-sm text-gray-600">
              Outras observações:
              <textarea
                value={conteudo.avaliacaoClinica.outrasObservacoes}
                onChange={(e) =>
                  setConteudo((c) => ({
                    ...c,
                    avaliacaoClinica: { ...c.avaliacaoClinica, outrasObservacoes: e.target.value },
                  }))
                }
                rows={2}
                className="mt-0.5 w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm"
              />
            </label>
          </fieldset>
        </section>

        {/* 3. Histórico clínico */}
        <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <TituloSecao numero={3} titulo="HISTÓRICO CLÍNICO" cor="bg-rose-500" />
          <fieldset disabled={somenteLeitura}>
            <div className="mt-3 space-y-2">
              <div className="grid grid-cols-[1fr_auto_auto] items-center gap-x-3 text-xs font-semibold text-rose-600">
                <span>Pergunta</span>
                <span className="w-8 text-center">Sim</span>
                <span className="w-8 text-center">Não</span>
              </div>
              {PERGUNTAS_HISTORICO.map(([chave, rotulo]) => {
                const r = conteudo.historicoClinico[chave];
                return (
                  <div key={chave} className="border-b border-gray-100 pb-2">
                    <div className="grid grid-cols-[1fr_auto_auto] items-center gap-x-3">
                      <span className="text-sm text-gray-800">{rotulo}</span>
                      <span className="flex w-8 justify-center">
                        <input
                          type="checkbox"
                          checked={r.resposta === true}
                          onChange={() =>
                            mudarHistorico(chave, { resposta: r.resposta === true ? null : true })
                          }
                          className="h-4 w-4 rounded border-gray-300"
                        />
                      </span>
                      <span className="flex w-8 justify-center">
                        <input
                          type="checkbox"
                          checked={r.resposta === false}
                          onChange={() =>
                            mudarHistorico(chave, { resposta: r.resposta === false ? null : false })
                          }
                          className="h-4 w-4 rounded border-gray-300"
                        />
                      </span>
                    </div>
                    {r.resposta === true ? (
                      <input
                        type="text"
                        placeholder="Observação…"
                        value={r.observacao}
                        onChange={(e) => mudarHistorico(chave, { observacao: e.target.value })}
                        className="mt-1 w-full rounded-md border border-gray-200 px-2 py-1 text-xs"
                      />
                    ) : null}
                  </div>
                );
              })}
            </div>
            <label className="mt-3 block text-sm text-rose-600">
              Outras informações relevantes:
              <textarea
                value={conteudo.historicoClinico.outrasInformacoes}
                onChange={(e) =>
                  setConteudo((c) => ({
                    ...c,
                    historicoClinico: { ...c.historicoClinico, outrasInformacoes: e.target.value },
                  }))
                }
                rows={2}
                className="mt-0.5 w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm text-gray-800"
              />
            </label>
          </fieldset>
        </section>

        {/* 4. Queixas referidas */}
        <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <TituloSecao numero={4} titulo="QUEIXAS REFERIDAS" cor="bg-sky-600" />
          <fieldset disabled={somenteLeitura}>
            <div className="mt-3 grid grid-cols-[1fr_auto_auto] items-center gap-x-3 text-xs font-semibold text-gray-600">
              <span>Sinais e Sintomas</span>
              <span className="w-20 text-center">Mama Direita</span>
              <span className="w-20 text-center">Mama Esquerda</span>
            </div>
            <div className="mt-1 space-y-1.5">
              {SINTOMAS_QUEIXA.map((s) => (
                <div key={s} className="grid grid-cols-[1fr_auto_auto] items-center gap-x-3">
                  <span className="text-sm text-gray-800">
                    {ROTULOS_SINTOMAS[s]}
                    {s === 'outro' ? (
                      <input
                        type="text"
                        placeholder="qual?"
                        value={conteudo.queixas.outroTexto}
                        onChange={(e) =>
                          setConteudo((c) => ({
                            ...c,
                            queixas: { ...c.queixas, outroTexto: e.target.value },
                          }))
                        }
                        className="ml-2 w-32 rounded-md border border-gray-200 px-2 py-0.5 text-xs"
                      />
                    ) : null}
                  </span>
                  {(['direita', 'esquerda'] as const).map((lado) => (
                    <span key={lado} className="flex w-20 justify-center">
                      <input
                        type="checkbox"
                        checked={conteudo.queixas.sintomas[s][lado]}
                        onChange={(e) =>
                          setConteudo((c) => ({
                            ...c,
                            queixas: {
                              ...c.queixas,
                              sintomas: {
                                ...c.queixas.sintomas,
                                [s]: { ...c.queixas.sintomas[s], [lado]: e.target.checked },
                              },
                            },
                          }))
                        }
                        className="h-4 w-4 rounded border-gray-300"
                      />
                    </span>
                  ))}
                </div>
              ))}
            </div>
            <label className="mt-3 block text-sm text-sky-700">
              Descrever:
              <textarea
                value={conteudo.queixas.descrever}
                onChange={(e) =>
                  setConteudo((c) => ({
                    ...c,
                    queixas: { ...c.queixas, descrever: e.target.value },
                  }))
                }
                rows={2}
                className="mt-0.5 w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm text-gray-800"
              />
            </label>
          </fieldset>
        </section>

        {/* 5. Avaliação de risco */}
        <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <TituloSecao numero={5} titulo="AVALIAÇÃO DE RISCO" cor="bg-orange-500" />
          <fieldset disabled={somenteLeitura}>
            <div className="mt-3 grid grid-cols-[1fr_auto_auto] items-center gap-x-3 text-xs font-semibold text-orange-600">
              <span>Critério</span>
              <span className="w-8 text-center">Sim</span>
              <span className="w-8 text-center">Não</span>
            </div>
            <div className="mt-1 space-y-2">
              {CRITERIOS_RISCO.map(([chave, rotulo]) => {
                const valor = conteudo.avaliacaoRisco[chave];
                const mudar = (novo: boolean | null) =>
                  setConteudo((c) => ({
                    ...c,
                    avaliacaoRisco: { ...c.avaliacaoRisco, [chave]: novo },
                  }));
                return (
                  <div key={chave} className="grid grid-cols-[1fr_auto_auto] items-center gap-x-3 border-b border-gray-100 pb-2">
                    <span className="text-sm text-gray-800">{rotulo}</span>
                    <span className="flex w-8 justify-center">
                      <input
                        type="checkbox"
                        checked={valor === true}
                        onChange={() => mudar(valor === true ? null : true)}
                        className="h-4 w-4 rounded border-gray-300"
                      />
                    </span>
                    <span className="flex w-8 justify-center">
                      <input
                        type="checkbox"
                        checked={valor === false}
                        onChange={() => mudar(valor === false ? null : false)}
                        className="h-4 w-4 rounded border-gray-300"
                      />
                    </span>
                  </div>
                );
              })}
            </div>

            <div className="mt-4">
              <span className="flex items-center gap-1.5 text-sm font-semibold text-orange-600">
                <ShieldAlert className="h-4 w-4" />
                Classificação do Risco:
              </span>
              <div className="mt-2 flex gap-3">
                {(
                  [
                    ['Baixo', 'border-green-300 bg-green-50 text-green-800', 'ring-green-500'],
                    ['Moderado', 'border-amber-300 bg-amber-50 text-amber-800', 'ring-amber-500'],
                    ['Alto', 'border-red-300 bg-red-50 text-red-800', 'ring-red-500'],
                  ] as [ClassificacaoRisco, string, string][]
                ).map(([nivel, cores, anel]) => (
                  <button
                    key={nivel}
                    type="button"
                    disabled={somenteLeitura}
                    onClick={() =>
                      setConteudo((c) => ({
                        ...c,
                        avaliacaoRisco: {
                          ...c.avaliacaoRisco,
                          classificacao: c.avaliacaoRisco.classificacao === nivel ? null : nivel,
                        },
                      }))
                    }
                    className={`rounded-md border px-4 py-1.5 text-sm font-medium ${cores} ${
                      conteudo.avaliacaoRisco.classificacao === nivel ? `ring-2 ${anel}` : ''
                    }`}
                  >
                    {nivel}
                  </button>
                ))}
              </div>
            </div>
          </fieldset>
        </section>

        {/* 6. Saúde reprodutiva */}
        <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <TituloSecao numero={6} titulo="SAÚDE REPRODUTIVA" cor="bg-fuchsia-600" />
          <fieldset disabled={somenteLeitura}>
            {/* Faz uso de anticoncepcional? */}
            <div className="mt-3 border-b border-gray-100 pb-2">
              <div className="grid grid-cols-[1fr_auto_auto] items-center gap-x-3">
                <span className="text-sm text-gray-800">Faz uso de anticoncepcional?</span>
                <label className="flex w-8 justify-center">
                  <input
                    type="checkbox"
                    checked={conteudo.saudeReprodutiva.usoAnticoncepcional.resposta === true}
                    onChange={() =>
                      setConteudo((c) => ({
                        ...c,
                        saudeReprodutiva: {
                          ...c.saudeReprodutiva,
                          usoAnticoncepcional: {
                            ...c.saudeReprodutiva.usoAnticoncepcional,
                            resposta:
                              c.saudeReprodutiva.usoAnticoncepcional.resposta === true ? null : true,
                          },
                        },
                      }))
                    }
                    className="h-4 w-4 rounded border-gray-300"
                  />
                </label>
                <label className="flex w-8 justify-center">
                  <input
                    type="checkbox"
                    checked={conteudo.saudeReprodutiva.usoAnticoncepcional.resposta === false}
                    onChange={() =>
                      setConteudo((c) => ({
                        ...c,
                        saudeReprodutiva: {
                          ...c.saudeReprodutiva,
                          usoAnticoncepcional: {
                            ...c.saudeReprodutiva.usoAnticoncepcional,
                            resposta:
                              c.saudeReprodutiva.usoAnticoncepcional.resposta === false ? null : false,
                          },
                        },
                      }))
                    }
                    className="h-4 w-4 rounded border-gray-300"
                  />
                </label>
              </div>
              <div className="mt-1 grid grid-cols-[1fr_auto_auto] gap-x-3 text-[10px] font-semibold uppercase text-gray-400">
                <span />
                <span className="w-8 text-center">Sim</span>
                <span className="w-8 text-center">Não</span>
              </div>
              {conteudo.saudeReprodutiva.usoAnticoncepcional.resposta === true ? (
                <input
                  type="text"
                  placeholder="Qual?"
                  value={conteudo.saudeReprodutiva.usoAnticoncepcional.observacao}
                  onChange={(e) =>
                    setConteudo((c) => ({
                      ...c,
                      saudeReprodutiva: {
                        ...c.saudeReprodutiva,
                        usoAnticoncepcional: {
                          ...c.saudeReprodutiva.usoAnticoncepcional,
                          observacao: e.target.value,
                        },
                      },
                    }))
                  }
                  className="mt-1 w-full rounded-md border border-gray-200 px-2 py-1 text-xs"
                />
              ) : null}
            </div>

            {/* Ainda menstrua? */}
            <div className="mt-3 border-b border-gray-100 pb-2">
              <div className="grid grid-cols-[1fr_auto_auto] items-center gap-x-3">
                <span className="text-sm text-gray-800">Ainda menstrua?</span>
                <label className="flex w-8 justify-center">
                  <input
                    type="checkbox"
                    checked={conteudo.saudeReprodutiva.aindaMenstrua.resposta === true}
                    onChange={() =>
                      setConteudo((c) => ({
                        ...c,
                        saudeReprodutiva: {
                          ...c.saudeReprodutiva,
                          aindaMenstrua: {
                            ...c.saudeReprodutiva.aindaMenstrua,
                            resposta: c.saudeReprodutiva.aindaMenstrua.resposta === true ? null : true,
                          },
                        },
                      }))
                    }
                    className="h-4 w-4 rounded border-gray-300"
                  />
                </label>
                <label className="flex w-8 justify-center">
                  <input
                    type="checkbox"
                    checked={conteudo.saudeReprodutiva.aindaMenstrua.resposta === false}
                    onChange={() =>
                      setConteudo((c) => ({
                        ...c,
                        saudeReprodutiva: {
                          ...c.saudeReprodutiva,
                          aindaMenstrua: {
                            ...c.saudeReprodutiva.aindaMenstrua,
                            resposta:
                              c.saudeReprodutiva.aindaMenstrua.resposta === false ? null : false,
                          },
                        },
                      }))
                    }
                    className="h-4 w-4 rounded border-gray-300"
                  />
                </label>
              </div>
              {conteudo.saudeReprodutiva.aindaMenstrua.resposta === true ? (
                <label className="mt-1 block text-xs text-gray-600">
                  Data da última menstruação:
                  <input
                    type="date"
                    value={conteudo.saudeReprodutiva.aindaMenstrua.dataUltimaMenstruacao}
                    onChange={(e) =>
                      setConteudo((c) => ({
                        ...c,
                        saudeReprodutiva: {
                          ...c.saudeReprodutiva,
                          aindaMenstrua: {
                            ...c.saudeReprodutiva.aindaMenstrua,
                            dataUltimaMenstruacao: e.target.value,
                          },
                        },
                      }))
                    }
                    className="mt-0.5 w-full rounded-md border border-gray-200 px-2 py-1 text-xs"
                  />
                </label>
              ) : null}
            </div>

            {/* Número de filhos */}
            <label className="mt-3 block text-sm text-gray-800">
              Número de filhos:
              <input
                type="number"
                min={0}
                inputMode="numeric"
                value={conteudo.saudeReprodutiva.numeroFilhos ?? ''}
                onChange={(e) =>
                  setConteudo((c) => ({
                    ...c,
                    saudeReprodutiva: {
                      ...c.saudeReprodutiva,
                      numeroFilhos: e.target.value === '' ? null : Math.max(0, Number(e.target.value)),
                    },
                  }))
                }
                className="mt-0.5 w-28 rounded-md border border-gray-300 px-2 py-1.5 text-sm"
              />
            </label>
          </fieldset>
        </section>

        {/* 7. O que a requisição do SISCAN exige além do formulário de papel */}
        <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <TituloSecao numero={7} titulo="REQUISIÇÃO DO SISCAN" cor="bg-teal-600" />
          <SecaoSiscan
            valor={conteudo.siscan}
            jaFezMamografia={conteudo.historicoClinico.jaRealizouMamografia.resposta === true}
            jaFezCirurgia={
              conteudo.historicoClinico.jaRealizouCirurgiaMamaria.resposta === true ||
              conteudo.historicoClinico.possuiProteseMamaria.resposta === true
            }
            somenteLeitura={somenteLeitura}
            aoMudar={(mudanca) =>
              setConteudo((c) => ({ ...c, siscan: { ...c.siscan, ...mudanca } }))
            }
          />
        </section>
      </div>
      )}

      {/* 8. Documentos / exames anexados (ponte QR → PWA) */}
      <AnexosExameSecao solicitacaoExameId={ctx.solicitacaoExameId} podeEditar={podeAnexar} />

      {/* Rodapé */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-xs text-gray-500">
          {ctx.anamnese
            ? `Preenchida em ${new Date(ctx.anamnese.criadoEm).toLocaleString('pt-BR')}${
                ctx.anamnese.atualizadoEm
                  ? ` · atualizada em ${new Date(ctx.anamnese.atualizadoEm).toLocaleString('pt-BR')}`
                  : ''
              }`
            : 'Anamnese ainda não preenchida.'}
        </p>
        {podeEditar ? (
          <Button onClick={aoSalvar} disabled={salvar.isPending}>
            {salvar.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            Salvar anamnese
          </Button>
        ) : null}
      </div>

      {/* ---- Requisição no SISCAN ---- */}

      <ModalLoginSiscan
        aberto={loginSiscan}
        aoFechar={() => setLoginSiscan(false)}
        aoAutenticar={() => setGerarSiscan(true)}
      />

      <ModalGerarRequisicaoSiscan
        aberto={gerarSiscan}
        exameImagemId={ctx.solicitacaoExameId}
        aoFechar={() => {
          setGerarSiscan(false);
          // Quem pediu para sair e parou aqui para gerar continua saindo depois.
          if (saindoApos) {
            setSaindoApos(false);
            navigate(-1);
          }
        }}
        aoGerar={() => contexto.refetch()}
      />

      {/* O aviso de que a requisição ainda não existe. Aparece ao salvar e ao sair, porque é
          nesses dois momentos que a pessoa ainda está aqui — depois vira pendência invisível. */}
      <Modal
        aberto={perguntarSiscan !== null}
        aoFechar={() => setPerguntarSiscan(null)}
        titulo="Ainda não há requisição no SISCAN"
        largura="sm"
      >
        <div className="space-y-4">
          <p className="text-sm text-gray-700">
            {perguntarSiscan === 'salvou'
              ? 'A anamnese foi salva, mas este exame ainda não tem requisição no SISCAN. Sem ela, a médica não consegue lançar o resultado lá.'
              : 'Você está saindo e este exame ainda não tem requisição no SISCAN. Sem ela, a médica não consegue lançar o resultado lá.'}
          </p>
          <div className="flex justify-end gap-2">
            <Button
              variante="secundaria"
              onClick={() => {
                setPerguntarSiscan(null);
                if (perguntarSiscan === 'salvou') notificar('Anamnese salva com sucesso.');
                navigate(-1);
              }}
            >
              {perguntarSiscan === 'salvou' ? 'Agora não' : 'Sair assim mesmo'}
            </Button>
            <Button
              onClick={() => {
                setSaindoApos(true);
                abrirGeracao();
              }}
            >
              <FileCheck2 className="mr-2 h-4 w-4" />
              Gerar agora
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
