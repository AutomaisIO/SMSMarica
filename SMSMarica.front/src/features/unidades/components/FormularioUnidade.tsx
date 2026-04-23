import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAtualizarUnidade,
  useCadastrarUnidade,
  useUnidadePorId,
} from '@/features/unidades/api/queries';
import { unidadeSchema } from '@/features/unidades/schemas/unidadeSchema';

type Props = {
  modo: 'criar' | 'editar';
  idUnidade?: string | null;
  aoConcluir: () => void;
};

type Valores = {
  nome: string;
  endereco: string;
  telefone: string;
  latitude: string;
  longitude: string;
};

const INICIAL: Valores = { nome: '', endereco: '', telefone: '', latitude: '', longitude: '' };
type Erros = Partial<Record<keyof Valores, string>>;

export function FormularioUnidade({ modo, idUnidade, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const cadastrar = useCadastrarUnidade();
  const atualizar = useAtualizarUnidade();
  const detalhe = useUnidadePorId(modo === 'editar' ? idUnidade ?? null : null);

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      setValores({
        nome: detalhe.data.nome,
        endereco: detalhe.data.endereco,
        telefone: detalhe.data.telefone ?? '',
        latitude: String(detalhe.data.latitude),
        longitude: String(detalhe.data.longitude),
      });
    }
  }, [modo, detalhe.data]);

  function set<K extends keyof Valores>(k: K, v: string) {
    setValores((prev) => ({ ...prev, [k]: v }));
  }

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

    const parsed = unidadeSchema.safeParse({
      nome: valores.nome.trim(),
      endereco: valores.endereco.trim(),
      telefone: valores.telefone,
      latitude: Number(valores.latitude),
      longitude: Number(valores.longitude),
    });
    if (!parsed.success) {
      const ne: Erros = {};
      for (const i of parsed.error.issues) {
        const k = i.path[0] as keyof Valores | undefined;
        if (k && !ne[k]) ne[k] = i.message;
      }
      setErros(ne);
      return;
    }

    try {
      if (modo === 'criar') {
        await cadastrar.mutateAsync(parsed.data);
      } else {
        if (!idUnidade) throw new Error('ID ausente.');
        await atualizar.mutateAsync({ id: idUnidade, payload: parsed.data });
      }
      aoConcluir();
    } catch (erro) {
      setErroGlobal(extrairMensagemDeErro(erro));
    }
  }

  const pendente = cadastrar.isPending || atualizar.isPending;

  return (
    <form onSubmit={aoEnviar} className="space-y-4">
      {modo === 'editar' && detalhe.isFetching ? (
        <div className="text-sm text-gray-500">Carregando dados…</div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Campo label="Nome" htmlFor="nome" erro={erros.nome} className="md:col-span-2">
          <Input id="nome" value={valores.nome} onChange={(e) => set('nome', e.target.value)} required />
        </Campo>

        <Campo
          label="Endereço"
          htmlFor="endereco"
          erro={erros.endereco}
          className="md:col-span-2"
        >
          <Input
            id="endereco"
            value={valores.endereco}
            onChange={(e) => set('endereco', e.target.value)}
            required
          />
        </Campo>

        <Campo label="Telefone (opcional)" htmlFor="telefone" erro={erros.telefone}>
          <Input
            id="telefone"
            value={valores.telefone}
            onChange={(e) => set('telefone', e.target.value)}
            placeholder="(21) 99999-0000"
          />
        </Campo>

        <div />

        <Campo label="Latitude" htmlFor="lat" erro={erros.latitude}>
          <Input
            id="lat"
            value={valores.latitude}
            onChange={(e) => set('latitude', e.target.value)}
            inputMode="decimal"
            required
          />
        </Campo>

        <Campo label="Longitude" htmlFor="lng" erro={erros.longitude}>
          <Input
            id="lng"
            value={valores.longitude}
            onChange={(e) => set('longitude', e.target.value)}
            inputMode="decimal"
            required
          />
        </Campo>
      </div>

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
