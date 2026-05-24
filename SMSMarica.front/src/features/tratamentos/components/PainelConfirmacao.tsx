import { useState, type FormEvent } from 'react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useListarMotoristas } from '@/features/motoristas/api/queries';
import { useConfirmarSessao } from '@/features/tratamentos/api/queries';
import { useQuery } from '@tanstack/react-query';
import { listarVeiculos } from '@/features/veiculos/api/veiculosApi';
import type { Sessao } from '@/features/tratamentos/types';

type Props = {
  tratamentoId: string;
  sessao: Sessao;
  aoConcluir: () => void;
};

export function PainelConfirmacao({ tratamentoId, sessao, aoConcluir }: Props) {
  const motoristas = useListarMotoristas();
  const veiculos = useQuery({ queryKey: ['veiculos', 'lista'], queryFn: listarVeiculos });
  const confirmar = useConfirmarSessao();
  const [erro, setErro] = useState<string | null>(null);
  const [realizada, setRealizada] = useState<boolean>(sessao.status !== 'NaoRealizada' && sessao.status !== 5);
  const [f, setF] = useState({
    nomeAcompanhante: sessao.nomeAcompanhante ?? '',
    parentescoAcompanhante: sessao.parentescoAcompanhante ?? '',
    motoristaIdaId: sessao.motoristaIdaId ?? '',
    veiculoIdaId: sessao.veiculoIdaId ?? '',
    horaSaidaResidencia: sessao.horaSaidaResidencia ?? '',
    horaChegadaUnidade: sessao.horaChegadaUnidade ?? '',
    motoristaVoltaId: sessao.motoristaVoltaId ?? '',
    veiculoVoltaId: sessao.veiculoVoltaId ?? '',
    horaSaidaUnidade: sessao.horaSaidaUnidade ?? '',
    horaChegadaResidencia: sessao.horaChegadaResidencia ?? '',
    motivoNaoRealizacao: sessao.motivoNaoRealizacao ?? '',
    observacoes: sessao.observacoes ?? '',
  });

  const motoristasAtivos = (motoristas.data ?? []).filter((m) => m.usuarioAtivo);
  const veiculosAtivos = (veiculos.data ?? []).filter((v) => v.ativo);

  async function salvar(e: FormEvent) {
    e.preventDefault();
    setErro(null);
    try {
      await confirmar.mutateAsync({
        id: tratamentoId,
        sessaoId: sessao.id,
        payload: {
          realizada,
          nomeAcompanhante: f.nomeAcompanhante || null,
          parentescoAcompanhante: f.parentescoAcompanhante || null,
          motoristaIdaId: f.motoristaIdaId || null,
          veiculoIdaId: f.veiculoIdaId || null,
          horaSaidaResidencia: f.horaSaidaResidencia || null,
          horaChegadaUnidade: f.horaChegadaUnidade || null,
          motoristaVoltaId: f.motoristaVoltaId || null,
          veiculoVoltaId: f.veiculoVoltaId || null,
          horaSaidaUnidade: f.horaSaidaUnidade || null,
          horaChegadaResidencia: f.horaChegadaResidencia || null,
          motivoNaoRealizacao: realizada ? null : f.motivoNaoRealizacao,
          observacoes: f.observacoes || null,
        },
      });
      aoConcluir();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <form onSubmit={salvar} className="space-y-4">
      <div className="rounded-md bg-gray-50 p-3">
        <div className="flex items-center gap-4">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              checked={realizada}
              onChange={() => setRealizada(true)}
            />
            Realizada
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              checked={!realizada}
              onChange={() => setRealizada(false)}
            />
            Não realizada
          </label>
        </div>
      </div>

      {!realizada ? (
        <Campo label="Motivo da não realização" htmlFor="motivo"
          dica="Obrigatório quando a sessão não aconteceu.">
          <textarea
            id="motivo"
            className="input min-h-[80px]"
            value={f.motivoNaoRealizacao}
            onChange={(e) => setF((s) => ({ ...s, motivoNaoRealizacao: e.target.value }))}
          />
        </Campo>
      ) : null}

      {realizada ? (
        <>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Campo label="Nome do acompanhante" htmlFor="acompNome">
              <Input
                id="acompNome"
                value={f.nomeAcompanhante}
                onChange={(e) => setF((s) => ({ ...s, nomeAcompanhante: e.target.value }))}
              />
            </Campo>
            <Campo label="Parentesco" htmlFor="acompParente">
              <Input
                id="acompParente"
                value={f.parentescoAcompanhante}
                onChange={(e) => setF((s) => ({ ...s, parentescoAcompanhante: e.target.value }))}
                placeholder="Mãe, Filho, Cônjuge…"
              />
            </Campo>
          </div>

          <h3 className="mt-4 text-sm font-semibold text-gray-900">Ida</h3>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Campo label="Motorista da ida" htmlFor="motIda">
              <Select id="motIda" value={f.motoristaIdaId} onChange={(e) => setF((s) => ({ ...s, motoristaIdaId: e.target.value }))}>
                <option value="">—</option>
                {motoristasAtivos.map((m) => <option key={m.id} value={m.id}>{m.nomeCompleto}</option>)}
              </Select>
            </Campo>
            <Campo label="Veículo da ida" htmlFor="veicIda">
              <Select id="veicIda" value={f.veiculoIdaId} onChange={(e) => setF((s) => ({ ...s, veiculoIdaId: e.target.value }))}>
                <option value="">—</option>
                {veiculosAtivos.map((v) => <option key={v.id} value={v.id}>{v.placa} — {v.modelo}</option>)}
              </Select>
            </Campo>
            <Campo label="Hora saída residência" htmlFor="hsr">
              <Input id="hsr" type="time" value={f.horaSaidaResidencia} onChange={(e) => setF((s) => ({ ...s, horaSaidaResidencia: e.target.value }))} />
            </Campo>
            <Campo label="Hora chegada unidade" htmlFor="hcu">
              <Input id="hcu" type="time" value={f.horaChegadaUnidade} onChange={(e) => setF((s) => ({ ...s, horaChegadaUnidade: e.target.value }))} />
            </Campo>
          </div>

          <h3 className="mt-4 text-sm font-semibold text-gray-900">Volta</h3>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Campo label="Motorista da volta" htmlFor="motVolta">
              <Select id="motVolta" value={f.motoristaVoltaId} onChange={(e) => setF((s) => ({ ...s, motoristaVoltaId: e.target.value }))}>
                <option value="">—</option>
                {motoristasAtivos.map((m) => <option key={m.id} value={m.id}>{m.nomeCompleto}</option>)}
              </Select>
            </Campo>
            <Campo label="Veículo da volta" htmlFor="veicVolta">
              <Select id="veicVolta" value={f.veiculoVoltaId} onChange={(e) => setF((s) => ({ ...s, veiculoVoltaId: e.target.value }))}>
                <option value="">—</option>
                {veiculosAtivos.map((v) => <option key={v.id} value={v.id}>{v.placa} — {v.modelo}</option>)}
              </Select>
            </Campo>
            <Campo label="Hora saída unidade" htmlFor="hsu">
              <Input id="hsu" type="time" value={f.horaSaidaUnidade} onChange={(e) => setF((s) => ({ ...s, horaSaidaUnidade: e.target.value }))} />
            </Campo>
            <Campo label="Hora chegada residência" htmlFor="hcr">
              <Input id="hcr" type="time" value={f.horaChegadaResidencia} onChange={(e) => setF((s) => ({ ...s, horaChegadaResidencia: e.target.value }))} />
            </Campo>
          </div>
        </>
      ) : null}

      <Campo label="Observações" htmlFor="obsConf">
        <textarea
          id="obsConf"
          className="input min-h-[60px]"
          value={f.observacoes}
          onChange={(e) => setF((s) => ({ ...s, observacoes: e.target.value }))}
        />
      </Campo>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      <div className="flex justify-end gap-2">
        <Button type="button" variante="ghost" onClick={aoConcluir}>Cancelar</Button>
        <Button type="submit" disabled={confirmar.isPending}>
          {confirmar.isPending ? 'Salvando…' : 'Salvar confirmação'}
        </Button>
      </div>
    </form>
  );
}
