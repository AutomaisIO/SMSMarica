import { useEffect, useState } from 'react';
import { AlertTriangle, KeyRound, Loader2, ShieldCheck } from 'lucide-react';

import { useEntrarNoSer } from '@/features/ser/api/queries';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';

/**
 * Entrada no SER com a credencial do PRÓPRIO operador.
 *
 * <p>Por que não basta a credencial já cadastrada em Regulação → Configuração: aquela é de
 * <b>sincronismo</b> — serve para a varredura ler a fila. O SER carimba cada evento com o nome de
 * quem fez (medido em 18/08/2026), então escrever com ela faria toda ação do município aparecer
 * assinada pela mesma pessoa na trilha de auditoria do Estado.</p>
 *
 * <p>A senha vai para a memória do servidor amarrada à sessão do usuário e <b>não é gravada em
 * banco</b>. Sair do sistema derruba também a sessão do SER.</p>
 *
 * <p><b>O OK valida na hora.</b> O backend tenta logar no SER de verdade antes de guardar
 * qualquer coisa: se a credencial não abrir o módulo Ambulatório, o modal continua aberto com o
 * motivo. Guardar sem provar empurraria a falha para o meio de um FollowUP, onde o operador não
 * saberia dizer se o que falhou foi a senha ou o registro.</p>
 */
export function ModalLoginSer({
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
  const [okComo, setOkComo] = useState<string | null>(null);

  const entrar = useEntrarNoSer();

  // Reabrir o modal (nova tentativa, outra ação) não pode herdar o erro nem a senha da vez
  // anterior pendurados na tela.
  useEffect(() => {
    if (aberto) {
      setSenha('');
      setErro(null);
      setOkComo(null);
    }
  }, [aberto]);

  function fechar() {
    setSenha('');
    setErro(null);
    aoFechar();
  }

  async function aoEnviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    try {
      const info = await entrar.mutateAsync({ usuario: usuario.trim(), senha });
      setSenha('');
      // Confirma antes de sumir: o operador precisa ver COM QUAL usuário ficou autenticado,
      // porque é esse nome que vai para o histórico da solicitação no SER.
      setOkComo(info.usuarioSer ?? usuario.trim());
      window.setTimeout(() => aoAutenticar?.(), 900);
    } catch (err) {
      // Fica aberto com o motivo — é aqui que "senha errada" tem de ser dita.
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <Modal
      aberto={aberto}
      aoFechar={fechar}
      largura="sm"
      titulo="Entrar no SER"
      descricao="Suas ações no SER são assinadas com o SEU usuário do Estado."
    >
      {okComo ? (
        <div className="space-y-3">
          <p className="flex items-center gap-2 rounded border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900">
            <ShieldCheck className="size-5 shrink-0" />
            <span>
              Autenticado no SER como <strong>{okComo}</strong>.
            </span>
          </p>
          <p className="text-xs text-slate-500">
            Continuando de onde você parou…
          </p>
        </div>
      ) : (
        <form onSubmit={aoEnviar} className="space-y-4">
          <p className="flex gap-2 rounded border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900">
            <ShieldCheck className="mt-0.5 size-4 shrink-0" />
            <span>
              A credencial cadastrada no sistema serve só para <strong>ler</strong> a fila. Para
              escrever no SER é preciso o seu login — é o nome dele que fica no histórico da
              solicitação. A senha <strong>não é gravada</strong>: fica na sua sessão e some ao
              sair.
            </span>
          </p>

          <label className="block space-y-1">
            <span className="text-sm text-slate-700">Usuário do SER</span>
            <Input
              value={usuario}
              onChange={(e) => setUsuario(e.target.value)}
              autoComplete="username"
              required
              autoFocus
            />
          </label>

          <label className="block space-y-1">
            <span className="text-sm text-slate-700">Senha do SER</span>
            <Input
              type="password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              autoComplete="current-password"
              required
            />
          </label>

          {erro && (
            <p className="flex gap-2 rounded border border-red-200 bg-red-50 p-2 text-xs text-red-800">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" />
              <span>{erro}</span>
            </p>
          )}

          <div className="flex justify-end gap-2 pt-1">
            <Button variante="ghost" onClick={fechar} type="button" disabled={entrar.isPending}>
              Cancelar
            </Button>
            <Button type="submit" disabled={entrar.isPending}>
              {entrar.isPending ? (
                <Loader2 className="mr-1 size-4 animate-spin" />
              ) : (
                <KeyRound className="mr-1 size-4" />
              )}
              Entrar
            </Button>
          </div>
        </form>
      )}
    </Modal>
  );
}
