import { useState, type FormEvent } from 'react';
import { ArrowRight, Lock, Mail, ShieldCheck } from 'lucide-react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth, type Perfil } from '@/shared/auth/authStore';
import { BrandLogo } from '@/shared/ui/BrandLogo';

type EstadoLocation = { de?: string };

export function LoginPage() {
  const entrar = useAuth((s) => s.entrar);
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('operador@marica.rj.gov.br');
  const [senha, setSenha] = useState('operador');
  const [perfil, setPerfil] = useState<Perfil>('operador');
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErro(null);
    setCarregando(true);
    try {
      await entrar({ email, senha, perfil });
      const estado = location.state as EstadoLocation | null;
      const destino = estado?.de ?? (perfil === 'operador' ? '/operador' : '/gestor');
      navigate(destino, { replace: true });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao entrar.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div className="min-h-screen flex flex-col justify-center bg-gray-50 px-4 py-8 sm:px-6 lg:px-8">
      <div className="w-full max-w-md mx-auto">
        <div className="flex justify-center">
          <BrandLogo className="h-20 w-auto max-w-[300px]" />
        </div>
        <p className="mt-3 text-center text-sm text-gray-500">
          Secretaria Municipal de Saúde · Painel administrativo
        </p>
      </div>

      <div className="mt-8 w-full max-w-md mx-auto">
        <div className="card py-8 px-4 shadow-lg sm:px-10">
          <form className="space-y-6" onSubmit={aoEnviar}>
            {erro && (
              <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                {erro}
              </div>
            )}

            <div>
              <label htmlFor="email" className="label">
                E-mail
              </label>
              <div className="relative">
                <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <input
                  id="email"
                  type="email"
                  autoComplete="username"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="input pl-10"
                  placeholder="seu@marica.rj.gov.br"
                  disabled={carregando}
                />
              </div>
            </div>

            <div>
              <label htmlFor="senha" className="label">
                Senha
              </label>
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <input
                  id="senha"
                  type="password"
                  autoComplete="current-password"
                  required
                  value={senha}
                  onChange={(e) => setSenha(e.target.value)}
                  className="input pl-10"
                  placeholder="Sua senha"
                  disabled={carregando}
                />
              </div>
            </div>

            <div>
              <label htmlFor="perfil" className="label">
                Entrar como
              </label>
              <div className="relative">
                <ShieldCheck className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <select
                  id="perfil"
                  value={perfil}
                  onChange={(e) => setPerfil(e.target.value as Perfil)}
                  className="input pl-10"
                  disabled={carregando}
                >
                  <option value="operador">Operador</option>
                  <option value="gestor">Gestor</option>
                </select>
              </div>
              <p className="mt-2 text-xs text-gray-500">
                Perfil simulado. Entrará em vigor real quando o módulo Identidade (S2.4) for publicado.
              </p>
            </div>

            <button type="submit" disabled={carregando} className="w-full btn btn-primary btn-lg">
              {carregando ? (
                <span>Entrando…</span>
              ) : (
                <>
                  Entrar
                  <ArrowRight className="w-5 h-5" />
                </>
              )}
            </button>
          </form>
        </div>
      </div>

      <div className="mt-6 w-full max-w-md mx-auto text-center">
        <p className="text-xs text-gray-400">
          SMS Maricá &middot; ambiente de desenvolvimento
        </p>
      </div>
    </div>
  );
}
