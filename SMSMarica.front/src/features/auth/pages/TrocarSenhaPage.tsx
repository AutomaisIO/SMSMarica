import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { KeyRound, Lock } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { BrandLogo } from '@/shared/ui/BrandLogo';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useAuth } from '@/shared/auth/authStore';
import { useAlterarMinhaSenha } from '@/features/usuarios/api/queries';

export function TrocarSenhaPage() {
  const usuario = useAuth((s) => s.usuario);
  const marcarSenhaTrocada = useAuth((s) => s.marcarSenhaTrocada);
  const sair = useAuth((s) => s.sair);
  const navigate = useNavigate();
  const trocar = useAlterarMinhaSenha();

  const [senhaAtual, setSenhaAtual] = useState('');
  const [senhaNova, setSenhaNova] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [erros, setErros] = useState<Partial<Record<'senhaAtual' | 'senhaNova' | 'confirmacao', string>>>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

    const ne: typeof erros = {};
    if (!senhaAtual) ne.senhaAtual = 'Informe a senha atual.';
    if (senhaNova.length < 8) ne.senhaNova = 'Mínimo 8 caracteres.';
    if (senhaNova !== confirmacao) ne.confirmacao = 'Não confere com a nova senha.';
    if (senhaNova && senhaAtual === senhaNova) ne.senhaNova = 'A nova senha deve ser diferente da atual.';
    if (Object.keys(ne).length > 0) {
      setErros(ne);
      return;
    }

    try {
      await trocar.mutateAsync({ senhaAtual, senhaNova });
      marcarSenhaTrocada();
      navigate('/app', { replace: true });
    } catch (e) {
      setErroGlobal(extrairMensagemDeErro(e));
    }
  }

  function aoCancelar() {
    sair();
    navigate('/login', { replace: true });
  }

  return (
    <div className="min-h-screen flex flex-col justify-center bg-gray-50 px-4 py-8 sm:px-6 lg:px-8">
      <div className="w-full max-w-md mx-auto">
        <div className="flex justify-center">
          <BrandLogo className="h-20 w-auto max-w-[300px]" />
        </div>
        <p className="mt-3 text-center text-sm text-gray-500">
          Troca de senha obrigatória
        </p>
      </div>

      <div className="mt-8 w-full max-w-md mx-auto">
        <div className="card py-8 px-4 shadow-lg sm:px-10">
          <div className="mb-5 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
            <strong>{usuario?.nome ?? 'Usuário'},</strong> defina uma nova senha pessoal antes de
            continuar. Você está usando uma senha temporária.
          </div>

          <form className="space-y-5" onSubmit={aoEnviar}>
            {erroGlobal ? (
              <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                {erroGlobal}
              </div>
            ) : null}

            <Campo label="Senha atual (temporária)" htmlFor="senhaAtual" erro={erros.senhaAtual} required>
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <Input
                  id="senhaAtual"
                  type="password"
                  autoComplete="current-password"
                  value={senhaAtual}
                  onChange={(e) => setSenhaAtual(e.target.value)}
                  className="pl-10"
                  required
                />
              </div>
            </Campo>

            <Campo label="Nova senha" htmlFor="senhaNova" erro={erros.senhaNova} required dica="Mínimo 8 caracteres.">
              <div className="relative">
                <KeyRound className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <Input
                  id="senhaNova"
                  type="password"
                  autoComplete="new-password"
                  value={senhaNova}
                  onChange={(e) => setSenhaNova(e.target.value)}
                  className="pl-10"
                  required
                />
              </div>
            </Campo>

            <Campo label="Confirmar nova senha" htmlFor="confirmacao" erro={erros.confirmacao} required>
              <div className="relative">
                <KeyRound className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <Input
                  id="confirmacao"
                  type="password"
                  autoComplete="new-password"
                  value={confirmacao}
                  onChange={(e) => setConfirmacao(e.target.value)}
                  className="pl-10"
                  required
                />
              </div>
            </Campo>

            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:items-center sm:justify-between">
              <Button type="button" variante="ghost" onClick={aoCancelar} disabled={trocar.isPending}>
                Sair
              </Button>
              <Button type="submit" disabled={trocar.isPending}>
                {trocar.isPending ? 'Trocando…' : 'Trocar senha e continuar'}
              </Button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
