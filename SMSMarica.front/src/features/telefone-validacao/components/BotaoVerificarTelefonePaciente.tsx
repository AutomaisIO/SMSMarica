import { useEffect, useState } from 'react';
import { Loader2, ShieldCheck, Smartphone } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Campo } from '@/shared/ui/Campo';
import { useConfirmarTelefoneOtp, useEnviarTelefoneOtp } from '@/features/telefone-validacao/api/queries';

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
 * Botão "Verificar" que abre o fluxo de OTP com o número EDITÁVEL: a recepção digita/ajusta
 * o número, envia o código por WhatsApp e confirma. Ao confirmar, o backend já grava o contato
 * verificado e estampa o número principal no FHIR — sem precisar salvar mais nada.
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
      {aberto ? (
        <ModalVerificar
          cpf={cpf}
          numeroInicial={numeroInicial ?? ''}
          aoFechar={() => setAberto(false)}
          aoValidado={() => {
            setAberto(false);
            aoValidado?.();
          }}
        />
      ) : null}
    </>
  );
}

function ModalVerificar({
  cpf,
  numeroInicial,
  aoFechar,
  aoValidado,
}: {
  cpf: string;
  numeroInicial: string;
  aoFechar: () => void;
  aoValidado: () => void;
}) {
  const enviar = useEnviarTelefoneOtp();
  const confirmar = useConfirmarTelefoneOtp();
  const [etapa, setEtapa] = useState<'numero' | 'codigo'>('numero');
  const [numero, setNumero] = useState(numeroInicial);
  const [mascara, setMascara] = useState<string | null>(null);
  const [modoTeste, setModoTeste] = useState(false);
  const [codigo, setCodigo] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    setEtapa('numero');
    setNumero(numeroInicial);
    setMascara(null);
    setModoTeste(false);
    setCodigo('');
    setErro(null);
  }, [numeroInicial]);

  async function aoEnviar() {
    setErro(null);
    if (numero.replace(/\D/g, '').length < 10) {
      setErro('Informe um celular com DDD.');
      return;
    }
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
    <Modal aberto aoFechar={aoFechar} titulo="Verificar telefone (WhatsApp)" largura="sm">
      <div className="space-y-4">
        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
        ) : null}

        {etapa === 'numero' ? (
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-sm text-gray-600">
              <Smartphone className="h-4 w-4 text-gray-400" />
              Digite o celular do paciente. Vamos enviar um código por WhatsApp.
            </div>
            <Campo label="Celular / WhatsApp" htmlFor="verif-numero">
              <Input
                id="verif-numero"
                value={numero}
                onChange={(e) => setNumero(e.target.value)}
                inputMode="tel"
                placeholder="(21) 90000-0000"
                autoFocus
              />
            </Campo>
            <Button onClick={aoEnviar} disabled={enviar.isPending} className="w-full">
              {enviar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Enviar código
            </Button>
          </div>
        ) : (
          <div className="space-y-3">
            <p className="text-sm text-gray-600">
              Enviamos um código {mascara ? <>para <span className="font-medium">{mascara}</span></> : null}. Digite-o
              abaixo.
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
              <Button variante="outline" onClick={() => setEtapa('numero')} className="flex-1">
                Corrigir número
              </Button>
              <Button onClick={aoConfirmar} disabled={confirmar.isPending || codigo.length < 6} className="flex-1">
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
