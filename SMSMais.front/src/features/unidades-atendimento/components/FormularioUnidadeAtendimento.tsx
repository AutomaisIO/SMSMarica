import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { enderecoVazio, FormularioEndereco, type EnderecoForm } from '@/shared/ui/FormularioEndereco';
import { MapaSeletor } from '@/shared/ui/MapaSeletor';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAtualizarUnidadeAtendimento,
  useCadastrarUnidadeAtendimento,
  useUnidadeAtendimento,
} from '@/features/unidades-atendimento/api/queries';
import type { SalvarUnidadeAtendimentoPayload } from '@/features/unidades-atendimento/types';

type Props = {
  modo: 'criar' | 'editar';
  idUnidade?: string | null;
  /** Recebe o id salvo; `null` quando o usuário cancelou. */
  aoConcluir: (id: string | null) => void;
};

type Valores = {
  nome: string;
  telefone: string;
  observacoes: string;
  externa: boolean;
  endereco: EnderecoForm;
  latitude: string;
  longitude: string;
};

// O transporte é TFD: o destino típico fica fora do município — nasce marcado, dá para desmarcar.
const INICIAL: Valores = {
  nome: '',
  telefone: '',
  observacoes: '',
  externa: true,
  endereco: enderecoVazio,
  latitude: '',
  longitude: '',
};

type Erros = Record<string, string | undefined>;

/** Mesmas regras do validador do backend — aqui só para apontar o campo antes de enviar. */
function validar(v: Valores): Erros {
  const e: Erros = {};
  if (v.nome.trim().length < 2) e.nome = 'Informe o nome da unidade.';
  const cep = v.endereco.cep.replace(/\D/g, '');
  if (cep.length > 0 && cep.length !== 8) e.cep = 'CEP deve ter 8 dígitos.';
  if (!v.endereco.logradouro.trim()) e.logradouro = 'Informe o logradouro.';
  if (!v.endereco.bairro.trim()) e.bairro = 'Informe o bairro.';
  if (!v.endereco.cidade.trim()) e.cidade = 'Informe a cidade.';
  if (v.endereco.uf.trim().length !== 2) e.uf = 'UF com 2 letras.';

  const lat = Number(v.latitude);
  const lng = Number(v.longitude);
  if (v.latitude.trim() === '' || v.longitude.trim() === '') {
    e.localizacao = 'Marque a unidade no mapa — sem o ponto de chegada não há rota.';
  } else if (!Number.isFinite(lat) || lat < -90 || lat > 90) {
    e.latitude = 'Latitude inválida.';
  } else if (!Number.isFinite(lng) || lng < -180 || lng > 180) {
    e.longitude = 'Longitude inválida.';
  }
  return e;
}

