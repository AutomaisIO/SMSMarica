import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { api } from '@/lib/api';
import { cpfValido, digitosCpf, mascararCpf } from '@/lib/cpf';
import { mensagemDaEntrada } from '@/lib/entrada';
import { AuthShell } from '@/components/AuthShell';
import { PrimaryButton } from '@/components/ui';

/** dd/mm/aaaa enquanto digita. */
function mascararData(valor: string): string {
  const d = valor.replace(/\D/g, '').slice(0, 8);
  if (d.length <= 2) return d;
  if (d.length <= 4) return `${d.slice(0, 2)}/${d.slice(2)}`;
  return `${d.slice(0, 2)}/${d.slice(2, 4)}/${d.slice(4)}`;
}

/** dd/mm/aaaa → aaaa-mm-dd; null se a data não existe, está no futuro ou é anterior a 1900. */
function paraIso(valor: string): string | null {
  const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(valor);
  if (!m) return null;
  const [, dia, mes, ano] = m;
  const d = new Date(Number(ano), Number(mes) - 1, Number(dia));
  if (
    d.getFullYear() !== Number(ano) ||
    d.getMonth() !== Number(mes) - 1 ||
    d.getDate() !== Number(dia) ||
    d > new Date() ||
    Number(ano) < 1900
  ) {
    return null;
  }
  return `${ano}-${mes}-${dia}`;
}

const ROTULO =
  'mb-2 block text-center text-[11px] font-semibold uppercase tracking-[0.22em] text-tinta-mute';
const CAMPO =
  'w-full rounded-2xl border border-areia bg-white py-4 text-center font-display text-[26px] font-semibold tabular-nums tracking-[0.06em] text-tinta shadow-carta transition placeholder:font-normal placeholder:text-tinta-mute/35 focus:border-lagoa focus:outline-none focus:ring-4 focus:ring-lagoa/15';

/**
 * Passo 1 do login (ADR-0078): CPF e data de nascimento, e nada mais. O servidor confere o par (na
 * ficha ou, sem cadastro, na Receita) e devolve os números que já conhece, só com o final — a
 * pessoa escolhe o seu no passo seguinte. Errou, a resposta é sempre a mesma ("não conseguimos
 * confirmar os seus dados"), para não ajudar quem tenta adivinhar.
 */
export function Login() {
  const navigate = useNavigate();
  const location = useLocation();
  // Volta do passo seguinte ("Voltar" ou o tempo acabou): o CPF já vem preenchido e o aviso aparece.
  const estado = location.state as { cpf?: string; aviso?: string } | null;
  const [cpf, setCpf] = useState(() => mascararCpf(estado?.cpf ?? ''));
  const [nascimento, setNascimento] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(estado?.aviso ?? null);

  const cpfLimpo = digitosCpf(cpf);
  const cpfErrado = cpfLimpo.length === 11 && !cpfValido(cpfLimpo);
  const iso = paraIso(nascimento);
  const dataErrada = nascimento.length === 10 && !iso;
  const valido = cpfLimpo.length === 11 && !cpfErrado && !!iso;

  async function continuar(e: React.FormEvent) {
    e.preventDefault();
    if (!valido || !iso) return;
    setErro(null);
    setEnviando(true);
    try {
      const entrada = await api.entrada(cpfLimpo, iso);
      navigate('/login/telefone', { state: { cpf: cpfLimpo, entrada } });
    } catch (e) {
      setErro(mensagemDaEntrada(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <AuthShell
      titulo="Entrar"
      subtitulo="Informe seu CPF e sua data de nascimento. Enviaremos um código de acesso pelo WhatsApp do número cadastrado na Saúde."
    >
      <form onSubmit={continuar} className="space-y-5">
        <div>
          <label htmlFor="cpf" className={ROTULO}>
            Seu CPF
          </label>
          <input
            id="cpf"
            inputMode="numeric"
            autoComplete="off"
            autoFocus={!estado?.cpf}
            placeholder="000.000.000-00"
            value={cpf}
            onChange={(e) => setCpf(mascararCpf(e.target.value))}
            className={CAMPO}
          />
          {cpfErrado && (
            <p className="mt-2 text-center text-xs text-marica">CPF inválido. Confira os números.</p>
          )}
        </div>
        <div>
          <label htmlFor="nascimento" className={ROTULO}>
            Data de nascimento
          </label>
          <input
            id="nascimento"
            inputMode="numeric"
            autoComplete="bday"
            autoFocus={!!estado?.cpf}
            placeholder="dd/mm/aaaa"
            value={nascimento}
            onChange={(e) => setNascimento(mascararData(e.target.value))}
            className={CAMPO}
          />
          {dataErrada && (
            <p className="mt-2 text-center text-xs text-marica">
              Data inválida. Confira o dia, o mês e o ano.
            </p>
          )}
        </div>
        {erro && <p className="text-center text-sm text-marica">{erro}</p>}
        <PrimaryButton type="submit" disabled={!valido} carregando={enviando}>
          Continuar
        </PrimaryButton>
        <p className="mx-auto max-w-[17rem] text-center text-xs leading-relaxed text-tinta-mute">
          É a sua primeira vez? Entre do mesmo jeito — procuramos o seu WhatsApp no cadastro da
          Saúde.
        </p>
      </form>
    </AuthShell>
  );
}
