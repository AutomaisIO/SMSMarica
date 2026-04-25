import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  enderecoVazio,
  FormularioEndereco,
  paraPayload,
  type EnderecoForm,
} from '@/shared/ui/FormularioEndereco';
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
  telefone: string;
  endereco: EnderecoForm;
  latitude: string;
  longitude: string;
};

const INICIAL: Valores = {
  nome: '',
  telefone: '',
  endereco: enderecoVazio,
  latitude: '',
  longitude: '',
};

type Erros = Partial<Record<'nome' | 'telefone' | 'latitude' | 'longitude', string>>;

export function FormularioUnidade({ modo, idUnidade, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const cadastrar = useCadastrarUnidade();
  const atualizar = useAtualizarUnidade();
  const detalhe = useUnidadePorId(modo === 'editar' ? idUnidade ?? null : null);

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      const e = detalhe.data.endereco;
      setValores({
        nome: detalhe.data.nome,
        telefone: detalhe.data.telefone ?? '',
        latitude: detalhe.data.latitude == null ? '' : String(detalhe.data.latitude),
        longitude: detalhe.data.longitude == null ? '' : String(detalhe.data.longitude),
        endereco: e
          ? {
              cep: e.cep ?? '',
              logradouro: e.logradouro ?? '',
              numero: e.numero ?? '',
              complemento: e.complemento ?? '',
              bairro: e.bairro ?? '',
              cidade: e.cidade ?? '',
              uf: e.uf ?? '',
              pontoReferencia: e.pontoReferencia ?? '',
            }
          : enderecoVazio,
      });
    }
  }, [modo, detalhe.data]);

  function set<K extends keyof Valores>(k: K, v: Valores[K]) {
    setValores((prev) => ({ ...prev, [k]: v }));
  }

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

    const enderecoForm = paraPayload(valores.endereco);
    const lat = valores.latitude.trim() === '' ? null : Number(valores.latitude);
    const lng = valores.longitude.trim() === '' ? null : Number(valores.longitude);

    const parsed = unidadeSchema.safeParse({
      nome: valores.nome.trim(),
      telefone: valores.telefone,
      endereco: enderecoForm
        ? {
            cep: enderecoForm.cep,
            logradouro: enderecoForm.logradouro,
            numero: enderecoForm.numero || null,
            complemento: enderecoForm.complemento || null,
            bairro: enderecoForm.bairro,
            cidade: enderecoForm.cidade,
            uf: enderecoForm.uf,
            pontoReferencia: enderecoForm.pontoReferencia || null,
          }
        : null,
      latitude: lat,
      longitude: lng,
    });
    if (!parsed.success) {
      const ne: Erros = {};
      for (const i of parsed.error.issues) {
        const k = i.path[0] as keyof Erros | undefined;
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
    <form onSubmit={aoEnviar} className="space-y-5">
      {modo === 'editar' && detalhe.isFetching ? (
        <div className="text-sm text-gray-500">Carregando dados…</div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Campo label="Nome" htmlFor="nome" erro={erros.nome} required className="md:col-span-2">
          <Input id="nome" value={valores.nome} onChange={(e) => set('nome', e.target.value)} required />
        </Campo>

        <Campo label="Telefone" htmlFor="telefone" erro={erros.telefone}>
          <Input
            id="telefone"
            value={valores.telefone}
            onChange={(e) => set('telefone', e.target.value)}
            placeholder="(21) 99999-0000"
          />
        </Campo>
        <div />
      </div>

      <section>
        <h3 className="mb-3 text-sm font-semibold text-gray-900">Endereço</h3>
        <FormularioEndereco
          valor={valores.endereco}
          aoMudar={(e) => set('endereco', e)}
          desabilitado={pendente}
        />
      </section>

      <section>
        <h3 className="mb-3 text-sm font-semibold text-gray-900">Coordenadas (opcional)</h3>
        <p className="mb-2 text-xs text-gray-500">
          Preencha apenas se a unidade tiver geolocalização exata para o operacional. Pode ficar em branco.
        </p>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <Campo label="Latitude" htmlFor="lat" erro={erros.latitude}>
            <Input
              id="lat"
              value={valores.latitude}
              onChange={(e) => set('latitude', e.target.value)}
              inputMode="decimal"
              placeholder="-22.9176"
            />
          </Campo>
          <Campo label="Longitude" htmlFor="lng" erro={erros.longitude}>
            <Input
              id="lng"
              value={valores.longitude}
              onChange={(e) => set('longitude', e.target.value)}
              inputMode="decimal"
              placeholder="-42.8186"
            />
          </Campo>
        </div>
      </section>

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