export function FormularioUnidadeAtendimento({ modo, idUnidade, aoConcluir }: Props) {
  const [valores, setValores] = useState<Valores>(INICIAL);
  const [erros, setErros] = useState<Erros>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const cadastrar = useCadastrarUnidadeAtendimento();
  const atualizar = useAtualizarUnidadeAtendimento();
  const detalhe = useUnidadeAtendimento(modo === 'editar' ? idUnidade ?? null : null);

  useEffect(() => {
    if (modo !== 'editar' || !detalhe.data) return;
    const u = detalhe.data;
    const e = u.endereco;
    setValores({
      nome: u.nome,
      telefone: u.telefone ?? '',
      observacoes: u.observacoes ?? '',
      externa: u.externa,
      latitude: u.latitude == null ? '' : String(u.latitude),
      longitude: u.longitude == null ? '' : String(u.longitude),
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
  }, [modo, detalhe.data]);

  function set<K extends keyof Valores>(k: K, v: Valores[K]) {
    setValores((prev) => ({ ...prev, [k]: v }));
  }

  async function aoEnviar(ev: FormEvent) {
    ev.preventDefault();
    setErroGlobal(null);
    const novosErros = validar(valores);
    setErros(novosErros);
    if (Object.keys(novosErros).length > 0) return;

    const e = valores.endereco;
    const payload: SalvarUnidadeAtendimentoPayload = {
      nome: valores.nome.trim().replace(/\s+/g, ' ').toUpperCase(),
      endereco: {
        cep: e.cep.replace(/\D/g, ''),
        logradouro: e.logradouro.trim(),
        numero: e.numero.trim() || null,
        complemento: e.complemento.trim() || null,
        bairro: e.bairro.trim(),
        cidade: e.cidade.trim(),
        uf: e.uf.trim().toUpperCase(),
        pontoReferencia: e.pontoReferencia.trim() || null,
      },
      telefone: valores.telefone.trim() || null,
      observacoes: valores.observacoes.trim() || null,
      latitude: Number(valores.latitude),
      longitude: Number(valores.longitude),
      externa: valores.externa,
    };

    try {
      if (modo === 'criar') {
        const id = await cadastrar.mutateAsync(payload);
        aoConcluir(id);
      } else {
        if (!idUnidade) throw new Error('ID ausente.');
        await atualizar.mutateAsync({ id: idUnidade, payload });
        aoConcluir(idUnidade);
      }
    } catch (erro) {
      setErroGlobal(extrairMensagemDeErro(erro));
    }
  }

  const pendente = cadastrar.isPending || atualizar.isPending;

  const enderecoParaBuscar = [
    valores.endereco.logradouro,
    valores.endereco.numero,
    valores.endereco.bairro,
    valores.endereco.cidade,
    valores.endereco.uf,
  ]
    .filter((p) => p && p.trim().length > 0)
    .join(', ');

  return (
    <form onSubmit={aoEnviar} className="space-y-6" noValidate>
      {modo === 'editar' && detalhe.isFetching ? (
        <div className="text-sm text-gray-500">Carregando dados…</div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Campo label="Nome" htmlFor="ua-nome" erro={erros.nome} required className="md:col-span-2"
          dica="Sempre em MAIÚSCULAS — é o que aparece no atendimento e na rota.">
          <Input
            id="ua-nome"
            value={valores.nome}
            onChange={(e) => set('nome', e.target.value.toUpperCase())}
            placeholder="EX.: CLÍNICA NEFROLÓGICA DA ALAMEDA"
            maxLength={200}
            autoCapitalize="characters"
          />
        </Campo>

        <Campo label="Telefone" htmlFor="ua-telefone">
          <Input
            id="ua-telefone"
            value={valores.telefone}
            onChange={(e) => set('telefone', e.target.value)}
            placeholder="(21) 99999-0000"
            maxLength={30}
          />
        </Campo>

        <div className="flex items-end pb-2">
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={valores.externa}
              onChange={(e) => set('externa', e.target.checked)}
            />
            Fora do município (TFD)
          </label>
        </div>
      </div>

      <section>
        <h3 className="mb-1 text-sm font-semibold text-gray-900">Endereço</h3>
        <p className="mb-3 text-xs text-gray-500">
          Digite o CEP e clique em "Buscar" para preencher o resto. Logradouro, bairro, cidade e UF são obrigatórios.
        </p>
        <FormularioEndereco
          valor={valores.endereco}
          aoMudar={(e) => set('endereco', e)}
          prefixoIds="ua-endereco"
          erros={erros}
          desabilitado={pendente}
        />
      </section>

      <section>
        <h3 className="mb-1 text-sm font-semibold text-gray-900">
          Localização no mapa <span className="text-red-600">*</span>
        </h3>
        <p className="mb-3 text-xs text-gray-500">
          É o ponto de chegada da van no cálculo da rota. Clique em "Localizar pelo endereço", confira
          onde o pin caiu e arraste até a porta de entrada (ou clique direto no mapa).
        </p>

        <MapaSeletor
          valor={
            valores.latitude && valores.longitude
              ? { lat: Number(valores.latitude), lng: Number(valores.longitude) }
              : null
          }
          aoMudar={(c) => {
            set('latitude', c.lat.toFixed(6));
            set('longitude', c.lng.toFixed(6));
          }}
          enderecoParaBuscar={enderecoParaBuscar}
          desabilitado={pendente}
        />
        {erros.localizacao ? <p className="mt-2 text-sm text-red-700">{erros.localizacao}</p> : null}

        <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2">
          <Campo label="Latitude" htmlFor="ua-lat" erro={erros.latitude}>
            <Input
              id="ua-lat"
              value={valores.latitude}
              onChange={(e) => set('latitude', e.target.value)}
              inputMode="decimal"
              placeholder="-22.9176"
            />
          </Campo>
          <Campo label="Longitude" htmlFor="ua-lng" erro={erros.longitude}>
            <Input
              id="ua-lng"
              value={valores.longitude}
              onChange={(e) => set('longitude', e.target.value)}
              inputMode="decimal"
              placeholder="-42.8186"
            />
          </Campo>
        </div>
      </section>

      <Campo label="Observações para o motorista" htmlFor="ua-obs"
        dica="Entrada de ambulância, portão, onde o paciente desembarca, horário de funcionamento.">
        <textarea
          id="ua-obs"
          className="input min-h-[80px]"
          value={valores.observacoes}
          onChange={(e) => set('observacoes', e.target.value)}
          maxLength={1000}
        />
      </Campo>

      {erroGlobal ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroGlobal}
        </div>
      ) : null}

      <div className="flex items-center justify-end gap-3 pt-2">
        <Button type="button" variante="ghost" onClick={() => aoConcluir(null)} disabled={pendente}>
          Cancelar
        </Button>
        <Button type="submit" disabled={pendente}>
          {pendente ? 'Salvando…' : modo === 'criar' ? 'Cadastrar' : 'Salvar alterações'}
        </Button>
      </div>
    </form>
  );
}
