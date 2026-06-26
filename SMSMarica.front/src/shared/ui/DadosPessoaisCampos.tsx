import { Lock } from 'lucide-react';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { UploadFoto } from '@/shared/ui/UploadFoto';
import { BotaoValidarTelefone } from '@/features/telefone-validacao/components/BotaoValidarTelefone';
import {
  FormularioEndereco,
  type EnderecoForm,
} from '@/shared/ui/FormularioEndereco';

export type DadosPessoaisValores = {
  nomeCompleto: string;
  cpf: string;
  dataNascimento: string;
  email: string;
  telefone: string;
  endereco: EnderecoForm;
  fotoBase64: string | null;
};

type Props = {
  valores: DadosPessoaisValores;
  erros: Record<string, string | undefined>;
  aoMudarCampo: <K extends keyof DadosPessoaisValores>(c: K, v: DadosPessoaisValores[K]) => void;
  /** Bloqueia nome/CPF/dataNasc (true após gate ou em modo editar). */
  identidadeReadOnly?: boolean;
  /** Bloqueia email (true em modo editar; false em criar). */
  emailReadOnly?: boolean;
  /** Desabilita interação geral (loading). */
  desabilitado?: boolean;
  mostrarPontoReferencia?: boolean;
  /** Slot opcional renderizado dentro do grid, depois do telefone. */
  camposExtras?: React.ReactNode;
  /** Exibe o selo/ação de validar o telefone por OTP (WhatsApp). */
  validarTelefone?: boolean;
}

const lockIcon = (
  <span className="inline-flex items-center gap-1">
    <Lock className="h-3 w-3" /> Imutável
  </span>
);

/**
 * Bloco visual compartilhado de "dados pessoais base" (foto + identidade +
 * contato + endereço) usado nos formulários de Usuário, Médico e Motorista.
 * Estado é controlado pelo pai — este componente é puramente visual.
 */
export function DadosPessoaisCampos({
  valores,
  erros,
  aoMudarCampo,
  identidadeReadOnly = true,
  emailReadOnly = false,
  desabilitado,
  mostrarPontoReferencia = true,
  camposExtras,
  validarTelefone = false,
}: Props) {
  return (
    <div className="space-y-5">
      <UploadFoto
        valor={valores.fotoBase64}
        aoMudar={(v) => aoMudarCampo('fotoBase64', v)}
        nome={valores.nomeCompleto || undefined}
        desabilitado={desabilitado}
      />

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Campo
          label="Nome completo"
          htmlFor="nomeCompleto"
          erro={erros.nomeCompleto}
          required
          className="md:col-span-2"
          dica={identidadeReadOnly ? lockIcon : undefined}
        >
          <Input
            id="nomeCompleto"
            value={valores.nomeCompleto}
            onChange={(e) => aoMudarCampo('nomeCompleto', e.target.value)}
            required
            disabled={identidadeReadOnly}
            readOnly={identidadeReadOnly}
          />
        </Campo>

        <Campo
          label="CPF"
          htmlFor="cpf"
          erro={erros.cpf}
          dica={identidadeReadOnly ? lockIcon : undefined}
        >
          <Input
            id="cpf"
            value={valores.cpf}
            onChange={(e) => aoMudarCampo('cpf', e.target.value)}
            inputMode="numeric"
            placeholder="00000000000"
            disabled={identidadeReadOnly}
            readOnly={identidadeReadOnly}
          />
        </Campo>

        <Campo
          label="Data de nascimento"
          htmlFor="dataNascimento"
          erro={erros.dataNascimento}
          dica={identidadeReadOnly ? lockIcon : undefined}
        >
          <Input
            id="dataNascimento"
            type="date"
            value={valores.dataNascimento}
            onChange={(e) => aoMudarCampo('dataNascimento', e.target.value)}
            disabled={identidadeReadOnly}
            readOnly={identidadeReadOnly}
          />
        </Campo>

        <Campo
          label="E-mail"
          htmlFor="email"
          erro={erros.email}
          dica={emailReadOnly ? 'E-mail não pode ser alterado.' : undefined}
        >
          <Input
            id="email"
            type="email"
            value={valores.email}
            onChange={(e) => aoMudarCampo('email', e.target.value)}
            disabled={emailReadOnly || desabilitado}
            readOnly={emailReadOnly}
          />
        </Campo>

        <Campo label="Telefone" htmlFor="telefone" erro={erros.telefone}>
          <Input
            id="telefone"
            value={valores.telefone}
            onChange={(e) => aoMudarCampo('telefone', e.target.value)}
            placeholder="(21) 99999-0000"
            disabled={desabilitado}
          />
          {validarTelefone ? (
            <div className="mt-1.5">
              <BotaoValidarTelefone numero={valores.telefone} />
            </div>
          ) : null}
        </Campo>

        {camposExtras}
      </div>

      <section>
        <h3 className="mb-3 text-sm font-semibold text-gray-900">Endereço</h3>
        <FormularioEndereco
          valor={valores.endereco}
          aoMudar={(e) => aoMudarCampo('endereco', e)}
          desabilitado={desabilitado}
          mostrarPontoReferencia={mostrarPontoReferencia}
        />
      </section>
    </div>
  );
}
