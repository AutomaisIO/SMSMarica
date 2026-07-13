import { useEffect, useState } from 'react';
import { Loader2, Smartphone } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Campo } from '@/shared/ui/Campo';
import { useConfirmarTelefoneOtp, useEnviarTelefoneOtp } from '@/features/telefone-validacao/api/queries';
import {
  lerOtpPendente,
  limparOtpPendente,
  salvarOtpPendente,
} from '@/features/telefone-validacao/lib/otpPendente';

type Props = {
  aberto: boolean;
  /** CPF da pessoa (chave do contato). */
  cpf: string;
  /** Número inicial (o do cadastro/da tela). */
  numeroInicial: string;
  /** true = a recepção pode digitar/ajustar o número; false = número travado (validar o que está na tela). */
  numeroEditavel: boolean;
  aoFechar: () => void;
  /** Chamado após confirmar o código (backend já gravou contato_validado + FHIR). */
  aoValidado: () => void;
};

/**
 * Modal ÚNICO do fluxo OTP de contato (WhatsApp): envia o código ao número e confirma.
 * Usado pelo BotaoValidarTelefone (número travado, valida o que está na tela) e pelo
 * BotaoVerificarTelefonePaciente (número editável, recepção digita/corrige).
 * Ao confirmar, o backend grava o contato verificado e estampa o principal no FHIR.
 */
export function ModalOtpTelefone({ aberto, cpf, numeroInicial, numeroEditavel, aoFechar, aoValidado }: Props) {
  const enviar = useEnviarTelefoneOtp();
  const confirmar = useConfirmarTelefoneOtp();
  const [etapa, setEtapa] = useState<'numero' | 'codigo'>('numero');
  const [numero, setNumero] = useState(numeroInicial);
  const [mascara, setMascara] = useState<string | null>(null);
  const [modoTeste, setModoTeste] = useState(false);
  const [codigo, setCodigo] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  // A cada abertura: se já mandamos um código para este CPF e ele ainda vale, retoma o
  // fluxo no campo do código. Reenviar aqui invalidaria o código que o paciente tem na mão.
  useEffect(() => {
    if (!aberto) return;
    setCodigo('');
    setErro(null);

    const pendente = lerOtpPendente(cpf);
    if (pendente) {
      setEtapa('codigo');
      setNumero(pendente.numero);
      setMascara(pendente.mascara);
      setModoTeste(pendente.modoTeste);
      return;
    }

    setEtapa('numero');
    setNumero(numeroInicial);
    setMascara(null);
    setModoTeste(false);
  }, [aberto, cpf, numeroInicial]);

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
      salvarOtpPendente(cpf, {
        numero,
        mascara: r.mascara,
        modoTeste: r.canal === 'tela-teste',
        expiraEm: Date.now() + r.expiraEmSegundos * 1000,
      });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoConfirmar() {
    setErro(null);
    try {
      await confirmar.mutateAsync({ cpf, numero, codigo });
      limparOtpPendente(cpf);
      aoValidado();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  /** Desiste da verificação: só aqui (ou ao confirmar/expirar) a pendência morre. */
  function aoCancelarVerificacao() {
    limparOtpPendente(cpf);
    aoFechar();
  }

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo="Verificar contato (WhatsApp)" largura="sm">
      <div className="space-y-4">
        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
        ) : null}

        {etapa === 'numero' ? (
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-sm text-gray-600">
              <Smartphone className="h-4 w-4 text-gray-400" />
              {numeroEditavel
                ? 'Digite o celular do paciente. Vamos enviar um código por WhatsApp.'
                : 'Vamos enviar um código por WhatsApp para o número em tela.'}
            </div>
            {numeroEditavel ? (
              <Campo label="Celular / WhatsApp" htmlFor="otp-telefone-numero">
                <Input
                  id="otp-telefone-numero"
                  value={numero}
                  onChange={(e) => setNumero(e.target.value)}
                  inputMode="tel"
                  placeholder="(21) 90000-0000"
                  autoFocus
                />
              </Campo>
            ) : (
              <p className="rounded-md bg-gray-50 px-3 py-2 text-sm font-medium text-gray-900">{numero}</p>
            )}
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
              {numeroEditavel ? (
                <Button
                  variante="outline"
                  onClick={() => {
                    // O número estava errado: o código enviado não serve mais.
                    limparOtpPendente(cpf);
                    setEtapa('numero');
                  }}
                  className="flex-1"
                >
                  Corrigir número
                </Button>
              ) : null}
              <Button variante="outline" onClick={aoEnviar} disabled={enviar.isPending} className="flex-1">
                {enviar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                Reenviar
              </Button>
              <Button onClick={aoConfirmar} disabled={confirmar.isPending || codigo.length < 6} className="flex-1">
                {confirmar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                Confirmar
              </Button>
            </div>

            <p className="text-xs text-gray-500">
              Pode fechar e voltar depois: o código continua valendo e esta tela volta aqui. Para
              desistir de vez,{' '}
              <button
                type="button"
                onClick={aoCancelarVerificacao}
                className="font-medium text-red-600 underline hover:text-red-700"
              >
                cancelar a verificação
              </button>
              .
            </p>
          </div>
        )}
      </div>
    </Modal>
  );
}
