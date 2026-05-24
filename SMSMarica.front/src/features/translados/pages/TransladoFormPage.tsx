import { useEffect, useMemo, useState } from 'react';
import { ArrowLeft } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useListarMotoristas } from '@/features/motoristas/api/queries';
import {
  useAtualizarRota,
  useCadastrarRota,
  useRotaPorId,
} from '@/features/translados/api/queries';
import { useListarVeiculos } from '@/features/veiculos/api/queries';

function hojeIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function TransladoFormPage() {
  const navigate = useNavigate();
  const params = useParams<{ id: string }>();
  const editando = Boolean(params.id);
  const rotaId = params.id ?? '';

  const veiculos = useListarVeiculos();
  const motoristas = useListarMotoristas();
  const detalhe = useRotaPorId(editando ? rotaId : null);
  const cadastrar = useCadastrarRota();
  const atualizar = useAtualizarRota();

  const [data, setData] = useState<string>(hojeIso());
  const [veiculoId, setVeiculoId] = useState<string>('');
  const [motoristaId, setMotoristaId] = useState<string>('');
  const [erro, setErro] = useState<string | null>(null);
  const [confirmarTrocaVeiculo, setConfirmarTrocaVeiculo] = useState(false);

  useEffect(() => {
    if (detalhe.data) {
      setData(detalhe.data.data);
      setVeiculoId(detalhe.data.veiculoId);
      setMotoristaId(detalhe.data.motoristaId);
    }
  }, [detalhe.data]);

  const veiculosAtivos = useMemo(
    () => (veiculos.data ?? []).filter((v) => v.ativo),
    [veiculos.data],
  );
  const motoristasAtivos = useMemo(
    () => (motoristas.data ?? []).filter((m) => m.usuarioAtivo),
    [motoristas.data],
  );

  const camposOk = Boolean(data && veiculoId && motoristaId);
  const pendente = cadastrar.isPending || atualizar.isPending;

  const trocandoVeiculoComAlocacoes =
    editando &&
    Boolean(detalhe.data) &&
    detalhe.data!.veiculoId !== veiculoId &&
    (detalhe.data!.alocacoes?.length ?? 0) > 0;

  const statusPlanejada = !editando || detalhe.data?.status === 'Planejada';

  async function salvar() {
    setErro(null);
    if (!camposOk) return;
    try {
      if (editando) {
        await atualizar.mutateAsync({
          id: rotaId,
          payload: { data, veiculoId, motoristaId },
        });
        navigate(`/app/translados/${rotaId}`);
      } else {
        const novoId = await cadastrar.mutateAsync({ data, veiculoId, motoristaId });
        navigate(`/app/translados/${novoId}`);
      }
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  function aoTentarSalvar() {
    if (trocandoVeiculoComAlocacoes) {
      setConfirmarTrocaVeiculo(true);
      return;
    }
    salvar();
  }

  return (
    <div className="space-y-6">
      <header className="flex items-center gap-3">
        <button
          type="button"
          onClick={() => navigate(editando ? `/app/translados/${rotaId}` : '/app/translados')}
          className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
          aria-label="Voltar"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">
            {editando ? 'Editar translado' : 'Novo translado'}
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            Defina data, veículo e motorista. A alocação de pacientes é feita após salvar.
          </p>
        </div>
      </header>

      {editando && !statusPlanejada ? (
        <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          Só é possível editar rota no estado Planejada. Status atual: {detalhe.data?.status}.
        </div>
      ) : null}

      <div className="max-w-xl space-y-4 rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <Campo label="Data" htmlFor="data" required>
          <Input
            id="data"
            type="date"
            value={data}
            onChange={(e) => setData(e.target.value)}
            disabled={pendente || !statusPlanejada}
          />
        </Campo>

        <Campo label="Veículo" htmlFor="veiculoId" required>
          <Select
            id="veiculoId"
            value={veiculoId}
            onChange={(e) => setVeiculoId(e.target.value)}
            disabled={pendente || !statusPlanejada || veiculos.isLoading}
          >
            <option value="">Selecione…</option>
            {veiculosAtivos.map((v) => (
              <option key={v.id} value={v.id}>
                {v.placa} · {v.modelo} ({v.fabricante})
              </option>
            ))}
          </Select>
        </Campo>

        <Campo label="Motorista" htmlFor="motoristaId" required>
          <Select
            id="motoristaId"
            value={motoristaId}
            onChange={(e) => setMotoristaId(e.target.value)}
            disabled={pendente || !statusPlanejada || motoristas.isLoading}
          >
            <option value="">Selecione…</option>
            {motoristasAtivos.map((m) => (
              <option key={m.id} value={m.id}>
                {m.nomeCompleto} · CPF {m.cpf}
              </option>
            ))}
          </Select>
        </Campo>

        {trocandoVeiculoComAlocacoes ? (
          <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
            Alterar o veículo desta rota vai <strong>remover as {detalhe.data!.alocacoes.length} alocações</strong>{' '}
            existentes. Os assentos do novo veículo são diferentes, então os pacientes precisarão ser alocados novamente.
          </div>
        ) : null}

        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </div>
        ) : null}

        <div className="flex items-center justify-end gap-2 pt-2">
          <Button
            variante="ghost"
            onClick={() => navigate(editando ? `/app/translados/${rotaId}` : '/app/translados')}
            disabled={pendente}
          >
            Cancelar
          </Button>
          <Button onClick={aoTentarSalvar} disabled={!camposOk || pendente || !statusPlanejada}>
            {pendente ? 'Salvando…' : editando ? 'Salvar alterações' : 'Criar translado'}
          </Button>
        </div>
      </div>

      <ConfirmDialog
        aberto={confirmarTrocaVeiculo}
        titulo="Trocar veículo do translado"
        mensagem={
          trocandoVeiculoComAlocacoes
            ? `O veículo atual tem ${detalhe.data!.alocacoes.length} alocação(ões). Ao trocar o veículo, todas serão removidas e os pacientes precisarão ser realocados no novo veículo. Deseja continuar?`
            : ''
        }
        destrutivo
        rotuloConfirmar="Trocar veículo e salvar"
        carregando={atualizar.isPending}
        aoConfirmar={async () => {
          setConfirmarTrocaVeiculo(false);
          await salvar();
        }}
        aoCancelar={() => setConfirmarTrocaVeiculo(false)}
      />
    </div>
  );
}
