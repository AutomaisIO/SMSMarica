import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft, ClipboardList, Loader2, Save, ShieldAlert, User } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { useContextoAnamnese, useSalvarAnamnese } from '@/features/anamnese/api/queries';
import { AnexosExameSecao } from '@/features/anamnese/components/AnexosExameSecao';
import { DiagramaMamas } from '@/features/anamnese/components/DiagramaMamas';
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

export function AnamnesePage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const solicitacaoId = params.get('solicitacaoId') ?? undefined;
  const accession = params.get('accession') ?? undefined;

  const contexto = useContextoAnamnese({
    solicitacaoExameId: solicitacaoId,
    accessionNumber: accession,
  });
  const salvar = useSalvarAnamnese();
  const podeEditar = usePermissao('SolicitacoesExame', 'Edicao');

  const [conteudo, setConteudo] = useState<AnamneseMamografiaConteudo>(conteudoVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  // Hidrata o form com a anamnese existente (merge defensivo sobre o shape vazio).
  useEffect(() => {
    const json = contexto.data?.anamnese?.conteudoJson;
    if (!json) return;
    try {
      const carregado = JSON.parse(json) as Partial<AnamneseMamografiaConteudo>;
      const base = conteudoVazio();
      setConteudo({
        avaliacaoClinica: { ...base.avaliacaoClinica, ...carregado.avaliacaoClinica },
        historicoClinico: { ...base.historicoClinico, ...carregado.historicoClinico },
        queixas: {
          ...base.queixas,
          ...carregado.queixas,
          sintomas: { ...base.queixas.sintomas, ...carregado.queixas?.sintomas },
        },
        avaliacaoRisco: { ...base.avaliacaoRisco, ...carregado.avaliacaoRisco },
      });
    } catch {
      setErro('Não foi possível ler a anamnese gravada; o formulário foi aberto em branco.');
    }
  }, [contexto.data?.anamnese?.conteudoJson]);

  const idade = useMemo(
    () => calcularIdade(contexto.data?.pacienteNascimento ?? null),
    [contexto.data?.pacienteNascimento],
  );

  async function aoSalvar() {
    if (!contexto.data) return;
    setErro(null);
    setSalvo(false);
    try {
      await salvar.mutateAsync({
        solicitacaoExameId: contexto.data.solicitacaoExameId,
        payload: {
          tipo: 'mamografia',
          versao: 1,
          conteudoJson: JSON.stringify(conteudo),
          classificacaoRisco: conteudo.avaliacaoRisco.classificacao,
        },
      });
      setSalvo(true);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

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
      <div className="space-y-3">
        <button
          type="button"
          onClick={() => navigate(-1)}
          className="inline-flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
        >
          <ArrowLeft className="h-4 w-4" /> Voltar
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
    <div className="mx-auto max-w-5xl space-y-6 pb-10">
      {/* Cabeçalho */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <button
            type="button"
            onClick={() => navigate(-1)}
            className="inline-flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
          >
            <ArrowLeft className="h-4 w-4" /> Voltar
          </button>
          <h1 className="mt-1 flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <ClipboardList className="h-6 w-6 text-primary-600" />
            Questionário para Exame de Mamografia
          </h1>
          <p className="text-sm text-gray-500">
            Pedido <span className="font-mono">{ctx.accessionNumber}</span> · {ctx.tipoExameNome}
            {ctx.anamnese?.preenchidoPorNome ? (
              <> · preenchida por {ctx.anamnese.preenchidoPorNome}</>
            ) : null}
          </p>
        </div>
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

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}
      {salvo ? (
        <div className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
          Anamnese salva com sucesso.
        </div>
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
      </div>

      {/* 6. Documentos / exames anexados (ponte QR → PWA) */}
      <AnexosExameSecao solicitacaoExameId={ctx.solicitacaoExameId} podeEditar={podeEditar} />

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
    </div>
  );
}
