import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { deMinutos, paraMinutos } from '@/shared/lib/tempoMedio';
import { CampoTempoMedio } from '@/shared/ui/CampoTempoMedio';
import {
  useAtualizarTipoTratamento,
  useCadastrarTipoTratamento,
  useTipoTratamentoPorId,
} from '@/features/tiposTratamento/api/queries';

type Props = { modo: 'criar' | 'editar'; id?: string | null; aoConcluir: () => void };

type Valores = { nome: string; codigo: string; ativo: boolean; tempoHoras: string; tempoMinutos: string };

const INICIAL: Valores = { nome: '', codigo: '', ativo: true, tempoHoras: '', tempoMinutos: '' };

type Erros = Partial<Record<'nome' | 'codigo' | 'tempo', string>>;

export function FormularioTipoTratamento({ modo, id, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const cadastrar = useCadastrarTipoTratamento();
  const atualizar = useAtualizarTipoTratamento();
  const detalhe = useTipoTratamentoPorId(modo === 'editar' ? id ?? null : null);

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      const tempo = deMinutos(detalhe.data.tempoMedioMinutos);
      setValores({
        nome: detalhe.data.nome,
        codigo: detalhe.data.codigo,
        ativo: detalhe.data.ativo,
        tempoHoras: tempo.horas,
        tempoMinutos: tempo.minutos,
      });
    }
  }, [modo, detalhe.data]);

  function set<K extends keyof Valores>(k: K, v: Valores[K]) {
    setValores((p) => ({ ...p, [k]: v }));
  }

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

    const ne: Erros = {};
    if (valores.nome.trim().length < 2) ne.nome = 'Nome obrigatório.';
    if (valores.codigo.trim().length < 2) ne.codigo = 'Código obrigatório.';
    const tempoMedio = paraMinutos(valores.tempoHoras, valores.tempoMinutos);
    if (tempoMedio == null) ne.tempo = 'Informe o tempo médio: entre 1 minuto e 24 horas (minutos de 0 a 59).';
    if (Object.keys(ne).length > 0 || tempoMedio == null) {
      setErros(ne);
      return;
    }

    try {
      if (modo === 'criar') {
        await cadastrar.mutateAsync({ nome: valores.nome.trim(), codigo: valores.codigo.trim(), tempoMedioMinutos: tempoMedio });
      } else {
        if (!id) throw new Error('ID ausente.');
        await atualizar.mutateAsync({
          id,
          payload: {
            nome: valores.nome.trim(),
            codigo: valores.codigo.trim(),
            tempoMedioMinutos: tempoMedio,
            ativo: valores.ativo,
          },
        });
      }
      aoConcluir();
    } catch (erro) {
      setErroGlobal(extrairMensagemDeErro(erro));
    }
  }

  const pendente = cadastrar.isPending || atualizar.isPending;

  return (
    <form onSubmit={aoEnviar} className="space-y-5">
      {modo === 'editar' && detalhe.isFetching ? (
        <div className="text-sm text-gray-500">Carregando dados…</div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Campo label="Nome" htmlFor="nome" erro={erros.nome} required className="md:col-span-2">
          <Input id="nome" value={valores.nome} onChange={(e) => set('nome', e.target.value)} required />
        </Campo>

        <Campo
          label="Código"
          htmlFor="codigo"
          erro={erros.codigo}
          required
          dica="Identificador curto (ex.: 'hemodialise'). Único."
        >
          <Input id="codigo" value={valores.codigo} onChange={(e) => set('codigo', e.target.value)} required />
        </Campo>

        <CampoTempoMedio
          horas={valores.tempoHoras}
          minutos={valores.tempoMinutos}
          aoMudar={(v) => setValores((p) => ({ ...p, tempoHoras: v.horas, tempoMinutos: v.minutos }))}
          erro={erros.tempo}
          className="md:col-span-2"
        />

        {modo === 'editar' ? (
          <Campo label="Situação" htmlFor="ativo">
            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input
                id="ativo"
                type="checkbox"
                checked={valores.ativo}
                onChange={(e) => set('ativo', e.target.checked)}
              />
              <span>Ativo</span>
            </label>
          </Campo>
        ) : null}
      </div>

      {erroGlobal ? (
        <div role="alert" className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
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
