import { useState, type FormEvent } from 'react';
import { ArrowRight, Building2, Lock, Mail } from 'lucide-react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth, type UnidadeVinculada } from '@/shared/auth/authStore';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BrandLogo } from '@/shared/ui/BrandLogo';

type EstadoLocation = { de?: string };

export function LoginPage() {
  const entrar = useAuth((s) => s.entrar);
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  // Usuário com mais de uma unidade e nenhuma default: escolhe em qual entrar.
  const [unidadesEscolha, setUnidadesEscolha] = useState<UnidadeVinculada[] | null>(null);

  function irParaApp() {
    const estadoAuth = useAuth.getState();
    const estado = location.state as EstadoLocation | null;
    if (estadoAuth.usuario?.deveTrocarSenha) {
      navigate('/trocar-senha', { replace: true });
    } else {
      navigate(estado?.de ?? '/app', { replace: true });
    }
  }

  function aoEscolherUnidade(id: string) {
    useAuth.getState().definirUnidadeAtiva(id);
    irParaApp();
  }

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErro(null);
    setCarregando(true);
    try {
      await entrar({ email, senha });
      const { unidades, unidadeAtivaId } = useAuth.getState();
      if (unidades.length > 1 && !unidadeAtivaId) {
        setUnidadesEscolha(unidades);
        return;
      }
      irParaApp();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
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
          Secretaria Municipal de Saúde
        </p>
      </div>

      <div className="mt-8 w-full max-w-md mx-auto">
        <div className="card py-8 px-4 shadow-lg sm:px-10">
          {unidadesEscolha ? (
            <div className="space-y-4">
              <div>
                <h2 className="text-base font-semibold text-gray-900">Escolha a unidade</h2>
                <p className="mt-1 text-sm text-gray-500">
                  Você tem acesso a mais de uma unidade. Selecione em qual deseja entrar
                  (dá para trocar depois, no topo do sistema).
                </p>
              </div>
              {/* Mais de 3 unidades: lista rola em vez de esticar o card. */}
              <div className="max-h-44 space-y-2 overflow-y-auto pr-1">
                {unidadesEscolha.map((u) => (
                  <button
                    key={u.id}
                    type="button"
                    onClick={() => aoEscolherUnidade(u.id)}
                    className="w-full flex items-center gap-3 rounded-lg border border-gray-200 px-4 py-3 text-left text-sm font-medium text-gray-700 hover:border-primary-300 hover:bg-primary-50"
                  >
                    <Building2 className="w-5 h-5 text-gray-400 shrink-0" />
                    <span className="flex-1">{u.nome}</span>
                    <ArrowRight className="w-4 h-4 text-gray-400" />
                  </button>
                ))}
              </div>
              <button
                type="button"
                onClick={() => irParaApp()}
                className="w-full text-center text-sm text-gray-500 hover:text-gray-700 underline underline-offset-2"
              >
                Entrar vendo todas as minhas unidades
              </button>
            </div>
          ) : (
          <form className="space-y-6" onSubmit={aoEnviar}>
            {erro && (
              <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                {erro}
              </div>
            )}

            <div>
              <label htmlFor="email" className="label">
                Usuário, e-mail ou CPF
              </label>
              <div className="relative">
                <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <input
                  id="email"
                  type="text"
                  autoComplete="username"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="input pl-10"
                  placeholder="Usuário, e-mail ou CPF"
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
          )}
        </div>
      </div>
    </div>
  );
}
