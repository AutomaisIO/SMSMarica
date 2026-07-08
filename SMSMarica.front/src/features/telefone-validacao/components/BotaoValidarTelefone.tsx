import { useState } from 'react';
import { BadgeCheck, ShieldCheck } from 'lucide-react';
import { ModalOtpTelefone } from '@/features/telefone-validacao/components/ModalOtpTelefone';

type Props = {
  /** CPF da pessoa (a chave do contato). Sem CPF de 11 díg. o botão não aparece. */
  cpf: string;
  /** Número do contato principal como está na tela (com ou sem máscara). */
  numero: string;
  /**
   * Número verificado que JÁ VEIO no objeto do paciente (telefoneVerificado do
   * PacienteDto — marcador no telecom FHIR). Sem request extra e sem "piscada".
   */
  numeroVerificado?: string | null;
  className?: string;
  /** Chamado após validar com sucesso (OTP OK) — ex.: persistir o número no cadastro. */
  onValidado?: () => void;
};

/** Mesmo número tolerando DDI (um é sufixo do outro) — espelha o backend. */
function mesmoNumero(a: string, b: string): boolean {
  return a.length >= 8 && b.length >= 8 && (a.endsWith(b) || b.endsWith(a));
}

/**
 * Selo + ação de validação do CONTATO PRINCIPAL (WhatsApp) do PACIENTE, por OTP, ancorado
 * no CPF. O estado "validado" vem pronto no objeto do paciente (sem consulta própria); se o
 * número digitado é o verificado, mostra "Validado". Senão, oferece o botão que abre o
 * ModalOtpTelefone (número TRAVADO — valida o que está na tela). O número é único entre
 * pessoas: se já for de outra, o backend bloqueia (409).
 */
export function BotaoValidarTelefone({ cpf, numero, numeroVerificado, className, onValidado }: Props) {
  const cpfDig = (cpf ?? '').replace(/\D/g, '');
  const numDig = (numero ?? '').replace(/\D/g, '');
  const habilitado = cpfDig.length === 11 && numDig.length >= 10; // CPF + DDD (2) + número (>=8)
  const [aberto, setAberto] = useState(false);
  // Validação feita AGORA nesta tela (o objeto do paciente ainda não refletiu).
  const [validadoAgora, setValidadoAgora] = useState<string | null>(null);

  if (!habilitado) return null;

  const verificadoDig = (validadoAgora ?? numeroVerificado ?? '').replace(/\D/g, '');
  const validado = verificadoDig.length >= 8 && mesmoNumero(numDig, verificadoDig);

  if (validado) {
    return (
      <span
        className={`inline-flex items-center gap-1 text-xs font-medium text-emerald-700 ${className ?? ''}`}
        title="Contato validado"
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
          setValidadoAgora(numDig);
          setAberto(false);
          onValidado?.();
        }}
      />
    </>
  );
}
