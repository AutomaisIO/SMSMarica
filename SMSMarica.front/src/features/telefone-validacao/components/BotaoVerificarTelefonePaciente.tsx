import { useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { ModalOtpTelefone } from '@/features/telefone-validacao/components/ModalOtpTelefone';

type Props = {
  /** CPF da pessoa (chave do contato). Sem CPF de 11 díg. o botão não aparece. */
  cpf: string;
  /** Número sugerido (o que está no cadastro), editável no modal. */
  numeroInicial?: string | null;
  className?: string;
  /** Chamado após verificar com sucesso (FHIR já atualizado pelo backend). */
  aoValidado?: () => void;
};

/**
 * Botão "Verificar" com o número EDITÁVEL: a recepção digita/ajusta o número, envia o código
 * por WhatsApp e confirma. Ao confirmar, o backend já grava o contato verificado e estampa o
 * número principal no FHIR — sem precisar salvar mais nada. Usa o ModalOtpTelefone (único).
 */
export function BotaoVerificarTelefonePaciente({ cpf, numeroInicial, className, aoValidado }: Props) {
  const [aberto, setAberto] = useState(false);
  if ((cpf ?? '').replace(/\D/g, '').length !== 11) return null;

  return (
    <>
      <button
        type="button"
        onClick={() => setAberto(true)}
        className={`inline-flex items-center gap-1 rounded-md border border-indigo-300 bg-indigo-50 px-2 py-0.5 text-xs font-medium text-indigo-700 hover:bg-indigo-100 ${className ?? ''}`}
      >
        <ShieldCheck className="h-3.5 w-3.5" />
        Verificar
      </button>
      <ModalOtpTelefone
        aberto={aberto}
        cpf={cpf}
        numeroInicial={numeroInicial ?? ''}
        numeroEditavel
        aoFechar={() => setAberto(false)}
        aoValidado={() => {
          setAberto(false);
          aoValidado?.();
        }}
      />
    </>
  );
}
