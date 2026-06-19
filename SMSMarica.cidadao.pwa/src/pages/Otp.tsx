import { useState } from 'react';
import { useLocation, useNavigate, Navigate } from 'react-router-dom';
import { http, extrairMensagemDeErro } from '@/lib/httpClient';
import { useAuth, type PacienteSessao } from '@/store/auth';
import { AuthShell } from '@/components/AuthShell';
import { PrimaryButton } from '@/components/ui';

type RespostaLogin = { token: string; paciente: PacienteSessao };

export function Otp() {
  const navigate = useNavigate();
  const location = useLocation();
  const entrar = useAuth((s) => s.entrar);
  const estado = location.state as { cpf?: string; codigoTeste?: string } | null;
  const cpf = estado?.cpf;
  const codigoTeste = estado?.codigoTeste;

  const [codigo, setCodigo] = useState(codigoTeste ?? '');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  if (!cpf) return <Navigate to="/login" replace />;

  const valido = codigo.replace(/\D/g, '').length >= 4;

  async function validar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setEnviando(true);
    try {
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
    <AuthShell titulo="Código de acesso" subtitulo="Digite o código que enviamos pelo WhatsApp.">
      {codigoTeste && (
        <div className="mb-5 rounded-2xl border border-amber-300 bg-amber-50 p-4 text-center text-sm text-amber-800">
          Modo de teste — envio por WhatsApp ainda não ativo.
          <br />
          Seu código: <span className="font-mono text-xl font-bold tracking-[0.3em]">{codigoTeste}</span>
        </div>
      )}
      <form onSubmit={validar} className="space-y-5">
        <input
          inputMode="numeric"
          autoComplete="one-time-code"
          aria-label="Código de acesso"
          value={codigo}
          onChange={(e) => setCodigo(e.target.value.replace(/\D/g, '').slice(0, 6))}
          placeholder="······"
          className="w-full rounded-2xl border border-areia bg-white py-4 text-center font-mono text-3xl tracking-[0.5em] text-tinta focus:border-lagoa focus:outline-none"
        />
        {erro && <p className="text-sm text-marica">{erro}</p>}
        <PrimaryButton type="submit" disabled={!valido} carregando={enviando}>
          Entrar
        </PrimaryButton>
        <button
          type="button"
          onClick={() => navigate('/login')}
          className="block w-full text-center text-sm font-medium text-tinta-mute underline"
        >
          Trocar CPF
        </button>
      </form>
    </AuthShell>
  );
}
