import { useEffect, useState, type FormEvent } from 'react';
import { Minus, Plus } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAtualizarVeiculo,
  useCadastrarVeiculo,
  useVeiculoPorId,
} from '@/features/veiculos/api/queries';
import {
  atualizarVeiculoSchema,
  cadastrarVeiculoSchema,
} from '@/features/veiculos/schemas/veiculoSchema';
import {
  ROTULOS_TIPO_VEICULO,
  TIPOS_ASSENTO,
  TIPOS_VEICULO,
  type TipoAssento,
  type TipoVeiculo,
} from '@/features/veiculos/types';
import {
  MapaDeAssentos,
  type LinhaLayout,
} from '@/features/veiculos/components/MapaDeAssentos';

type Props = {
  modo: 'criar' | 'editar';
  idVeiculo?: string | null;
  aoConcluir: () => void;
};

type ValoresBase = {
  placa: string;
  modelo: string;
  fabricante: string;
  cor: string;
  tipo: TipoVeiculo;
};

const INICIAL: ValoresBase = {
  placa: '',
  modelo: '',
  fabricante: '',
  cor: '',
  tipo: TIPOS_VEICULO.Van,
};

type Erros = Partial<Record<keyof ValoresBase, string>> & { fileiras?: string };

const TIPO_ROTATIVO: TipoAssento[] = [
  TIPOS_ASSENTO.Passageiro,
  TIPOS_ASSENTO.Motorista,
  TIPOS_ASSENTO.Acompanhante,
];

function layoutInicial(): LinhaLayout[] {
  return [
    { ordem: 1, assentos: [
      { numero: 1, tipo: TIPOS_ASSENTO.Motorista },
      { numero: 2, tipo: TIPOS_ASSENTO.Passageiro },
    ]},
    { ordem: 2, assentos: Array.from({ length: 3 }, (_, i) => ({
      numero: i + 1,
      tipo: TIPOS_ASSENTO.Passageiro,
    }))},
    { ordem: 3, assentos: Array.from({ length: 3 }, (_, i) => ({
      numero: i + 1,
      tipo: TIPOS_ASSENTO.Passageiro,
    }))},
  ];
}

