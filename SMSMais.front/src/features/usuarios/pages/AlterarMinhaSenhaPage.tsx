import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { KeyRound, Lock } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { useAlterarMinhaSenha } from '@/features/usuarios/api/queries';

export function AlterarMinhaSenhaPage() {
  const navigate = useNavigate();
  const trocar = useAlterarMinhaSenha();
  const [senhaAtual, setSenhaAtual] = useState('');
  const [senhaNova, setSenhaNova] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [erros, setErros] = useState<Partial<Record<'senhaAtual' | 'senhaNova' | 'confirmacao', string>>>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const [sucesso, setSucesso] = useState(false);

  async function aoEnviar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);
    setSucesso(false);

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
      setSucesso(true);
      setSenhaAtual('');
      setSenhaNova('');
      setConfirmacao('');
    } catch (e) {
      setErroGlobal(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="max-w-md space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">Alterar minha senha</h1>
        <p className="mt-1 text-sm text-gray-600">
          Defina uma nova senha pessoal. Você precisará informar a senha atual para confirmar.
        </p>
      </header>

      <form onSubmit={aoEnviar} className="card space-y-5 p-5">
        <Campo label="Senha atual" htmlFor="senhaAtual" erro={erros.senhaAtual} required>
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

        {erroGlobal ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erroGlobal}
          </div>
        ) : null}
        {sucesso ? (
          <div className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
            Senha alterada com sucesso.
          </div>
        ) : null}

        <div className="flex items-center justify-between">
          <Button type="button" variante="ghost" onClick={() => navigate('/app')}>
            Voltar
          </Button>
          <Button type="submit" disabled={trocar.isPending}>
            {trocar.isPending ? 'Alterando…' : 'Alterar senha'}
          </Button>
        </div>
      </form>
    </div>
  );
}
