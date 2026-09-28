import { useEffect, useState } from 'react';
import { KeyRound, Loader2, ShieldCheck } from 'lucide-react';
import { useEntrarNoSiscan } from '@/features/anamnese/api/siscanApi';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';

/**
 * Entrada no SISCAN com a credencial do PRÓPRIO operador.
 *
 * <p><b>Por que não existe uma credencial do sistema aqui.</b> Diferente do SISREG e do SER, o
 * SISCAN nunca teve credencial de sincronismo — e não vai ter. A requisição que nasce lá leva um
 * responsável e fica carimbada com quem operou, numa base federal de rastreamento de câncer. Quem
 * gera assina com o próprio login.</p>
 *
 * <p><b>A senha não é gravada.</b> Vai para a memória do servidor amarrada à sessão de quem
 * entrou e morre com ela — sair do sistema derruba a sessão do SISCAN junto.</p>
 *
 * <p><b>O OK valida na hora.</b> O backend entra no SISCAN de verdade antes de guardar qualquer
 * coisa: credencial errada mantém o modal aberto com o motivo. Guardar sem provar empurraria a
 * falha para o meio da geração, onde ninguém sabe dizer se o que falhou foi a senha ou a
 * requisição.</p>
 */
export function ModalLoginSiscan({
  aberto,
  aoFechar,
  aoAutenticar,
}: {
  aberto: boolean;
  aoFechar: () => void;
  /** Chamado depois do login OK — é onde a tela retoma a ação que estava pendente. */
  aoAutenticar?: () => void;
}) {
  const [usuario, setUsuario] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  const entrar = useEntrarNoSiscan();

  // Reabrir não pode herdar a senha nem o erro da tentativa anterior pendurados na tela.
  useEffect(() => {
    if (aberto) {
      setSenha('');
      setErro(null);
    }
  }, [aberto]);

  async function aoEnviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    try {
      await entrar.mutateAsync({ usuario: usuario.trim(), senha });
      setSenha('');
      aoAutenticar?.();
      aoFechar();
    } catch (falha) {
      setErro(extrairMensagemDeErro(falha));
    }
  }

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo="Entrar no SISCAN" largura="sm">
      {/* Computador compartilhado na recepção (#137): o navegador não deve guardar nem oferecer
          o usuário e a senha do SISCAN de uma pessoa para a próxima. */}
      <form onSubmit={aoEnviar} autoComplete="off" className="space-y-4">
        <div className="rounded-md bg-teal-50 px-3 py-2 text-xs text-teal-900">
          <p className="flex items-start gap-2">
            <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              Use o <strong>seu</strong> e-mail e senha do SISCAN. A requisição fica registrada lá
              com quem a criou — por isso não existe um login do sistema para isso.
            </span>
          </p>
        </div>

        <label className="block text-sm font-medium text-gray-700">
          E-mail do SISCAN
          <Input
            type="email"
            autoComplete="off"
            value={usuario}
            onChange={(e) => setUsuario(e.target.value)}
            placeholder="seu.email@exemplo.gov.br"
            required
            autoFocus
            className="mt-1"
          />
        </label>

        <label className="block text-sm font-medium text-gray-700">
          Senha
          <Input
            type="password"
            autoComplete="new-password"
            value={senha}
            onChange={(e) => setSenha(e.target.value)}
            required
            className="mt-1"
          />
        </label>

        <p className="text-xs text-gray-500">
          A senha não é guardada. Ela vale enquanto durar a sua sessão neste sistema e some quando
          você sair.
        </p>

        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </div>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button type="button" variante="secundaria" onClick={aoFechar}>
            Cancelar
          </Button>
          <Button type="submit" disabled={entrar.isPending}>
            {entrar.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <KeyRound className="mr-2 h-4 w-4" />
            )}
            Entrar
          </Button>
        </div>
      </form>
    </Modal>
  );
}
