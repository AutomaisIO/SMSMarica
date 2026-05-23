import { useState } from 'react';
import { Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { consultarCep } from '@/shared/api/integracoes';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';

export type EnderecoForm = {
  cep: string;
  logradouro: string;
  numero: string;
  complemento: string;
  bairro: string;
  cidade: string;
  uf: string;
  pontoReferencia: string;
};

export const enderecoVazio: EnderecoForm = {
  cep: '', logradouro: '', numero: '', complemento: '',
  bairro: '', cidade: '', uf: '', pontoReferencia: '',
};

type Props = {
  valor: EnderecoForm;
  aoMudar: (e: EnderecoForm) => void;
  prefixoIds?: string;
  erros?: Record<string, string | undefined>;
  desabilitado?: boolean;
  /** Permite ocultar o campo "Ponto de referência" em formulários onde ele não faz sentido. */
  mostrarPontoReferencia?: boolean;
};

/**
 * Componente compartilhado de endereço com busca por CEP via Hub.
 * Espera estado controlado pelo pai. Use enderecoVazio() para inicializar.
 */
export function FormularioEndereco({
  valor,
  aoMudar,
  prefixoIds = 'endereco',
  erros = {},
  desabilitado,
  mostrarPontoReferencia = true,
}: Props) {
  const [buscando, setBuscando] = useState(false);
  const [erroCep, setErroCep] = useState<string | null>(null);

  function setCampo<K extends keyof EnderecoForm>(campo: K, novoValor: EnderecoForm[K]) {
    aoMudar({ ...valor, [campo]: novoValor });
  }

  async function buscarCep() {
    const cepLimpo = valor.cep.replace(/\D/g, '');
    if (cepLimpo.length !== 8) {
      setErroCep('CEP precisa ter 8 dígitos.');
      return;
    }
    setErroCep(null);
    setBuscando(true);
    try {
      const r = await consultarCep(cepLimpo);
      aoMudar({
        ...valor,
        cep: cepLimpo,
        logradouro: r.logradouro || valor.logradouro,
        bairro: r.bairro || valor.bairro,
        cidade: r.localidade || valor.cidade,
        uf: r.uf || valor.uf,
        complemento: r.complemento || valor.complemento,
      });
    } catch (e) {
      setErroCep(extrairMensagemDeErro(e));
    } finally {
      setBuscando(false);
    }
  }

  return (
    <div className="grid grid-cols-1 gap-5 md:grid-cols-6">
      <Campo
        label="CEP"
        htmlFor={`${prefixoIds}-cep`}
        erro={erros['cep'] ?? erroCep ?? undefined}
        className="md:col-span-2"
      >
        <div className="flex gap-2">
          <Input
            id={`${prefixoIds}-cep`}
            value={valor.cep}
            onChange={(e) => setCampo('cep', e.target.value)}
            onBlur={() => {
              const limpo = valor.cep.replace(/\D/g, '');
              if (limpo.length === 8) buscarCep();
            }}
            placeholder="00000-000"
            disabled={desabilitado}
            inputMode="numeric"
          />
          <Button
            type="button"
            variante="outline"
            tamanho="sm"
            onClick={buscarCep}
            disabled={desabilitado || buscando}
            title="Buscar pelo CEP"
          >
            <Search className="h-4 w-4" />
            {buscando ? '...' : 'Buscar'}
          </Button>
        </div>
      </Campo>

      <Campo
        label="Logradouro"
        htmlFor={`${prefixoIds}-logradouro`}
        erro={erros['logradouro']}
        className="md:col-span-4"
      >
        <Input
          id={`${prefixoIds}-logradouro`}
          value={valor.logradouro}
          onChange={(e) => setCampo('logradouro', e.target.value)}
          disabled={desabilitado}
        />
      </Campo>

      <Campo
        label="Número"
        htmlFor={`${prefixoIds}-numero`}
        erro={erros['numero']}
        className="md:col-span-1"
      >
        <Input
          id={`${prefixoIds}-numero`}
          value={valor.numero}
          onChange={(e) => setCampo('numero', e.target.value)}
          disabled={desabilitado}
        />
      </Campo>

      <Campo
        label="Complemento"
        htmlFor={`${prefixoIds}-complemento`}
        erro={erros['complemento']}
        className="md:col-span-3"
      >
        <Input
          id={`${prefixoIds}-complemento`}
          value={valor.complemento}
          onChange={(e) => setCampo('complemento', e.target.value)}
          disabled={desabilitado}
        />
      </Campo>

      <Campo
        label="Bairro"
        htmlFor={`${prefixoIds}-bairro`}
        erro={erros['bairro']}
        className="md:col-span-2"
      >
        <Input
          id={`${prefixoIds}-bairro`}
          value={valor.bairro}
          onChange={(e) => setCampo('bairro', e.target.value)}
          disabled={desabilitado}
        />
      </Campo>

      <Campo
        label="Cidade"
        htmlFor={`${prefixoIds}-cidade`}
        erro={erros['cidade']}
        className="md:col-span-3"
      >
        <Input
          id={`${prefixoIds}-cidade`}
          value={valor.cidade}
          onChange={(e) => setCampo('cidade', e.target.value)}
          disabled={desabilitado}
        />
      </Campo>

      <Campo
        label="UF"
        htmlFor={`${prefixoIds}-uf`}
        erro={erros['uf']}
        className="md:col-span-1"
      >
        <Input
          id={`${prefixoIds}-uf`}
          value={valor.uf}
          maxLength={2}
          onChange={(e) => setCampo('uf', e.target.value.toUpperCase())}
          disabled={desabilitado}
        />
      </Campo>

      {mostrarPontoReferencia ? (
        <Campo
          label="Ponto de referência"
          htmlFor={`${prefixoIds}-ref`}
          erro={erros['pontoReferencia']}
          className="md:col-span-6"
        >
          <Input
            id={`${prefixoIds}-ref`}
            value={valor.pontoReferencia}
            onChange={(e) => setCampo('pontoReferencia', e.target.value)}
            disabled={desabilitado}
          />
        </Campo>
      ) : null}
    </div>
  );
}

export function temConteudo(e: EnderecoForm): boolean {
  return Boolean(
    e.cep || e.logradouro || e.numero || e.complemento ||
    e.bairro || e.cidade || e.uf || e.pontoReferencia,
  );
}

export function paraPayload(e: EnderecoForm): EnderecoForm | null {
  return temConteudo(e) ? e : null;
}
