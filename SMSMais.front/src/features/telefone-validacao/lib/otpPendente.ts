import { useEffect, useState } from 'react';

/**
 * Memória local do código de verificação JÁ ENVIADO e ainda não confirmado.
 *
 * A recepção manda o código, sai da tela para consultar outra coisa ou para pedir o número
 * ao paciente, e volta depois. Sem esta memória, reabrir o modal significaria disparar OUTRO
 * código — o anterior (que o paciente tem na mão) morreria. Guardamos no navegador porque a
 * pendência é do OPERADOR, não do paciente: só quem enviou precisa voltar ao campo do código.
 *
 * A pendência morre de três formas: o código é confirmado, o operador cancela explicitamente,
 * ou o prazo do próprio código (dado pelo backend) expira.
 */
export type OtpPendente = {
  numero: string;
  mascara: string | null;
  modoTeste: boolean;
  /** Epoch em ms — depois disso o código já não vale, e a pendência se apaga sozinha. */
  expiraEm: number;
};

/** Evento local: o localStorage só avisa OUTRAS abas, e a mudança precisa refletir nesta. */
const EVENTO = 'smsmarica:otp-telefone-mudou';

function chave(cpf: string): string {
  return `smsmarica:otp-telefone:${(cpf ?? '').replace(/\D/g, '')}`;
}

export function lerOtpPendente(cpf: string): OtpPendente | null {
  try {
    const cru = localStorage.getItem(chave(cpf));
    if (!cru) return null;
    const p = JSON.parse(cru) as OtpPendente;
    if (!p?.expiraEm || p.expiraEm <= Date.now()) {
      localStorage.removeItem(chave(cpf));
      return null;
    }
    return p;
  } catch {
    return null;
  }
}

export function salvarOtpPendente(cpf: string, pendente: OtpPendente): void {
  try {
    localStorage.setItem(chave(cpf), JSON.stringify(pendente));
    window.dispatchEvent(new CustomEvent(EVENTO));
  } catch {
    /* modo privado / storage cheio: seguimos sem a memória */
  }
}

export function limparOtpPendente(cpf: string): void {
  try {
    localStorage.removeItem(chave(cpf));
    window.dispatchEvent(new CustomEvent(EVENTO));
  } catch {
    /* idem */
  }
}

/** Pendência viva deste CPF, reativa a envio/confirmação/cancelamento (inclusive em outra aba). */
export function useOtpPendente(cpf: string): OtpPendente | null {
  const [pendente, setPendente] = useState<OtpPendente | null>(() => lerOtpPendente(cpf));

  useEffect(() => {
    const reler = () => setPendente(lerOtpPendente(cpf));
    reler();
    window.addEventListener(EVENTO, reler);
    window.addEventListener('storage', reler);
    // O código expira sozinho: sem este tick, o botão continuaria dizendo "aguardando" para
    // uma pendência que já morreu.
    const t = setInterval(reler, 30_000);
    return () => {
      window.removeEventListener(EVENTO, reler);
      window.removeEventListener('storage', reler);
      clearInterval(t);
    };
  }, [cpf]);

  return pendente;
}
