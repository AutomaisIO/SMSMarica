import { useState } from 'react';
import {
  ArrowLeft,
  CheckCircle2,
  Loader2,
  Pencil,
  Play,
  Trash2,
  XCircle,
} from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import {
  useCancelarRota,
  useConcluirRota,
  useCriarAlocacao,
  useIniciarRota,
  useRemoverAlocacao,
  useRotaPorId,
  useSessoesElegiveis,
} from '@/features/translados/api/queries';
import { ListaSessoesElegiveis } from '@/features/translados/components/ListaSessoesElegiveis';
import { MapaDeAssentosAlocavel } from '@/features/translados/components/MapaDeAssentosAlocavel';
import { ModalEscolherSessao } from '@/features/translados/components/ModalEscolherSessao';
import type { AlocacaoDto, SessaoElegivel, StatusRota } from '@/features/translados/types';
import { useVeiculoPorId } from '@/features/veiculos/api/queries';

function formatarData(iso: string): string {
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

const ROTULOS_STATUS: Record<StatusRota, string> = {
  Planejada: 'Planejada',
  EmAndamento: 'Em andamento',
  Concluida: 'Concluída',
  Cancelada: 'Cancelada',
};

const CORES_STATUS: Record<StatusRota, string> = {
  Planejada: 'bg-blue-50 text-blue-800 border-blue-200',
  EmAndamento: 'bg-amber-50 text-amber-800 border-amber-200',
  Concluida: 'bg-emerald-50 text-emerald-800 border-emerald-200',
  Cancelada: 'bg-gray-100 text-gray-700 border-gray-200',
};

export function TransladoDetalhePage() {
  const navigate = useNavigate();
  const params = useParams<{ id: string }>();
  const rotaId = params.id ?? '';

  const detalhe = useRotaPorId(rotaId || null);
  const veiculo = useVeiculoPorId(detalhe.data?.veiculoId ?? null);
  const elegiveis = useSessoesElegiveis(
    detalhe.data?.status === 'Planejada' ? rotaId : null,
  );

  const iniciar = useIniciarRota();
  const concluir = useConcluirRota();
  const cancelar = useCancelarRota();
  const criarAlocacao = useCriarAlocacao();
  const removerAlocacao = useRemoverAlocacao();

  const [sessaoSelecionada, setSessaoSelecionada] = useState<SessaoElegivel | null>(null);
  const [modalAssento, setModalAssento] = useState<
    { assentoId: string; fileiraOrdem: number; numero: number } | null
  >(null);
  const [alocacaoParaRemover, setAlocacaoParaRemover] = useState<AlocacaoDto | null>(null);
  const [confirmarCancelamento, setConfirmarCancelamento] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const r = detalhe.data;
  const podeEditar = r?.status === 'Planejada';

  async function aoClicarAssentoVazio(info: {
    fileiraOrdem: number;
    numero: number;
    assentoId: string;
  }) {
    if (!r) return;
    setErro(null);

    if (sessaoSelecionada) {
      try {
        await criarAlocacao.mutateAsync({
          rotaId: r.id,
          payload: { sessaoId: sessaoSelecionada.sessaoId, assentoId: info.assentoId },
        });
        setSessaoSelecionada(null);
      } catch (e) {
        setErro(extrairMensagemDeErro(e));
      }
      return;
    }

    setModalAssento(info);
  }

  async function escolherNoModal(s: SessaoElegivel) {
    if (!r || !modalAssento) return;
    setErro(null);
    try {
      await criarAlocacao.mutateAsync({
        rotaId: r.id,
        payload: { sessaoId: s.sessaoId, assentoId: modalAssento.assentoId },
      });
      setModalAssento(null);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function confirmarRemoverAlocacao() {
    if (!r || !alocacaoParaRemover) return;
    setErro(null);
    try {
      await removerAlocacao.mutateAsync({
        rotaId: r.id,
        alocacaoId: alocacaoParaRemover.id,
      });
      setAlocacaoParaRemover(null);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoIniciar() {
    if (!r) return;
    setErro(null);
    try {
      await iniciar.mutateAsync(r.id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoConcluir() {
    if (!r) return;
    setErro(null);
    try {
      await concluir.mutateAsync(r.id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoCancelar() {
    if (!r) return;
    setErro(null);
    try {
      await cancelar.mutateAsync(r.id);
      setConfirmarCancelamento(false);
      navigate('/app/translados');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  if (detalhe.isLoading) {
    return <div className="text-sm text-gray-500">Carregando translado…</div>;
  }

  if (detalhe.isError || !r) {
    return (
      <div className="space-y-4">
        <button
          type="button"
          onClick={() => navigate('/app/translados')}
          className="flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
        >
          <ArrowLeft className="h-4 w-4" /> Voltar
        </button>
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {detalhe.error ? extrairMensagemDeErro(detalhe.error) : 'Translado não encontrado.'}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => navigate('/app/translados')}
            className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
            aria-label="Voltar"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-semibold text-gray-900">
                Translado · {formatarData(r.data)}
              </h1>
              <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${CORES_STATUS[r.status]}`}>
                {ROTULOS_STATUS[r.status]}
              </span>
            </div>
            <p className="mt-1 text-sm text-gray-600">
              <strong>{r.veiculoPlaca}</strong> · {r.veiculoModelo} · motorista {r.motoristaNome}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {r.status === 'Planejada' ? (
            <>
              <Button variante="outline" onClick={() => navigate(`/app/translados/${r.id}/editar`)}>
                <Pencil className="h-4 w-4" /> Editar
              </Button>
              <Button onClick={aoIniciar} disabled={iniciar.isPending}>
                <Play className="h-4 w-4" /> Iniciar
              </Button>
              <Button variante="danger" onClick={() => setConfirmarCancelamento(true)}>
                <XCircle className="h-4 w-4" /> Cancelar
              </Button>
            </>
          ) : null}
          {r.status === 'EmAndamento' ? (
            <>
              <Button onClick={aoConcluir} disabled={concluir.isPending}>
                <CheckCircle2 className="h-4 w-4" /> Concluir
              </Button>
              <Button variante="danger" onClick={() => setConfirmarCancelamento(true)}>
                <XCircle className="h-4 w-4" /> Cancelar
              </Button>
            </>
          ) : null}
        </div>
      </header>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      {veiculo.isLoading ? (
        <div className="flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Carregando layout do veículo…
        </div>
      ) : veiculo.isError || !veiculo.data ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          Não foi possível carregar o veículo desta rota.
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
          <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-900">
                Mapa de assentos · {r.alocacoes.length}/{veiculo.data.fileiras.reduce((acc, f) => acc + f.assentos.filter((a) => a.tipo !== 'Motorista' && !a.bloqueado).length, 0)} ocupados
              </h2>
              {sessaoSelecionada ? (
                <button
                  type="button"
                  onClick={() => setSessaoSelecionada(null)}
                  className="text-xs text-red-600 hover:underline"
                >
                  Cancelar seleção ({sessaoSelecionada.pacienteNome.split(' ')[0]})
                </button>
              ) : null}
            </div>
            <MapaDeAssentosAlocavel
              veiculo={veiculo.data}
              alocacoes={r.alocacoes}
              sessaoSelecionada={sessaoSelecionada}
              somenteLeitura={!podeEditar}
              aoClicarAssentoVazio={aoClicarAssentoVazio}
              aoClicarAssentoAlocado={(a) => setAlocacaoParaRemover(a)}
            />
          </section>

          {podeEditar ? (
            <aside className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
              <div className="mb-3">
                <h2 className="text-sm font-semibold text-gray-900">Sessões elegíveis</h2>
                <p className="text-xs text-gray-500">
                  Clique numa sessão e depois num assento — ou clique primeiro no assento.
                </p>
              </div>
              <ListaSessoesElegiveis
                sessoes={elegiveis.data ?? []}
                dataRota={r.data}
                sessaoSelecionada={sessaoSelecionada}
                aoSelecionar={setSessaoSelecionada}
                carregando={elegiveis.isLoading}
              />
            </aside>
          ) : null}
        </div>
      )}

      <ModalEscolherSessao
        aberto={Boolean(modalAssento)}
        aoFechar={() => setModalAssento(null)}
        sessoes={elegiveis.data ?? []}
        dataRota={r.data}
        carregando={elegiveis.isLoading}
        tituloAssento={
          modalAssento ? `assento F${modalAssento.fileiraOrdem}·${modalAssento.numero}` : undefined
        }
        aoEscolher={escolherNoModal}
      />

      <ConfirmDialog
        aberto={Boolean(alocacaoParaRemover)}
        titulo="Desalocar paciente"
        mensagem={
          alocacaoParaRemover
            ? `Remover ${alocacaoParaRemover.pacienteNome} do assento F${alocacaoParaRemover.fileiraOrdem}·${alocacaoParaRemover.numeroAssento}? A sessão volta para a fila de elegíveis.`
            : ''
        }
        destrutivo
        rotuloConfirmar="Desalocar"
        carregando={removerAlocacao.isPending}
        aoConfirmar={confirmarRemoverAlocacao}
        aoCancelar={() => setAlocacaoParaRemover(null)}
      />

      <ConfirmDialog
        aberto={confirmarCancelamento}
        titulo="Cancelar translado"
        mensagem={`Cancelar a rota de ${formatarData(r.data)}? As alocações ficam congeladas e as sessões voltam a estar elegíveis para outro translado.`}
        destrutivo
        rotuloConfirmar="Cancelar translado"
        carregando={cancelar.isPending}
        aoConfirmar={aoCancelar}
        aoCancelar={() => setConfirmarCancelamento(false)}
      />

      <div className="flex items-center gap-2 text-xs text-gray-500">
        <Trash2 className="h-3 w-3 hidden" />
        {r.iniciadaEm ? <span>Iniciada em {new Date(r.iniciadaEm).toLocaleString('pt-BR')}</span> : null}
        {r.concluidaEm ? <span>· Concluída em {new Date(r.concluidaEm).toLocaleString('pt-BR')}</span> : null}
      </div>
    </div>
  );
}
