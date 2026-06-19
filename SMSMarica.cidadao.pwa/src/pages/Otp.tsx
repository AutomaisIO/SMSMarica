import { useState } from 'react';
import { useLocation, useNavigate, Navigate } from 'react-router-dom';
import { http, extrairMensagemDeErro } from '@/lib/httpClient';
import { useAuth, type PacienteSessao } from '@/store/auth';
import { Botao, Tela } from '@/components/Tela';

type RespostaLogin = { token: string; paciente: PacienteSessao };

export function Otp() {
  const navigate = useNavigate();
  const location = useLocation();
  const entrar = useAuth((s) => s.entrar);
  const cpf = (location.state as { cpf?: string } | null)?.cpf;

  const [codigo, setCodigo] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  if (!cpf) return <Navigate to="/login" replace />;

  const valido = codigo.replace(/\D/g, '').length >= 4;

  async function validar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setEnviando(true);
    try {
      // TODO(FT7): endpoint a implementar no backend — valida o OTP e devolve o token.
      const { data } = await http.post<RespostaLogin>('/auth/paciente/validar-otp', {
        cpf,
        codigo: codigo.replace(/\D/g, ''),
      });
      entrar(data.token, data.paciente);
      navigate('/', { replace: true });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Tela titulo="Código de acesso">
      <p className="text-neutral-600 mb-6 text-center">
        Digite o código que enviamos pelo WhatsApp.
      </p>
      <form onSubmit={validar} className="space-y-4">
        <input
          inputMode="numeric"
          autoComplete="one-time-code"
          value={codigo}
          onChange={(e) => setCodigo(e.target.value.replace(/\D/g, '').slice(0, 6))}
          placeholder="••••••"
          className="w-full rounded-xl border border-neutral-300 px-4 py-3 text-center text-2xl tracking-[0.5em] focus:border-marica focus:outline-none"
        />
        {erro && <p className="text-sm text-marica">{erro}</p>}
        <Botao type="submit" disabled={!valido || enviando}>
          {enviando ? 'Validando…' : 'Entrar'}
        </Botao>
        <button
          type="button"
          onClick={() => navigate('/login')}
          className="w-full text-sm text-neutral-500 underline"
        >
          Trocar CPF
        </button>
      </form>
    </Tela>
  );
}
