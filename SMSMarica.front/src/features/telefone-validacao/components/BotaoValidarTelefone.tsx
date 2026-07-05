import { useState } from 'react';
import { BadgeCheck, ShieldCheck } from 'lucide-react';
import { useTelefoneValidado } from '@/features/telefone-validacao/api/queries';
import { ModalOtpTelefone } from '@/features/telefone-validacao/components/ModalOtpTelefone';

type Props = {
  /** CPF da pessoa (a chave do contato). Sem CPF de 11 díg. o botão não aparece. */
  cpf: string;
  /** Número do contato principal como está na tela (com ou sem máscara). */
  numero: string;
  className?: string;
  /** Chamado após validar com sucesso (OTP OK) — ex.: persistir o número no cadastro. */
  onValidado?: () => void;
};

/**
 * Selo + ação de validação do CONTATO PRINCIPAL (WhatsApp) da pessoa, por OTP, ancorado
 * no CPF. Se o CPF já tem este número como contato validado, mostra "Validado". Senão,
 * oferece o botão que abre o ModalOtpTelefone (único, número TRAVADO — valida o que está
 * na tela). O número é único entre pessoas: se já for de outra, o backend bloqueia (409).
 */
export function BotaoValidarTelefone({ cpf, numero, className, onValidado }: Props) {
  const cpfDig = (cpf ?? '').replace(/\D/g, '');
  const numDig = (numero ?? '').replace(/\D/g, '');
  const habilitado = cpfDig.length === 11 && numDig.length >= 10; // CPF + DDD (2) + número (>=8)
  const validadoQ = useTelefoneValidado(habilitado ? cpf : null, habilitado ? numero : null);
  const [aberto, setAberto] = useState(false);

  if (!habilitado) return null;

  if (validadoQ.data?.validado) {
    return (
      <span
        className={`inline-flex items-center gap-1 text-xs font-medium text-emerald-700 ${className ?? ''}`}
        title={
          validadoQ.data.validadoEm
            ? `Validado em ${new Date(validadoQ.data.validadoEm).toLocaleString('pt-BR')}`
            : 'Contato validado'
        }
      >
        <BadgeCheck className="h-4 w-4" />
        Validado
      </span>
    );
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setAberto(true)}
        className={`inline-flex items-center gap-1 rounded-md border border-indigo-300 bg-indigo-50 px-2.5 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-100 ${className ?? ''}`}
      >
        <ShieldCheck className="h-3.5 w-3.5" />
        Validar contato
      </button>
      <ModalOtpTelefone
        aberto={aberto}
        cpf={cpf}
        numeroInicial={numero}
        numeroEditavel={false}
        aoFechar={() => setAberto(false)}
        aoValidado={() => {
          validadoQ.refetch();
          setAberto(false);
          onValidado?.();
        }}
      />
    </>
  );
}
