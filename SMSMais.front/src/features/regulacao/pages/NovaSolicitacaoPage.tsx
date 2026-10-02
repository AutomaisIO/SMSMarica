import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  Building2,
  Check,
  FilePlus2,
  Loader2,
  PencilLine,
  Save,
  Send,
  Undo2,
} from 'lucide-react';

import { useAuth } from '@/shared/auth/authStore';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { UploadAnexo, type ArquivoResumo } from '@/shared/ui/UploadAnexo';
import { SeletorAcervo } from '@/shared/acervo/SeletorAcervo';
import { VisualizadorArquivo } from '@/shared/acervo/VisualizadorArquivo';
import { CampoDinamico } from '@/shared/regulacao/CampoDinamico';
import { notificar } from '@/shared/ui/Notificacoes';
import { cn } from '@/shared/lib/cn';

import { useConfiguracaoFluxo, useProcedimento } from '../api/queries';
import {
  useAnexarArquivo,
  useAtualizarSolicitacao,
  useCriarSolicitacao,
  useEnviarParaFila,
  useExigencias,
  useFormularioRegulacao,
  usePendencias,
  useRemoverArquivo,
  useSolicitacao,
} from '../api/solicitacoesQueries';
import { BuscaProcedimento } from '../components/BuscaProcedimento';
import { PassoPaciente } from '../components/wizard/PassoPaciente';
import { PassoRegras } from '../components/wizard/PassoRegras';
import { ExamesInternosSugeridos } from '../components/ExamesInternosSugeridos';
import { CHAVE_CIDS_SECUNDARIOS, CidsSecundarios } from '../components/CidsSecundarios';
import { IncluirMedico } from '../components/IncluirMedico';
import { MedicoSisreg } from '../components/MedicoSisreg';
import {
  anexarComTitulo,
  anexarDoAcervo,
  caminhoArquivoExigencia,
  caminhoConteudoAcervo,
  listarAcervoDaSolicitacao,
} from '../api/acervoRegulacao';
import { SeletorCidRegulacao } from '../components/SeletorCidRegulacao';
import type { FluxoRegulacao, SolicitacaoRegulacao } from '../tiposSolicitacao';
import { ROTULO_SISTEMA_REGULACAO } from '../types';
import type { PacienteResumoRegulacao, RegulacaoProcedimentoItem, SistemaRegulacao } from '../types';

type Passo = 'procedimento' | 'destino' | 'paciente' | 'regras' | 'formulario' | 'revisao';

const PASSOS: { id: Passo; rotulo: string }[] = [
  { id: 'procedimento', rotulo: 'Procedimento' },
  { id: 'destino', rotulo: 'Destino' },
  { id: 'paciente', rotulo: 'Paciente' },
  // Entrou no incremento 4, entre paciente e formulário: as regras dependem de quem é o
  // paciente (idade, sexo, CID) e decidem o que o formulário vai exigir.
  { id: 'regras', rotulo: 'Regras' },
  { id: 'formulario', rotulo: 'Formulário e anexos' },
  { id: 'revisao', rotulo: 'Revisão' },
];

/** Até aqui o que se escolhe é a ESTRUTURA do pedido; dali em diante a solicitação já existe. */
const ULTIMO_PASSO_DA_ESTRUTURA = PASSOS.findIndex((p) => p.id === 'paciente');

/** Enquanto é da unidade, a solicitação se edita inteira (rascunho de quem abriu, ou devolvida). */
const EDITAVEIS = ['Rascunho', 'Devolvida'];

/**
 * Abertura de solicitação (planos 02 e 10, ordem D-5) — e, pela rota `…/:id/editar`, a
 * continuação de um rascunho ou a correção de uma solicitação devolvida.
 *
 * <p><b>Por que o procedimento vem antes do paciente.</b> É a escolha do procedimento que revela
 * se há oferta interna em Maricá e em quais sistemas externos ele existe — e é isso que decide o
 * destino. Perguntar o paciente primeiro obrigaria a refazer o caminho quando o destino não
 * tivesse oferta.</p>
 *
 * <p><b>A solicitação nasce como rascunho ao sair do passo do paciente</b>: os anexos precisam
 * de um dono, e um rascunho salvo é melhor do que perder o preenchimento se a tela fechar. Daí em
 * diante, voltar e trocar procedimento, destino ou paciente <b>grava a troca</b> ao avançar de
 * novo — antes, a troca ficava só na tela e a solicitação seguia com a escolha antiga.</p>
 *
 * <p><b>Devolvida:</b> a unidade corrige tudo — procedimento, destino, paciente, regras,
 * formulário e anexos — e reenvia para a pré-regulação (pedido do Bernardo, 01/10/2026).</p>
 */
