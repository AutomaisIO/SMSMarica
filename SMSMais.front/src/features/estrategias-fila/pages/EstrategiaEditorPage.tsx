import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { Archive, ArrowLeft, Bot, CheckCircle2, Loader2, Play, RotateCcw, Save, Target } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { AvisoCobertura } from '@/features/agenda/components/AvisoCobertura';
import { diaBrasilia } from '@/features/agenda/lib/datasAgenda';
import {
  useArquivarEstrategia,
  useAtualizarEstrategia,
  useCenario,
  useCriarEstrategia,
  useEstrategia,
  useMarcarAplicada,
  useRodada,
  useRodar,
  useSimular,
} from '@/features/estrategias-fila/api/queries';
import { CenarioAtualCard } from '@/features/estrategias-fila/components/CenarioAtualCard';
import { HistoricoRodadas } from '@/features/estrategias-fila/components/HistoricoRodadas';
import { PainelParametros } from '@/features/estrategias-fila/components/PainelParametros';
import { ProjecaoChart } from '@/features/estrategias-fila/components/ProjecaoChart';
import { PropostaAgente } from '@/features/estrategias-fila/components/PropostaAgente';
import { STATUS_ROTULO, dataHoraBr, diferencas } from '@/features/estrategias-fila/lib/parametros';
import { obterCenario } from '@/features/estrategias-fila/api/estrategiasApi';
import type { CenarioFila, ParametrosEstrategia, Projecao, Rodada } from '@/features/estrategias-fila/types';

/**
 * A tela onde se simula: cenário atual em cima, projeção no meio, parâmetros com cadeados ao lado
 * e a proposta do agente quando houver.
 *
 * <p>Serve às duas rotas — <c>/nova?codigo=&nome=</c> (ainda não salva) e <c>/:id</c> (salva).
 * Na nova, "Simular" é instantâneo e nada é gravado; pedir ao agente exige salvar primeiro,
 * porque a rodada do agente custa dinheiro e precisa ficar registrada.</p>
 */
