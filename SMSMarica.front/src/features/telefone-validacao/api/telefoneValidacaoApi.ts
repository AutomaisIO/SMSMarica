import { http } from '@/shared/api/httpClient';

export type TelefoneOtpEmitido = {
  /** 'whatsapp' = enviado de verdade; 'tela-teste' = sem credencial/modo teste. */
  canal: string;
  mascara: string | null;
  expiraEmSegundos: number;
};

export type TelefoneValidado = {
  numero: string;
  validado: boolean;
  validadoEm: string | null;
};

/** Dispara o envio do código de validação por WhatsApp para o contato principal do CPF. */
export async function enviarTelefoneOtp(cpf: string, numero: string): Promise<TelefoneOtpEmitido> {
  const { data } = await http.post<TelefoneOtpEmitido>('/telefones/validacao/enviar', { cpf, numero });
  return data;
}

/** Confirma o código; em sucesso o número fica como contato validado do CPF. */
export async function confirmarTelefoneOtp(
  cpf: string,
  numero: string,
  codigo: string,
): Promise<TelefoneValidado> {
  const { data } = await http.post<TelefoneValidado>('/telefones/validacao/confirmar', {
    cpf,
    numero,
    codigo,
  });
  return data;
}
