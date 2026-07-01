import { useEffect, useState } from 'react';
import { BadgeCheck, Loader2, ShieldCheck, Smartphone } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import {
  useConfirmarTelefoneOtp,
  useEnviarTelefoneOtp,
  useTelefoneValidado,
} from '@/features/telefone-validacao/api/queries';

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
 * oferece o botão que dispara o código no WhatsApp e abre o modal aguardando o código.
 * O número é único entre pessoas: se já for de outra pessoa, o backend bloqueia (409).
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
      <ModalValidarTelefone
        cpf={cpf}
        numero={numero}
        aberto={aberto}
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

function ModalValidarTelefone({
  cpf,
  numero,
  aberto,
  aoFechar,
  aoValidado,
}: {
  cpf: string;
  numero: string;
  aberto: boolean;
  aoFechar: () => void;
  aoValidado: () => void;
}) {
  const enviar = useEnviarTelefoneOtp();
  const confirmar = useConfirmarTelefoneOtp();
  const [etapa, setEtapa] = useState<'enviar' | 'codigo'>('enviar');
  const [mascara, setMascara] = useState<string | null>(null);
  const [modoTeste, setModoTeste] = useState(false);
  const [codigo, setCodigo] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  // Reseta o fluxo a cada abertura.
  useEffect(() => {
    if (aberto) {
      setEtapa('enviar');
      setMascara(null);
      setModoTeste(false);
      setCodigo('');
      setErro(null);
    }
  }, [aberto]);

  async function aoEnviar() {
    setErro(null);
    try {
      const r = await enviar.mutateAsync({ cpf, numero });
      setMascara(r.mascara);
      setModoTeste(r.canal === 'tela-teste');
      setEtapa('codigo');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoConfirmar() {
    setErro(null);
    try {
      await confirmar.mutateAsync({ cpf, numero, codigo });
      aoValidado();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo="Validar contato (WhatsApp)" largura="sm">
      <div className="space-y-4">
        <div className="flex items-center gap-2 text-sm text-gray-600">
          <Smartphone className="h-4 w-4 text-gray-400" />
          Vamos enviar um código por WhatsApp para o número em tela.
        </div>

        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </div>
        ) : null}

        {etapa === 'enviar' ? (
          <Button onClick={aoEnviar} disabled={enviar.isPending} className="w-full">
            {enviar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Enviar código
          </Button>
        ) : (
          <div className="space-y-3">
            <p className="text-sm text-gray-600">
              Enviamos um código {mascara ? <>para <span className="font-medium">{mascara}</span></> : null}.
              Digite-o abaixo.
            </p>
            {modoTeste ? (
              <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
                WhatsApp não configurado — em modo teste o código não é enviado de verdade.
              </div>
            ) : null}
            <Input
              value={codigo}
              onChange={(e) => setCodigo(e.target.value.replace(/\D/g, '').slice(0, 6))}
              inputMode="numeric"
              placeholder="000000"
              autoFocus
              className="text-center text-lg tracking-[0.3em]"
            />
            <div className="flex gap-2">
              <Button
                variante="outline"
                onClick={aoEnviar}
                disabled={enviar.isPending}
                className="flex-1"
              >
                {enviar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                Reenviar
              </Button>
              <Button
                onClick={aoConfirmar}
                disabled={confirmar.isPending || codigo.length < 6}
                className="flex-1"
              >
                {confirmar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                Confirmar
              </Button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}