export function FormularioVeiculo({ modo, idVeiculo, aoConcluir }: Props) {
  const [valores, setValores] = useState<ValoresBase>(INICIAL);
  const [linhas, setLinhas] = useState<LinhaLayout[]>(layoutInicial);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const cadastrar = useCadastrarVeiculo();
  const atualizar = useAtualizarVeiculo();
  const detalhe = useVeiculoPorId(modo === 'editar' ? idVeiculo ?? null : null);

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      const v = detalhe.data;
      setValores({
        placa: v.placa,
        modelo: v.modelo,
        fabricante: v.fabricante,
        cor: v.cor,
        tipo: v.tipo,
      });
      setLinhas(
        v.fileiras
          .slice()
          .sort((a, b) => a.ordem - b.ordem)
          .map((f) => ({
            ordem: f.ordem,
            assentos: f.assentos
              .slice()
              .sort((a, b) => a.numero - b.numero)
              .map((a) => ({ numero: a.numero, tipo: a.tipo })),
          })),
      );
    }
  }, [modo, detalhe.data]);

  function set<K extends keyof ValoresBase>(k: K, v: ValoresBase[K]) {
    setValores((prev) => ({ ...prev, [k]: v }));
  }

  function adicionarFileira() {
    setLinhas((prev) => {
      const proxima = prev.length ? Math.max(...prev.map((l) => l.ordem)) + 1 : 1;
      const qtdRef = prev[prev.length - 1]?.assentos.length ?? 3;
      return [
        ...prev,
        {
          ordem: proxima,
          assentos: Array.from({ length: qtdRef }, (_, i) => ({
            numero: i + 1,
            tipo: TIPOS_ASSENTO.Passageiro,
          })),
        },
      ];
    });
  }

  function removerFileira(ordem: number) {
    setLinhas((prev) => prev.filter((l) => l.ordem !== ordem));
  }

  function ajustarAssentos(ordem: number, delta: number) {
    setLinhas((prev) =>
      prev.map((l) => {
        if (l.ordem !== ordem) return l;
        const alvo = Math.min(10, Math.max(1, l.assentos.length + delta));
        if (alvo === l.assentos.length) return l;
        if (alvo > l.assentos.length) {
          const novos = Array.from({ length: alvo - l.assentos.length }, (_, i) => ({
            numero: l.assentos.length + i + 1,
            tipo: TIPOS_ASSENTO.Passageiro as TipoAssento,
          }));
          return { ...l, assentos: [...l.assentos, ...novos] };
        }
        return { ...l, assentos: l.assentos.slice(0, alvo) };
      }),
    );
  }

  function rotacionarTipoAssento(ordem: number, numero: number) {
    setLinhas((prev) =>
      prev.map((l) => {
        if (l.ordem !== ordem) return l;
        return {
          ...l,
          assentos: l.assentos.map((a) => {
            if (a.numero !== numero) return a;
            const idx = TIPO_ROTATIVO.indexOf(a.tipo);
            const prox = TIPO_ROTATIVO[(idx + 1) % TIPO_ROTATIVO.length];
            return { ...a, tipo: prox };
          }),
        };
      }),
    );
  }

  const totalAssentos = linhas.reduce((acc, l) => acc + l.assentos.length, 0);

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

    if (modo === 'criar') {
      const parsed = cadastrarVeiculoSchema.safeParse({
        ...valores,
        placa: valores.placa.trim(),
        modelo: valores.modelo.trim(),
        fabricante: valores.fabricante.trim(),
        cor: valores.cor.trim(),
        fileiras: linhas.map((l) => ({
          ordem: l.ordem,
          assentos: l.assentos.map((a) => ({ numero: a.numero, tipo: a.tipo })),
        })),
      });
      if (!parsed.success) {
        const ne: Erros = {};
        for (const i of parsed.error.issues) {
          const k = i.path[0] as keyof ValoresBase | 'fileiras' | undefined;
          if (k && !ne[k]) ne[k] = i.message;
        }
        setErros(ne);
        return;
      }
      try {
        await cadastrar.mutateAsync(parsed.data);
        aoConcluir();
      } catch (erro) {
        setErroGlobal(extrairMensagemDeErro(erro));
      }
      return;
    }

    const parsed = atualizarVeiculoSchema.safeParse({
      ...valores,
      placa: valores.placa.trim(),
      modelo: valores.modelo.trim(),
      fabricante: valores.fabricante.trim(),
      cor: valores.cor.trim(),
    });
    if (!parsed.success) {
      const ne: Erros = {};
      for (const i of parsed.error.issues) {
        const k = i.path[0] as keyof ValoresBase | undefined;
        if (k && !ne[k]) ne[k] = i.message;
      }
      setErros(ne);
      return;
    }
    try {
      if (!idVeiculo) throw new Error('ID ausente.');
      await atualizar.mutateAsync({ id: idVeiculo, payload: parsed.data });
      aoConcluir();
    } catch (erro) {
      setErroGlobal(extrairMensagemDeErro(erro));
    }
  }

  const pendente = cadastrar.isPending || atualizar.isPending;

  return (
    <form onSubmit={aoEnviar} className="space-y-6">
      {modo === 'editar' && detalhe.isFetching ? (
        <div className="text-sm text-gray-500">Carregando dados…</div>
      ) : null}

      <section className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Campo label="Placa" htmlFor="placa" erro={erros.placa}>
          <Input
            id="placa"
            value={valores.placa}
            onChange={(e) => set('placa', e.target.value.toUpperCase())}
            maxLength={10}
            required
            placeholder="ABC1D23"
          />
        </Campo>

        <Campo label="Tipo" htmlFor="tipo" erro={erros.tipo}>
          <Select
            id="tipo"
            value={valores.tipo}
            onChange={(e) => set('tipo', Number(e.target.value) as TipoVeiculo)}
          >
            {Object.values(TIPOS_VEICULO).map((t) => (
              <option key={t} value={t}>
                {ROTULOS_TIPO_VEICULO[t]}
              </option>
            ))}
          </Select>
        </Campo>

        <Campo label="Fabricante" htmlFor="fabricante" erro={erros.fabricante}>
          <Input
            id="fabricante"
            value={valores.fabricante}
            onChange={(e) => set('fabricante', e.target.value)}
            maxLength={80}
            required
            placeholder="Mercedes-Benz, Renault…"
          />
        </Campo>

        <Campo label="Modelo" htmlFor="modelo" erro={erros.modelo}>
          <Input
            id="modelo"
            value={valores.modelo}
            onChange={(e) => set('modelo', e.target.value)}
            maxLength={100}
            required
            placeholder="Sprinter, Master…"
          />
        </Campo>

        <Campo label="Cor" htmlFor="cor" erro={erros.cor}>
          <Input
            id="cor"
            value={valores.cor}
            onChange={(e) => set('cor', e.target.value)}
            maxLength={40}
            required
            placeholder="Branco, Prata…"
          />
        </Campo>
      </section>

      {modo === 'criar' ? (
        <section className="space-y-4">
          <div className="flex items-end justify-between gap-3 border-b border-gray-200 pb-2">
            <div>
              <h3 className="text-sm font-semibold text-gray-900">Layout dos assentos</h3>
              <p className="text-xs text-gray-600">
                Adicione fileiras e ajuste quantos assentos cada uma tem. Clique num assento
                para alternar entre Passageiro → Motorista → Acompanhante.
              </p>
            </div>
            <div className="text-xs text-gray-500">
              {linhas.length} fileira(s) · {totalAssentos} assento(s)
            </div>
          </div>

          {erros.fileiras ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
              {erros.fileiras}
            </div>
          ) : null}

          <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
            <div className="space-y-2">
              {linhas.map((l) => (
                <div
                  key={l.ordem}
                  className="flex items-center justify-between rounded-md border border-gray-200 bg-white px-3 py-2"
                >
                  <div className="flex items-center gap-2 text-sm">
                    <span className="inline-flex h-6 w-8 items-center justify-center rounded bg-gray-100 text-xs font-semibold text-gray-700">
                      F{l.ordem}
                    </span>
                    <span className="text-gray-700">
                      {l.assentos.length} assento(s)
                    </span>
                  </div>
                  <div className="flex items-center gap-1">
                    <Button
                      type="button"
                      tamanho="sm"
                      variante="outline"
                      onClick={() => ajustarAssentos(l.ordem, -1)}
                      disabled={l.assentos.length <= 1}
                      aria-label={`Remover assento da fileira ${l.ordem}`}
                    >
                      <Minus className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      type="button"
                      tamanho="sm"
                      variante="outline"
                      onClick={() => ajustarAssentos(l.ordem, +1)}
                      disabled={l.assentos.length >= 10}
                      aria-label={`Adicionar assento à fileira ${l.ordem}`}
                    >
                      <Plus className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      type="button"
                      tamanho="sm"
                      variante="ghost"
                      onClick={() => removerFileira(l.ordem)}
                      disabled={linhas.length <= 1}
                      className="text-red-600 hover:bg-red-50"
                    >
                      Excluir
                    </Button>
                  </div>
                </div>
              ))}
              <Button
                type="button"
                variante="outline"
                onClick={adicionarFileira}
                disabled={linhas.length >= 30}
                className="w-full"
              >
                <Plus className="h-4 w-4" /> Adicionar fileira
              </Button>
            </div>

            <div className="rounded-lg border border-gray-200 bg-gray-50 p-4">
              <MapaDeAssentos
                linhas={linhas}
                onClickAssento={({ fileiraOrdem, numero }) =>
                  rotacionarTipoAssento(fileiraOrdem, numero)
                }
              />
            </div>
          </div>
        </section>
      ) : (
        <section className="rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-xs text-gray-600">
          Edição de layout (fileiras/assentos) não está disponível neste formulário.
          Para alterar o layout, exclua e recadastre o veículo — histórico não é afetado.
        </section>
      )}

      {erroGlobal ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroGlobal}
        </div>
      ) : null}

      <div className="flex items-center justify-end gap-3 pt-2">
        <Button type="button" variante="ghost" onClick={aoConcluir} disabled={pendente}>
          Cancelar
        </Button>
        <Button type="submit" disabled={pendente}>
          {pendente ? 'Salvando…' : modo === 'criar' ? 'Cadastrar' : 'Salvar alterações'}
        </Button>
      </div>
    </form>
  );
}
