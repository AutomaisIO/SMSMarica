import { useState } from 'react';
import { ImageIcon, ScanFace, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstanteData } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import {
  useApagarBiometria,
  useBiometriaMotorista,
  useFotoBiometria,
} from '@/features/biometria/api/queries';
import { QuadroFoto } from '@/features/biometria/components/QuadroFoto';

function ModalFotoCadastro({
  motoristaId,
  nomeMotorista,
  aoFechar,
}: {
  motoristaId: string;
  nomeMotorista?: string;
  aoFechar: () => void;
}) {
  const foto = useFotoBiometria(motoristaId, true);
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Foto do cadastro do rosto" descricao={nomeMotorista}>
      <QuadroFoto
        carregando={foto.isLoading}
        erro={foto.error}
        base64={foto.data}
        textoSemFoto="Este motorista não tem rosto cadastrado."
        alt={`Rosto cadastrado de ${nomeMotorista ?? 'motorista'}`}
      />
      {foto.data ? (
        <p className="mt-3 text-xs text-gray-500">
          Tirada pelo próprio motorista no primeiro acesso, no tablet. É com esta foto que os tablets
          conferem o rosto dele.
        </p>
      ) : null}
    </Modal>
  );
}

/**
 * Cartão "Reconhecimento facial no tablet" da ficha do motorista (ADR-0073): se ele já cadastrou o
 * rosto (onde, quando e qual termo aceitou), a foto do cadastro sob demanda e o "Apagar cadastro do
 * rosto" (Motoristas/Edição) — que obriga o motorista a refazer o primeiro acesso no tablet.
 */
export function ReconhecimentoFacialMotorista({
  motoristaId,
  nomeMotorista,
  podeEditar,
}: {
  motoristaId: string;
  nomeMotorista?: string;
  podeEditar: boolean;
}) {
  const situacao = useBiometriaMotorista(motoristaId);
  const apagar = useApagarBiometria(motoristaId);
  const [vendoFoto, setVendoFoto] = useState(false);
  const [confirmando, setConfirmando] = useState(false);

  const s = situacao.data;

  function confirmarApagar() {
    apagar.mutate(undefined, {
      onSuccess: () => {
        setConfirmando(false);
        notificar('Cadastro do rosto apagado. O motorista refaz o primeiro acesso no tablet.', 'sucesso');
      },
    });
  }

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-gray-900">
        <ScanFace className="h-4 w-4" /> Reconhecimento facial no tablet
      </h2>

      {situacao.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : situacao.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(situacao.error)}
        </div>
      ) : s?.cadastrada ? (
        <div className="space-y-1 text-sm text-gray-700">
          <p>
            <span className="font-medium text-emerald-700">Rosto cadastrado</span>
            {s.cadastradaEm ? ` em ${formatarInstanteData(s.cadastradaEm)}` : ''}
            {s.veiculoPlaca ? ` no veículo ${s.veiculoPlaca}` : ''}
            {s.termoVersao ? ` · termo de ciência versão ${s.termoVersao}` : ''}.
          </p>
          <p className="text-xs text-gray-500">
            Ele entra pelo rosto no tablet do veículo em que estiver escalado no dia.
          </p>
        </div>
      ) : s ? (
        <div className="space-y-1 text-sm text-gray-700">
          <p>
            <span className="font-medium text-amber-700">Ainda não cadastrou</span> — faz no primeiro
            acesso no tablet do veículo escalado.
          </p>
          <p className="text-xs text-gray-500">
            No tablet: “Primeiro acesso (cadastrar meu rosto)”, com o CPF e um código que chega no
            WhatsApp do telefone deste cadastro.
          </p>
        </div>
      ) : null}

      {s?.cadastrada ? (
        <div className="mt-4 flex flex-wrap gap-2">
          <Button variante="outline" tamanho="sm" onClick={() => setVendoFoto(true)}>
            <ImageIcon className="h-4 w-4" /> Ver foto do cadastro
          </Button>
          {podeEditar ? (
            <Button
              variante="outline"
              tamanho="sm"
              className="text-red-700"
              onClick={() => {
                apagar.reset();
                setConfirmando(true);
              }}
            >
              <Trash2 className="h-4 w-4" /> Apagar cadastro do rosto
            </Button>
          ) : null}
        </div>
      ) : null}

      {vendoFoto ? (
        <ModalFotoCadastro
          motoristaId={motoristaId}
          nomeMotorista={nomeMotorista}
          aoFechar={() => setVendoFoto(false)}
        />
      ) : null}

      <ConfirmDialog
        aberto={confirmando}
        titulo="Apagar cadastro do rosto"
        mensagem={`${
          nomeMotorista ? `${nomeMotorista} deixa` : 'O motorista deixa'
        } de entrar pelo rosto em qualquer tablet. Para voltar, ele refaz o primeiro acesso no tablet do veículo em que estiver escalado (CPF, código no WhatsApp, termo e foto nova). Enquanto isso, só entra com código da central. As conferências já registradas continuam guardadas.`}
        destrutivo
        rotuloConfirmar="Apagar cadastro"
        carregando={apagar.isPending}
        erro={apagar.isError ? extrairMensagemDeErro(apagar.error) : null}
        aoConfirmar={confirmarApagar}
        aoCancelar={() => setConfirmando(false)}
      />
    </section>
  );
}
