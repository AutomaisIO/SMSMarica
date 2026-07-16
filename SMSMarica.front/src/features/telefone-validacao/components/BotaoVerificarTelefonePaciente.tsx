import { useEffect, useRef, useState } from 'react';
import { ShieldCheck, Timer } from 'lucide-react';
import { ModalOtpTelefone } from '@/features/telefone-validacao/components/ModalOtpTelefone';
import { useOtpPendente } from '@/features/telefone-validacao/lib/otpPendente';

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
  const pendente = useOtpPendente(cpf);

  // #13: ao (re)entrar na tela com um código ainda válido pendente, o modal reabre sozinho
  // no campo do código — sem exigir clique nem reenviar. Uma vez por montagem: fechar no X
  // não reabre até voltar à página; "cancelar a verificação" mata a pendência de vez.
  const autoAbriu = useRef(false);
  useEffect(() => {
    if (pendente && !autoAbriu.current) {
      autoAbriu.current = true;
      setAberto(true);
    }
  }, [pendente]);

  if ((cpf ?? '').replace(/\D/g, '').length !== 11) return null;

  // Já mandamos o código e ele ainda vale: o botão diz isso, e clicar volta ao campo do
  // código (não dispara outro).
  const cor = pendente
    ? 'border-amber-300 bg-amber-50 text-amber-700 hover:bg-amber-100'
    : 'border-indigo-300 bg-indigo-50 text-indigo-700 hover:bg-indigo-100';

  return (
    <>
      <button
        type="button"
        onClick={() => setAberto(true)}
        title={pendente ? `Código enviado para ${pendente.mascara ?? pendente.numero} — digite-o` : undefined}
        className={`inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-xs font-medium ${cor} ${className ?? ''}`}
      >
        {pendente ? <Timer className="h-3.5 w-3.5" /> : <ShieldCheck className="h-3.5 w-3.5" />}
        {pendente ? 'Digitar código' : 'Verificar'}
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
