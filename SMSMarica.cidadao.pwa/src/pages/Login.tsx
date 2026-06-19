import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { http, extrairMensagemDeErro } from '@/lib/httpClient';
import { Botao, Tela } from '@/components/Tela';

function mascararCpf(valor: string): string {
  const d = valor.replace(/\D/g, '').slice(0, 11);
  return d
    .replace(/(\d{3})(\d)/, '$1.$2')
    .replace(/(\d{3})(\d)/, '$1.$2')
    .replace(/(\d{3})(\d{1,2})$/, '$1-$2');
}

export function Login() {
  const navigate = useNavigate();
  const [cpf, setCpf] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const cpfLimpo = cpf.replace(/\D/g, '');
  const valido = cpfLimpo.length === 11;

  async function solicitar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setEnviando(true);
    try {
      // TODO(FT7): endpoint a implementar no backend — envia OTP por WhatsApp.
      await http.post('/auth/paciente/solicitar-otp', { cpf: cpfLimpo });
      navigate('/login/codigo', { state: { cpf: cpfLimpo } });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Tela titulo="Entrar">
      <div className="text-center mb-6">
        <p className="text-neutral-600">
          Informe seu CPF. Enviaremos um código de acesso pelo WhatsApp do número cadastrado.
        </p>
      </div>
      <form onSubmit={solicitar} className="space-y-4">
        <label className="block">
          <span className="text-sm font-medium text-neutral-700">CPF</span>
          <input
            inputMode="numeric"
            autoComplete="off"
            value={cpf}
            onChange={(e) => setCpf(mascararCpf(e.target.value))}
            placeholder="000.000.000-00"
            className="mt-1 w-full rounded-xl border border-neutral-300 px-4 py-3 text-lg tracking-wide focus:border-marica focus:outline-none"
          />
        </label>
        {erro && <p className="text-sm text-marica">{erro}</p>}
        <Botao type="submit" disabled={!valido || enviando}>
          {enviando ? 'Enviando…' : 'Receber código'}
        </Botao>
      </form>
    </Tela>
  );
}