export function EstrategiaEditorPage() {
  const { id } = useParams<{ id: string }>();
  const [busca] = useSearchParams();
  const navigate = useNavigate();

  const podeIncluir = usePermissao('EstrategiasFila', 'Inclusao');
  const podeEditar = usePermissao('EstrategiasFila', 'Edicao');
  const podeExcluir = usePermissao('EstrategiasFila', 'Exclusao');

  const codigoNovo = busca.get('codigo');
  const nomeNovo = busca.get('nome');

  const estrategia = useEstrategia(id);
  const cenarioNovo = useCenario(codigoNovo, id ? null : nomeNovo);

  const simular = useSimular();
  const criar = useCriarEstrategia();
  const atualizar = useAtualizarEstrategia();
  const rodar = useRodar();
  const marcarAplicada = useMarcarAplicada();
  const arquivar = useArquivarEstrategia();

  // ---- estado local ----
  const [parametros, setParametros] = useState<ParametrosEstrategia | null>(null);
  const [projecao, setProjecao] = useState<Projecao | null>(null);
  const [cenario, setCenario] = useState<CenarioFila | null>(null);
  const [rodadaExibida, setRodadaExibida] = useState<Rodada | null>(null);
  const [rodadaSelecionada, setRodadaSelecionada] = useState<number | null>(null);
  const [nome, setNome] = useState('');
  const [modalSalvar, setModalSalvar] = useState<'salvar' | 'agente' | null>(null);
  const [modalAplicar, setModalAplicar] = useState(false);
  const [notaAplicacao, setNotaAplicacao] = useState('');
  const [confirmarArquivar, setConfirmarArquivar] = useState(false);
  const [sujo, setSujo] = useState(false);

  const rodadaDetalhe = useRodada(id, rodadaSelecionada);

  // Carga inicial: da estratégia salva (rodada atual) ou do cenário novo.
  useEffect(() => {
    if (id && estrategia.data) {
      const e = estrategia.data;
      setNome(e.nome);
      setParametros(e.parametros);
      if (e.rodadaAtual) {
        setCenario(e.rodadaAtual.cenario);
        setProjecao(e.rodadaAtual.projecao);
        setRodadaExibida(e.rodadaAtual);
        setRodadaSelecionada(e.rodadaAtual.numero);
      }
      setSujo(false);
    }
  }, [id, estrategia.data]);

  useEffect(() => {
    if (!id && cenarioNovo.data) {
      setCenario(cenarioNovo.data.cenario);
      setParametros(cenarioNovo.data.cenario.parametrosIniciais);
      setProjecao(cenarioNovo.data.projecao);
      setNome(`Zerar a fila de ${cenarioNovo.data.cenario.procedimento.nome}`.slice(0, 200));
    }
  }, [id, cenarioNovo.data]);

  // Ao clicar numa rodada do histórico, exibe o que ela produziu (sem mexer nos parâmetros vigentes).
  useEffect(() => {
    if (rodadaDetalhe.data) {
      setRodadaExibida(rodadaDetalhe.data);
      setProjecao(rodadaDetalhe.data.projecao);
      setCenario(rodadaDetalhe.data.cenario);
    }
  }, [rodadaDetalhe.data]);

  const procedimentoCodigo = id ? (estrategia.data?.procedimentoCodigo ?? null) : codigoNovo;
  const procedimentoNome = id ? estrategia.data?.procedimentoNome : nomeNovo;

  // A curva "como está hoje" (cenário sem mudança), em cinza, para comparar. Na estratégia nova
  // ela vem junto com o cenário; na salva, sai de uma simulação com os parâmetros iniciais da
  // rodada atual — uma chamada, só na abertura.
  const base = !id && cenarioNovo.data ? cenarioNovo.data.projecao : null;
  const [baseSalva, setBaseSalva] = useState<Projecao | null>(null);
  useEffect(() => {
    if (id && estrategia.data?.rodadaAtual && procedimentoNome && !baseSalva) {
      simular
        .mutateAsync({ codigo: procedimentoCodigo, nome: procedimentoNome, parametros: estrategia.data.rodadaAtual.cenario.parametrosIniciais })
        .then((r) => setBaseSalva(r.projecao))
        .catch(() => undefined);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, estrategia.data?.rodadaAtual?.id, procedimentoNome]);

  const mudancas = useMemo(
    () => (rodadaExibida ? diferencas(rodadaExibida.parametrosEntrada, rodadaExibida.parametrosResultado) : []),
    [rodadaExibida],
  );

  // ---- ações ----
  const [voltando, setVoltando] = useState(false);

  /**
   * Volta tudo para "como está hoje": busca o cenário de novo (não o snapshot da rodada) e põe os
   * parâmetros iniciais dele. Não grava — é só a tela.
   */
  async function aoVoltarParaHoje() {
    if (!procedimentoNome) return;
    setVoltando(true);
    try {
      const r = await obterCenario(procedimentoCodigo, procedimentoNome);
      setCenario(r.cenario);
      setParametros(r.cenario.parametrosIniciais);
      setProjecao(r.projecao);
      setBaseSalva(r.projecao);
      setRodadaExibida(null);
      setRodadaSelecionada(null);
      setSujo(!!id);
      notificar('Parâmetros voltaram para o cenário de hoje.', 'info');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    } finally {
      setVoltando(false);
    }
  }

  async function aoSimular() {
    if (!parametros || !procedimentoNome) return;
    try {
      const r = await simular.mutateAsync({ codigo: procedimentoCodigo, nome: procedimentoNome, parametros });
      setProjecao(r.projecao);
      setCenario(r.cenario);
      setRodadaExibida(null);
      setRodadaSelecionada(null);
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  async function aoSalvar() {
    if (!parametros || !procedimentoNome) return;
    try {
      if (!id) {
        const novoId = await criar.mutateAsync({ nome: nome.trim(), procedimentoCodigo, procedimentoNome, parametros });
        notificar('Estratégia salva.');
        setModalSalvar(null);
        navigate(`/app/agenda/estrategias/${novoId}`, { replace: true });
        return novoId;
      }
      await atualizar.mutateAsync({ id, nome: nome.trim(), parametros });
      notificar('Estratégia atualizada.');
      setSujo(false);
      setModalSalvar(null);
      return id;
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
      return null;
    }
  }

  async function aoRodar(modo: 'Manual' | 'Agente') {
    if (!parametros) return;
    if (!id) {
      // Rodada precisa de estratégia salva (fica registrada e, no agente, custa dinheiro).
      setModalSalvar('agente');
      return;
    }
    try {
      const r = await rodar.mutateAsync({ id, modo, parametros });
      setRodadaExibida(r);
      setRodadaSelecionada(r.numero);
      setProjecao(r.projecao);
      setCenario(r.cenario);
      if (r.falha) notificar(r.falha, 'erro');
      else {
        setParametros(r.parametrosResultado);
        setSujo(false);
        notificar(modo === 'Agente' ? 'O agente propôs uma estratégia.' : 'Rodada gravada.');
      }
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  async function aoConfirmarSalvarEAgente() {
    if (!parametros || !procedimentoNome) return;
    try {
      const novoId = await criar.mutateAsync({ nome: nome.trim(), procedimentoCodigo, procedimentoNome, parametros });
      setModalSalvar(null);
      navigate(`/app/agenda/estrategias/${novoId}`, { replace: true });
      const r = await rodar.mutateAsync({ id: novoId, modo: 'Agente', parametros });
      if (r.falha) notificar(r.falha, 'erro');
      else notificar('O agente propôs uma estratégia.');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  async function aoMarcarAplicada() {
    if (!id) return;
    try {
      await marcarAplicada.mutateAsync({ id, nota: notaAplicacao.trim() || null });
      notificar('Marcada como aplicada.');
      setModalAplicar(false);
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  async function aoArquivar() {
    if (!id) return;
    try {
      await arquivar.mutateAsync(id);
      notificar('Estratégia arquivada.');
      navigate('/app/agenda/estrategias');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  // ---- render ----
  const carregando = id ? estrategia.isPending : cenarioNovo.isPending;
  const erro = id ? estrategia.error : cenarioNovo.error;
  const status = estrategia.data?.status;
  const somenteLeitura = status === 'Arquivada' || (id ? !podeEditar : !podeIncluir && !podeEditar);
  const ocupado = simular.isPending || rodar.isPending || criar.isPending || atualizar.isPending;

  if (!id && !nomeNovo) {
    return (
      <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
        Escolha um procedimento na lista para simular.
      </p>
    );
  }

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <button type="button" onClick={() => navigate('/app/agenda/estrategias')} className="mb-1 inline-flex items-center gap-1 text-xs text-gray-500 hover:text-gray-800">
            <ArrowLeft className="h-3 w-3" /> Estratégias de fila
          </button>
          <h1 className="flex items-center gap-2 text-xl font-semibold text-gray-900">
            <Target className="h-5 w-5 text-primary-600" />
            <span className="truncate">{id ? nome || '…' : procedimentoNome}</span>
            {status ? <span className={`rounded-full px-2 py-0.5 text-[10px] ${STATUS_ROTULO[status].classe}`}>{STATUS_ROTULO[status].rotulo}</span> : null}
          </h1>
          <p className="text-xs text-gray-500">
            {id ? procedimentoNome : 'Ainda não salva — simule à vontade; salve quando quiser guardar ou pedir ao agente.'}
            {procedimentoCodigo ? ` · código ${procedimentoCodigo}` : ''}
            {estrategia.data?.aplicadaEm ? ` · aplicada em ${dataHoraBr(estrategia.data.aplicadaEm)}` : ''}
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button variante="ghost" tamanho="sm" disabled={ocupado || voltando || !procedimentoNome} onClick={aoVoltarParaHoje} title="Descarta as mudanças da tela e recarrega o cenário e os parâmetros como estão hoje no SISREG. Não grava.">
            {voltando ? <Loader2 className="h-4 w-4 animate-spin" /> : <RotateCcw className="h-4 w-4" />} Voltar para hoje
          </Button>
          <Button variante="outline" tamanho="sm" disabled={ocupado || !parametros} onClick={aoSimular} title="Recalcula a projeção com os parâmetros da tela. Não grava.">
            {simular.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Play className="h-4 w-4" />} Simular
          </Button>
          {!somenteLeitura ? (
            <Button
              variante="secundaria"
              tamanho="sm"
              disabled={ocupado || !parametros || (!id && !podeIncluir)}
              onClick={() => (id ? aoRodar('Agente') : setModalSalvar('agente'))}
              title="O agente escolhe os parâmetros livres (respeitando os travados) e propõe ações. Leva de 30 a 90 s e gasta uma chamada de IA."
            >
              {rodar.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Bot className="h-4 w-4" />} Pedir estratégia ao agente
            </Button>
          ) : null}
          {!somenteLeitura ? (
            <Button variante="primaria" tamanho="sm" disabled={ocupado || !parametros || (!id && !podeIncluir)} onClick={() => (id ? aoSalvar() : setModalSalvar('salvar'))}>
              <Save className="h-4 w-4" /> {id ? (sujo ? 'Salvar alterações' : 'Salvar') : 'Salvar estratégia'}
            </Button>
          ) : null}
          {id && podeEditar && status !== 'Aplicada' && status !== 'Arquivada' ? (
            <Button variante="ghost" tamanho="sm" onClick={() => setModalAplicar(true)} title="Anota que alguém executou esta estratégia por fora (no SISREG, por exemplo). Nada é enviado a lugar nenhum.">
              <CheckCircle2 className="h-4 w-4" /> Marcar como aplicada
            </Button>
          ) : null}
          {id && podeExcluir && status !== 'Arquivada' ? (
            <Button variante="ghost" tamanho="sm" onClick={() => setConfirmarArquivar(true)}>
              <Archive className="h-4 w-4" /> Arquivar
            </Button>
          ) : null}
        </div>
      </header>

      {rodar.isPending && rodar.variables?.modo === 'Agente' ? (
        <p className="flex items-center gap-2 rounded-md border border-violet-200 bg-violet-50 px-3 py-2 text-xs text-violet-800">
          <Loader2 className="h-4 w-4 animate-spin" /> O agente está testando combinações no simulador. Isso leva de 30 a 90 segundos — pode continuar olhando o cenário.
        </p>
      ) : null}

      {erro ? <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">{extrairMensagemDeErro(erro)}</p> : null}

      {carregando ? (
        <div className="flex items-center justify-center py-16 text-gray-400">
          <Loader2 className="h-5 w-5 animate-spin" />
          <span className="ml-2 text-xs">Montando o cenário atual…</span>
        </div>
      ) : null}

      {cenario ? (
        <>
          <AvisoCobertura de={diaBrasilia(-84)} ate={diaBrasilia()} />
          <CenarioAtualCard cenario={cenario} />

          <div className="grid gap-4 xl:grid-cols-[1fr_28rem]">
            <div className="space-y-4">
              <ProjecaoChart projecao={projecao} base={base ?? baseSalva} prazoAlvo={parametros?.prazoAlvoSemanas ?? null} carregando={simular.isPending} />
              {rodadaExibida?.modo === 'Agente' ? <PropostaAgente rodada={rodadaExibida} mudancas={mudancas} /> : null}
              {estrategia.data ? (
                <HistoricoRodadas
                  rodadas={estrategia.data.rodadas}
                  atualId={estrategia.data.rodadaAtual?.id ?? null}
                  selecionada={rodadaSelecionada}
                  aoSelecionar={setRodadaSelecionada}
                />
              ) : null}
            </div>
            {parametros ? (
              <PainelParametros
                parametros={parametros}
                desabilitado={somenteLeitura || ocupado}
                aoMudar={(p) => {
                  setParametros(p);
                  setSujo(true);
                }}
              />
            ) : null}
          </div>
          {rodadaExibida && rodadaExibida.numero !== estrategia.data?.rodadaAtual?.numero ? (
            <p className="text-[11px] text-gray-500">
              Exibindo a rodada {rodadaExibida.numero} ({dataHoraBr(rodadaExibida.criadoEm)}). Os parâmetros ao lado continuam os vigentes.
            </p>
          ) : null}
        </>
      ) : null}

      {/* Salvar / salvar e pedir ao agente */}
      <Modal aberto={modalSalvar !== null} aoFechar={() => setModalSalvar(null)} titulo={modalSalvar === 'agente' ? 'Salvar e pedir ao agente' : 'Salvar estratégia'} largura="sm">
        <div className="space-y-3">
          {modalSalvar === 'agente' ? (
            <p className="text-xs text-gray-600">A rodada do agente gasta uma chamada de IA e fica registrada. Dê um nome à estratégia para guardá-la.</p>
          ) : null}
          <label className="block text-xs">
            <span className="mb-1 block text-gray-600">Nome</span>
            <Input value={nome} onChange={(e) => setNome(e.target.value)} maxLength={200} autoFocus />
          </label>
          <div className="flex justify-end gap-2">
            <Button variante="ghost" tamanho="sm" onClick={() => setModalSalvar(null)}>
              Cancelar
            </Button>
            <Button tamanho="sm" disabled={!nome.trim() || criar.isPending} onClick={() => (modalSalvar === 'agente' ? aoConfirmarSalvarEAgente() : aoSalvar())}>
              {criar.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              {modalSalvar === 'agente' ? 'Salvar e pedir ao agente' : 'Salvar'}
            </Button>
          </div>
        </div>
      </Modal>

      {/* Marcar como aplicada */}
      <Modal aberto={modalAplicar} aoFechar={() => setModalAplicar(false)} titulo="Marcar como aplicada" largura="sm">
        <div className="space-y-3">
          <p className="text-xs text-gray-600">
            Isto só anota que a estratégia foi executada por fora (escala aberta no SISREG, profissional habilitado…). O sistema não
            envia nada a lugar nenhum.
          </p>
          <label className="block text-xs">
            <span className="mb-1 block text-gray-600">O que foi feito (opcional)</span>
            <textarea className="input min-h-24" value={notaAplicacao} onChange={(e) => setNotaAplicacao(e.target.value)} maxLength={2000} />
          </label>
          <div className="flex justify-end gap-2">
            <Button variante="ghost" tamanho="sm" onClick={() => setModalAplicar(false)}>
              Cancelar
            </Button>
            <Button tamanho="sm" disabled={marcarAplicada.isPending} onClick={aoMarcarAplicada}>
              Confirmar
            </Button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        aberto={confirmarArquivar}
        titulo="Arquivar estratégia"
        mensagem="A estratégia some das listas, mas fica guardada com todas as rodadas. Continuar?"
        aoCancelar={() => setConfirmarArquivar(false)}
        aoConfirmar={() => {
          setConfirmarArquivar(false);
          aoArquivar();
        }}
      />
    </div>
  );
}