export function NovaSolicitacaoPage() {
  const navegar = useNavigate();
  const queryClient = useQueryClient();
  const config = useConfiguracaoFluxo();
  const { id: idEdicao } = useParams();
  const editando = !!idEdicao;

  const unidades = useAuth((s) => s.unidades);
  const unidadeAtivaId = useAuth((s) => s.unidadeAtivaId);
  const definirUnidadeAtiva = useAuth((s) => s.definirUnidadeAtiva);

  const [passo, setPasso] = useState<Passo>('procedimento');
  const [procedimento, setProcedimento] = useState<RegulacaoProcedimentoItem | null>(null);
  const [fluxo, setFluxo] = useState<FluxoRegulacao | null>(null);
  const [destino, setDestino] = useState<string>('');
  const [paciente, setPaciente] = useState<PacienteResumoRegulacao | null>(null);
  const [solicitacaoId, setSolicitacaoId] = useState<string | null>(idEdicao ?? null);
  const [valores, setValores] = useState<Record<string, string>>({});
  const [carregada, setCarregada] = useState(!editando);
  /** O passo para onde se ia quando o modal de unidade abriu — retomado depois da escolha. */
  const [escolhendoUnidadePara, setEscolhendoUnidadePara] = useState<Passo | null>(null);

  const criar = useCriarSolicitacao();
  const atualizar = useAtualizarSolicitacao();
  const enviar = useEnviarParaFila();
  const anexar = useAnexarArquivo();
  const remover = useRemoverArquivo();

  // A solicitação como está gravada: é contra ela que se decide se a estrutura mudou.
  const atual = useSolicitacao(solicitacaoId);
  const procedimentoGravado = useProcedimento(editando && !carregada ? atual.data?.procedimentoId ?? null : null);

  const formulario = useFormularioRegulacao(procedimento?.id ?? null, fluxo);
  const exigencias = useExigencias(solicitacaoId);
  // Anexos: abrir no visualizador e "anexar do cadastro" (exigência que recebe o escolhido).
  const [arquivoAberto, setArquivoAberto] = useState<ArquivoResumo | null>(null);
  const [exigenciaDoCadastro, setExigenciaDoCadastro] = useState<string | null>(null);
  const pendencias = usePendencias(passo === 'revisao' ? solicitacaoId : null);

  // Edição: preenche o assistente com o que está gravado, uma vez, e abre no formulário — é
  // lá que costuma estar o que a regulação pediu para corrigir. Os passos de cima seguem a um
  // clique na barra.
  useEffect(() => {
    if (carregada || !atual.data || !procedimentoGravado.data) return;
    const s = atual.data;
    const p = procedimentoGravado.data;
    setProcedimento({
      id: p.id,
      nome: p.nome,
      tipo: p.tipo,
      score: 1,
      origens: p.origens,
      executantesInternos: p.executantesInternos,
      existeExterno: p.existeExterno,
    });
    setFluxo(s.fluxo);
    setDestino(s.fluxo === 'Externo' ? (s.sistemaDestino ?? '') : '');
    setPaciente(pacienteGravado(s));
    setValores(comoTexto(s.formulario));
    setPasso('formulario');
    setCarregada(true);
  }, [carregada, atual.data, procedimentoGravado.data]);

  const temInterno = (procedimento?.executantesInternos.length ?? 0) > 0;
  const temExterno =
    !!procedimento?.existeExterno.ser ||
    !!procedimento?.existeExterno.sernit ||
    !!procedimento?.existeExterno.esusSg;

  /**
   * Com oferta interna, o normal é resolver dentro do município — o Externo só aparece quando a
   * configuração permite, ou quando não há oferta interna nenhuma.
   */
  const fluxosDisponiveis = useMemo<FluxoRegulacao[]>(() => {
    const lista: FluxoRegulacao[] = [];
    if (temInterno) lista.push('Interno');
    if (temExterno && (!temInterno || config.data?.permitirExternoComInterno)) lista.push('Externo');
    lista.push('Nar');
    return lista;
  }, [temInterno, temExterno, config.data?.permitirExternoComInterno]);

  const destinosExternos = useMemo(() => {
    const lista: { valor: string; rotulo: string }[] = [];
    if (procedimento?.existeExterno.ser) lista.push({ valor: 'Ser', rotulo: 'SER (SES-RJ)' });
    if (procedimento?.existeExterno.sernit) lista.push({ valor: 'Sernit', rotulo: 'SERNIT (Niterói)' });
    // ESUS de São Gonçalo (ADR-0063): a integração é só leitura — o pedido segue com os campos
    // canônicos (sem bloco dinâmico) e é incluído na tela do próprio ESUS.
    if (procedimento?.existeExterno.esusSg) lista.push({ valor: 'EsusSg', rotulo: 'ESUS São Gonçalo' });
    return lista;
  }, [procedimento]);

  const indice = PASSOS.findIndex((p) => p.id === passo);
  const ocupado = criar.isPending || atualizar.isPending;

  function passoValido(p: Passo): boolean {
    if (p === 'procedimento') return !!procedimento;
    // O NAR ainda não tem o passo da unidade "em nome de" — o backend recusa sem ela, e a tela
    // deixava avançar até o 400.
    if (p === 'destino') return !!fluxo && fluxo !== 'Nar' && (fluxo !== 'Externo' || !!destino);
    if (p === 'paciente') return !!paciente;
    return true;
  }

  /** Dá para chegar ao passo `alvo` com o que está preenchido nos anteriores? */
  function alcancavel(alvo: number): boolean {
    return PASSOS.slice(0, alvo).every((p) => passoValido(p.id));
  }

  function destinoDoFluxo(f: FluxoRegulacao): string | null {
    return f === 'Externo' ? destino : f === 'Nar' ? 'Sisreg' : null;
  }

  /**
   * Sem unidade escolhida no topo, com mais de uma vinculada, o backend não sabe de qual unidade
   * é o pedido e recusa. Perguntar antes, num modal, em vez de deixar o erro sair num toast.
   */
  const precisaEscolherUnidade = !solicitacaoId && unidades.length > 1 && !unidadeAtivaId;

  async function criarRascunho(): Promise<boolean> {
    if (!procedimento || !paciente || !fluxo) return false;
    try {
      const s = await criar.mutateAsync({
        fluxo,
        procedimentoId: procedimento.id,
        pacienteId: paciente.id,
        sistemaDestino: destinoDoFluxo(fluxo),
      });
      setSolicitacaoId(s.id);
      notificar(`Rascunho ${s.numeroLocal} criado.`);
      return true;
    } catch {
      return false; // o interceptor já mostrou o erro
    }
  }

  /** Grava procedimento, fluxo/destino e paciente — só o que mudou em relação ao gravado. */
  async function salvarEstrutura(): Promise<boolean> {
    const s = atual.data;
    if (!solicitacaoId || !s || !procedimento || !paciente || !fluxo) return true;

    const trocouProcedimento = procedimento.id !== s.procedimentoId;
    const trocouFluxo = fluxo !== s.fluxo;
    const trocouDestino = fluxo === 'Externo' && destino !== (s.sistemaDestino ?? '');
    // Mesmo paciente com CPF diferente do gravado = CPF informado depois; reenviar relê o cadastro.
    const trocouPaciente = paciente.id !== s.pacienteId || (paciente.cpf ?? null) !== (s.pacienteCpf ?? null);
    if (!trocouProcedimento && !trocouFluxo && !trocouDestino && !trocouPaciente) return true;

    try {
      const depois = await atualizar.mutateAsync({
        id: solicitacaoId,
        procedimentoId: trocouProcedimento ? procedimento.id : undefined,
        fluxo: trocouFluxo ? fluxo : undefined,
        sistemaDestino: trocouFluxo || trocouDestino ? destinoDoFluxo(fluxo) : undefined,
        pacienteId: trocouPaciente ? paciente.id : undefined,
      });
      // Trocar procedimento/fluxo troca o formulário: o servidor preserva o que sobrevive.
      setValores(comoTexto(depois.formulario));
      if (trocouPaciente && paciente.id !== s.pacienteId) {
        notificar('Paciente trocado. Confira os anexos — os exames do paciente anterior saíram.');
      }
      return true;
    } catch {
      return false;
    }
  }

  async function salvarFormulario() {
    if (!solicitacaoId) return;
    await atualizar.mutateAsync({ id: solicitacaoId, formulario: valores });
  }

  /**
   * "Salvar rascunho": parar e voltar depois. Cria o rascunho se ainda não existe (precisa de
   * procedimento e paciente) ou grava o que está na tela, e leva ao detalhe — de onde se continua
   * pelo "Continuar rascunho". Rascunho é só de quem abriu: ninguém mais o vê na fila.
   */
  async function salvarRascunho() {
    if (!solicitacaoId) {
      if (precisaEscolherUnidade) {
        setEscolhendoUnidadePara(passo);
        return;
      }
      if (!(await criarRascunho())) return;
      notificar('Rascunho salvo. Para continuar, abra-o em Minha fila.', 'sucesso');
      return;
    }
    if (indice <= ULTIMO_PASSO_DA_ESTRUTURA && !(await salvarEstrutura())) return;
    if (passo === 'formulario') await salvarFormulario();
    notificar('Rascunho salvo. Para continuar, use “Continuar rascunho”.', 'sucesso');
    navegar(`/app/regulacao/solicitacoes/${solicitacaoId}`);
  }

  async function irPara(alvo: Passo) {
    const iAlvo = PASSOS.findIndex((p) => p.id === alvo);
    if (iAlvo === indice) return;

    if (passo === 'formulario' && solicitacaoId) await salvarFormulario();

    // Cruzando da estrutura para o resto: o pedido precisa existir (ou refletir o que mudou).
    if (indice <= ULTIMO_PASSO_DA_ESTRUTURA && iAlvo > ULTIMO_PASSO_DA_ESTRUTURA) {
      if (!solicitacaoId) {
        if (precisaEscolherUnidade) {
          setEscolhendoUnidadePara(alvo);
          return;
        }
        if (!(await criarRascunho())) return;
      } else if (!(await salvarEstrutura())) {
        return;
      }
    }
    setPasso(alvo);
  }

  function escolherUnidade(id: string) {
    const alvo = escolhendoUnidadePara;
    definirUnidadeAtiva(id);
    // Tudo que veio do servidor pode depender da unidade ativa — mesmo gesto do seletor do topo.
    void queryClient.invalidateQueries();
    setEscolhendoUnidadePara(null);
    if (alvo) {
      void (async () => {
        if (await criarRascunho()) setPasso(alvo);
      })();
    }
  }

  async function enviarParaAFila() {
    if (!solicitacaoId) return;
    try {
      await salvarFormulario();
      const s = await enviar.mutateAsync(solicitacaoId);
      notificar(`Solicitação ${s.numeroLocal} enviada para a pré-regulação.`);
      navegar(`/app/regulacao/solicitacoes/${s.id}`);
    } catch {
      // Validação com a lista de pendências já chega pelo interceptor.
      pendencias.refetch();
    }
  }

  // ---------------------------------------------------------------- edição: carregando / travada

  if (editando && !carregada) {
    if (atual.isError || procedimentoGravado.isError) {
      return <p className="text-sm text-slate-500">Solicitação não encontrada.</p>;
    }
    return <p className="text-sm text-slate-500">Carregando…</p>;
  }

  const s = atual.data;
  const devolvida = s?.status === 'Devolvida';

  if (editando && s && !EDITAVEIS.includes(s.status)) {
    return (
      <div className="mx-auto max-w-4xl space-y-3">
        <p className="rounded-md border border-slate-200 bg-white p-4 text-sm text-slate-700">
          A solicitação PR-{s.numeroLocal} não está mais com a unidade.
        </p>
        <Link
          to={`/app/regulacao/solicitacoes/${s.id}`}
          className="text-sm text-red-700 hover:underline"
        >
          Abrir a solicitação
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <header className="flex items-center gap-3">
        {editando ? (
          <PencilLine className="size-6 text-red-600" />
        ) : (
          <FilePlus2 className="size-6 text-red-600" />
        )}
        <div>
          <h1 className="text-xl font-semibold">
            {!editando
              ? 'Nova solicitação'
              : devolvida
                ? `Corrigir solicitação PR-${s?.numeroLocal}`
                : `Rascunho PR-${s?.numeroLocal}`}
            <AjudaManual artigo="regulacao-solicitacoes" secao="abrir" />
          </h1>
          {!editando ? (
            <p className="text-sm text-slate-500">
              Escolha o procedimento, o destino e o paciente; o formulário se monta conforme o
              sistema de destino.
            </p>
          ) : null}
        </div>
      </header>

      {devolvida && s?.statusMotivo ? (
        <div className="flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          <Undo2 className="mt-0.5 size-4 shrink-0" />
          <div>
            <p className="font-medium">Devolvida pela regulação</p>
            <p>{s.statusMotivo}</p>
          </div>
        </div>
      ) : null}

      <ol className="flex flex-wrap gap-1 rounded-md border border-slate-200 bg-white p-1 text-sm">
        {PASSOS.map((p, i) => {
          // Depois que a solicitação existe, qualquer passo alcançável abre com um clique.
          const clicavel = !!solicitacaoId && i !== indice && alcancavel(i) && !ocupado;
          return (
            <li key={p.id} className="flex-1">
              <button
                type="button"
                disabled={!clicavel}
                onClick={() => irPara(p.id)}
                className={cn(
                  'w-full rounded px-3 py-1.5 text-center',
                  i === indice
                    ? 'bg-red-600 font-medium text-white'
                    : i < indice
                      ? 'text-emerald-700'
                      : 'text-slate-400',
                  clicavel && 'hover:bg-slate-100',
                )}
              >
                {i < indice ? <Check className="mr-1 inline size-3.5" /> : null}
                {p.rotulo}
              </button>
            </li>
          );
        })}
      </ol>

      <section className="rounded-md border border-slate-200 bg-white p-4">
        {passo === 'procedimento' ? (
          <BuscaProcedimento value={procedimento} onChange={setProcedimento} autoFocus />
        ) : null}

        {passo === 'destino' ? (
          <div className="space-y-4">
            <Campo label="Para onde vai esta solicitação?" htmlFor="fluxo">
              <div className="space-y-2">
                {fluxosDisponiveis.map((f) => (
                  <label key={f} className="flex cursor-pointer items-start gap-2">
                    <input
                      type="radio"
                      name="fluxo"
                      id={f === fluxosDisponiveis[0] ? "fluxo" : undefined}
                      checked={fluxo === f}
                      onChange={() => {
                        setFluxo(f);
                        if (f !== 'Externo') setDestino('');
                      }}
                      className="mt-0.5 accent-red-600"
                    />
                    <span>
                      <span className="block text-sm font-medium text-slate-800">
                        {f === 'Interno' ? 'Interno (SISREG — Maricá)' : f === 'Externo' ? 'Externo' : 'NAR (agendamento indireto)'}
                      </span>
                      <span className="block text-xs text-slate-500">
                        {f === 'Interno'
                          ? `${procedimento?.executantesInternos.length} unidade(s) com vaga no SISREG.`
                          : f === 'Externo'
                            ? 'Executado pelo Estado ou por outro município.'
                            : 'Sempre SISREG, em nome de outra unidade solicitante.'}
                      </span>
                    </span>
                  </label>
                ))}
              </div>
            </Campo>

            {fluxo === 'Externo' ? (
              <Campo label="Sistema de destino" htmlFor="destino">
                <Select
                  id="destino"
                  value={destino}
                  onChange={(e) => setDestino(e.target.value)}
                  className="max-w-xs"
                >
                  <option value="">Selecione…</option>
                  {destinosExternos.map((d) => (
                    <option key={d.valor} value={d.valor}>
                      {d.rotulo}
                    </option>
                  ))}
                </Select>
              </Campo>
            ) : null}

            {fluxo === 'Nar' ? (
              <p className="rounded border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                O NAR precisa da unidade em nome de quem a solicitação é aberta — esse passo ainda
                não existe. Por ora, use Interno ou Externo.
              </p>
            ) : null}
          </div>
        ) : null}

        {passo === 'paciente' ? (
          <PassoPaciente
            value={paciente}
            onChange={setPaciente}
            exigirCpf={config.data?.exigirCpf ?? true}
          />
        ) : null}

        {passo === 'regras' ? (
          <PassoRegras solicitacaoId={solicitacaoId} />
        ) : passo === 'formulario' ? (
          <div className="space-y-5">
            {formulario.isLoading ? <p className="text-sm text-slate-500">Montando o formulário…</p> : null}

            <div className="flex flex-wrap gap-4">
              {(formulario.data?.campos ?? []).map((c) => (
                <div key={c.chave} className={c.tipo === 'cid' && fluxo === 'Externo' ? 'min-w-96 flex-1' : 'min-w-72'}>
                  {/* Hipótese do bloco fixo do SER/SERNIT: caixa de CID do recurso, não texto. */}
                  {c.tipo === 'cid' && fluxo === 'Externo' ? (
                    <SeletorCidRegulacao
                      rotulo={`${c.rotulo}${c.obrigatorio ? ' *' : ''}`}
                      procedimentoId={procedimento?.id ?? null}
                      sistema={(destino || null) as SistemaRegulacao | null}
                      valor={valores[c.chave] ?? ''}
                      onChange={(v) => setValores((atual) => ({ ...atual, [c.chave]: v }))}
                    />
                  ) : (
                  <CampoDinamico
                    // O ESUS SG não tem bloco dinâmico: com ele de destino, os campos que
                    // aparecem vêm do SER/SERNIT — o sistema do aviso é o da origem do campo.
                    sistema={
                      destino === 'Sernit' || (destino !== 'Ser' && c.origens[0] === 'Sernit')
                        ? 'SERNIT'
                        : 'SER'
                    }
                    c={{
                      numero: c.chave,
                      campo: c.chave,
                      rotulo: c.rotulo,
                      tipo: c.tipo,
                      obrigatorio: c.obrigatorio,
                      // Opção de um sistema só vale nele (o médico do SER não existe no SERNIT):
                      // com o destino escolhido, mostra só as dele.
                      opcoes:
                        c.opcoes
                          ?.filter((o) => (destino !== 'Ser' && destino !== 'Sernit') || o.origens.includes(destino))
                          .map((o) => ({ valor: o.valor, rotulo: o.rotulo })) ?? null,
                    }}
                    valor={valores[c.chave] ?? ''}
                    onChange={(v) => setValores((atual) => ({ ...atual, [c.chave]: v }))}
                  />
                  )}
                  {/* CID principal + secundários: os sistemas só têm UM CID na tela, então os
                      secundários vão no fim das Observações na hora de lançar. */}
                  {c.tipo === 'cid' ? (
                    <div className="mt-3">
                      <CidsSecundarios
                        valor={valores[CHAVE_CIDS_SECUNDARIOS] ?? ''}
                        onChange={(v) => setValores((atual) => ({ ...atual, [CHAVE_CIDS_SECUNDARIOS]: v }))}
                        procedimentoId={procedimento?.id ?? null}
                        sistema={(destino || null) as SistemaRegulacao | null}
                        comCaixaDoRecurso={fluxo === 'Externo'}
                      />
                    </div>
                  ) : null}
                  {/* Médico fora da lista: vira pedido de cadastro PENDENTE — quem cadastra no
                      sistema é a regulação, no fim do processo. */}
                  {c.chave === 'medico_solicitante' && (destino === 'Ser' || destino === 'Sernit') ? (
                    <IncluirMedico
                      sistema={destino}
                      aoEscolher={(v) => {
                        setValores((atual) => ({ ...atual, [c.chave]: v }));
                        // O pedido entra na lista do campo como "(aguardando cadastro…)".
                        void formulario.refetch();
                      }}
                    />
                  ) : null}
                  {/* SISREG: o médico é texto digitado. A busca no nosso cadastro (montado das
                      fichas, sem duplicar) só preenche CPF e nome. */}
                  {c.chave === 'profissional_solicitante_cpf' ? (
                    <div className="mt-2">
                      <MedicoSisreg
                        aoEscolher={(cpf, nome) =>
                          setValores((atual) => ({
                            ...atual,
                            profissional_solicitante_cpf: cpf,
                            profissional_solicitante_nome: nome,
                          }))
                        }
                      />
                    </div>
                  ) : null}
                  {c.origens.length === 1 && fluxo === 'Externo' ? (
                    <p className="mt-0.5 text-[11px] text-slate-400">
                      exigido só pelo {ROTULO_SISTEMA_REGULACAO[c.origens[0]]}
                    </p>
                  ) : null}
                </div>
              ))}
            </div>

            <div className="space-y-2 border-t border-slate-200 pt-4">
              <h2 className="text-sm font-semibold text-slate-700">Anexos</h2>
              {(exigencias.data ?? []).map((e) => (
                <div key={e.id}>
                <UploadAnexo
                  titulo={e.titulo}
                  obrigatoria={e.obrigatoria}
                  situacao={e.criticaTexto ?? undefined}
                  accept={config.data?.anexoTiposPermitidos ?? ['application/pdf']}
                  limiteMb={config.data?.anexoLimiteMb ?? 15}
                  arquivos={e.arquivos}
                  pedirTitulo
                  onAbrir={setArquivoAberto}
                  onAnexarDoCadastro={solicitacaoId ? () => setExigenciaDoCadastro(e.id) : undefined}
                  onEnviar={async (files, info) => {
                    for (const arquivo of files) {
                      if (info) {
                        // Com nome e descrição o arquivo também entra no cadastro do paciente.
                        await anexarComTitulo(solicitacaoId!, e.id, arquivo, info.titulo, info.descricao);
                        await queryClient.invalidateQueries({ queryKey: ['regulacao', 'solicitacoes'] });
                      } else {
                        await anexar.mutateAsync({ solicitacaoId: solicitacaoId!, exigenciaId: e.id, arquivo });
                      }
                    }
                  }}
                  onRemover={async (arquivoId) => {
                    await remover.mutateAsync({ solicitacaoId: solicitacaoId!, arquivoId });
                  }}
                />
                {/* Só nas caixinhas de regra: em "Anexos gerais" não há o que sugerir, porque
                    não existe um documento específico sendo pedido. */}
                {e.regraId && solicitacaoId && (
                  <ExamesInternosSugeridos solicitacaoId={solicitacaoId} exigenciaId={e.id} />
                )}
                </div>
              ))}

              {arquivoAberto && solicitacaoId ? (
                <VisualizadorArquivo
                  caminho={caminhoArquivoExigencia(solicitacaoId, arquivoAberto.id)}
                  titulo={arquivoAberto.titulo || arquivoAberto.nome}
                  descricao={arquivoAberto.descricao}
                  nomeArquivo={arquivoAberto.nome}
                  aoFechar={() => setArquivoAberto(null)}
                />
              ) : null}

              {solicitacaoId ? (
                <SeletorAcervo
                  aberto={exigenciaDoCadastro !== null}
                  aoFechar={() => setExigenciaDoCadastro(null)}
                  chaveConsulta={['regulacao', 'solicitacoes', solicitacaoId, 'acervo']}
                  carregar={() => listarAcervoDaSolicitacao(solicitacaoId)}
                  caminhoConteudo={(item) => caminhoConteudoAcervo(solicitacaoId, item)}
                  aoAnexar={async (item) => {
                    await anexarDoAcervo(solicitacaoId, exigenciaDoCadastro!, item.chave);
                    notificar('Documento do cadastro anexado.', 'sucesso');
                    await queryClient.invalidateQueries({ queryKey: ['regulacao', 'solicitacoes'] });
                  }}
                />
              ) : null}
            </div>
          </div>
        ) : null}

        {passo === 'revisao' ? (
          <div className="space-y-4">
            <dl className="grid gap-2 sm:grid-cols-2">
              <Linha rotulo="Procedimento" valor={procedimento?.nome} />
              <Linha rotulo="Destino" valor={fluxo === 'Externo' ? destino : fluxo ?? undefined} />
              <Linha rotulo="Paciente" valor={paciente?.nome} />
              <Linha rotulo="CPF" valor={paciente?.cpf ?? 'não informado'} />
            </dl>

            {pendencias.isLoading ? (
              <p className="text-sm text-slate-500">Conferindo…</p>
            ) : (pendencias.data?.length ?? 0) > 0 ? (
              <div className="rounded-md border border-amber-300 bg-amber-50 p-3">
                <p className="flex items-center gap-1.5 text-sm font-medium text-amber-900">
                  <AlertTriangle className="size-4" /> Ainda falta:
                </p>
                <ul className="mt-1 list-inside list-disc text-sm text-amber-800">
                  {pendencias.data!.map((p) => (
                    <li key={p.codigo}>{p.descricao}</li>
                  ))}
                </ul>
              </div>
            ) : (
              <p className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">
                Tudo certo — a solicitação pode ir para a pré-regulação.
              </p>
            )}
          </div>
        ) : null}
      </section>

      <div className="flex items-center justify-between">
        <Button
          variante="outline"
          disabled={indice === 0 || ocupado}
          onClick={() => irPara(PASSOS[Math.max(indice - 1, 0)].id)}
        >
          <ArrowLeft className="mr-1 size-4" /> Voltar
        </Button>

        {(solicitacaoId || (procedimento && paciente && fluxo)) && passo !== 'revisao' ? (
          <Button variante="ghost" disabled={ocupado} onClick={salvarRascunho}>
            <Save className="mr-1 size-4" /> Salvar rascunho
          </Button>
        ) : null}

        {passo === 'revisao' ? (
          <Button
            disabled={enviar.isPending || (pendencias.data?.length ?? 1) > 0}
            onClick={enviarParaAFila}
          >
            {enviar.isPending ? (
              <Loader2 className="mr-1 size-4 animate-spin" />
            ) : (
              <Send className="mr-1 size-4" />
            )}
            {devolvida ? 'Reenviar para a pré-regulação' : 'Enviar para a pré-regulação'}
          </Button>
        ) : (
          <Button
            disabled={!passoValido(passo) || ocupado}
            onClick={() => irPara(PASSOS[Math.min(indice + 1, PASSOS.length - 1)].id)}
          >
            {ocupado ? <Loader2 className="mr-1 size-4 animate-spin" /> : null}
            Avançar <ArrowRight className="ml-1 size-4" />
          </Button>
        )}
      </div>

      <Modal
        aberto={escolhendoUnidadePara !== null}
        aoFechar={() => setEscolhendoUnidadePara(null)}
        titulo="De qual unidade é esta solicitação?"
        descricao="Você está vinculado a mais de uma unidade e nenhuma está escolhida no topo da tela. A solicitação pertence à unidade que você escolher aqui — ela também passa a ficar selecionada no topo."
        largura="sm"
      >
        <ul className="space-y-2">
          {unidades.map((u) => (
            <li key={u.id}>
              <button
                type="button"
                disabled={criar.isPending}
                onClick={() => escolherUnidade(u.id)}
                className="flex w-full items-center gap-2 rounded-md border border-slate-200 px-3 py-2 text-left text-sm hover:border-red-300 hover:bg-red-50"
              >
                <Building2 className="size-4 shrink-0 text-slate-500" />
                <span className="flex-1">{u.nome}</span>
                {u.principal ? <span className="text-xs text-slate-400">principal</span> : null}
              </button>
            </li>
          ))}
        </ul>
      </Modal>
    </div>
  );
}

/** O paciente como a solicitação o guardou — o suficiente para o passo mostrar e trocar. */
function pacienteGravado(s: SolicitacaoRegulacao): PacienteResumoRegulacao {
  return {
    id: s.pacienteId,
    nome: s.pacienteNome,
    cpf: s.pacienteCpf,
    cns: null,
    nascimento: null,
    sexo: null,
    cpfPendente: !s.pacienteCpf,
  };
}

/** O formulário gravado vem como JSON; os campos da tela são texto. */
function comoTexto(formulario: Record<string, unknown> | null | undefined): Record<string, string> {
  return Object.fromEntries(
    Object.entries(formulario ?? {}).map(([chave, valor]) => [
      chave,
      valor == null ? '' : typeof valor === 'string' ? valor : JSON.stringify(valor),
    ]),
  );
}

function Linha({ rotulo, valor }: { rotulo: string; valor?: string }) {
  return (
    <div className="rounded border border-slate-200 px-3 py-2">
      <dt className="text-xs text-slate-500">{rotulo}</dt>
      <dd className="text-sm text-slate-900">{valor ?? '—'}</dd>
    </div>
  );
}
