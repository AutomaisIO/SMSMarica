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

/** Dispara o envio do código de validação por WhatsApp para o número. */
export async function enviarTelefoneOtp(numero: string): Promise<TelefoneOtpEmitido> {
  const { data } = await http.post<TelefoneOtpEmitido>('/telefones/validacao/enviar', { numero });
  return data;
}

/** Confirma o código; em caso de sucesso o número fica registrado como validado. */
export async function confirmarTelefoneOtp(numero: string, codigo: string): Promise<TelefoneValidado> {
  const { data } = await http.post<TelefoneValidado>('/telefones/validacao/confirmar', {
    numero,
    codigo,
  });
  return data;
}

/** Situação de validação de um número (para exibir o selo). */
export async function obterTelefoneValidado(numero: string): Promise<TelefoneValidado> {
  const { data } = await http.get<TelefoneValidado>('/telefones/validacao', { params: { numero } });
  return data;
}
