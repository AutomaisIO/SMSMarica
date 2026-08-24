import { useState } from 'react';
import { Copy, KeyRound, ShieldAlert, Wand2 } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAlterarSenhaDoUsuario,
  useGerarNovaSenhaDoUsuario,
} from '@/features/usuarios/api/queries';

type Props = {
  usuarioId: string;
  deveTrocarAtual: boolean;
};

/**
 * Seção compartilhada para gestão de senha de um usuário em modo edição.
 * Encapsula alteração manual de senha e geração de senha aleatória.
 * Reutilizada nos formulários de Usuário, Médico e Motorista.
 */
export function SegurancaSecao({ usuarioId, deveTrocarAtual }: Props) {
  const alterarSenha = useAlterarSenhaDoUsuario();
  const gerarSenha = useGerarNovaSenhaDoUsuario();

  const [senhaManual, setSenhaManual] = useState('');
  const [exigirTroca, setExigirTroca] = useState(true);
  const [erroSenha, setErroSenha] = useState<string | null>(null);
  const [senhaGerada, setSenhaGerada] = useState<string | null>(null);
  const [senhaCopiada, setSenhaCopiada] = useState(false);

  async function aplicarSenhaManual() {
    setErroSenha(null);
    if (senhaManual.length < 8) {
      setErroSenha('Senha deve ter pelo menos 8 caracteres.');
      return;
    }
    try {
      await alterarSenha.mutateAsync({
        id: usuarioId,
        senhaNova: senhaManual,
        deveTrocarNoProximoLogin: exigirTroca,
      });
      setSenhaManual('');
    } catch (e) {
      setErroSenha(extrairMensagemDeErro(e));
    }
  }

  async function gerarNovaSenha() {
    setErroSenha(null);
    setSenhaCopiada(false);
    try {
      const r = await gerarSenha.mutateAsync(usuarioId);
      setSenhaGerada(r.senhaGerada);
    } catch (e) {
      setErroSenha(extrairMensagemDeErro(e));
    }
  }

  async function copiarSenha() {
    if (!senhaGerada) return;
    try {
      await navigator.clipboard.writeText(senhaGerada);
      setSenhaCopiada(true);
      setTimeout(() => setSenhaCopiada(false), 2000);
    } catch {
      // ignore
    }
  }

  return (
    <>
      <section className="rounded-lg border border-gray-200 bg-gray-50 p-4">
        <h3 className="mb-1 flex items-center gap-2 text-sm font-semibold text-gray-900">
          <KeyRound className="h-4 w-4" /> Segurança
        </h3>
        <p className="mb-3 text-xs text-gray-500">
          Defina uma nova senha manualmente ou gere uma aleatória para o usuário. Em ambos os
          casos ele pode ser forçado a trocar no próximo login.
        </p>

        {deveTrocarAtual ? (
          <div className="mb-3 flex items-center gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
            <ShieldAlert className="h-4 w-4 flex-shrink-0" />
            Este usuário já está marcado para trocar a senha no próximo login.
          </div>
        ) : null}

        {erroSenha ? (
          <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erroSenha}
          </div>
        ) : null}

        <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
          <Campo
            label="Nova senha (manual)"
            htmlFor="senhaManual"
            className="md:col-span-2"
            dica="Mínimo 8 caracteres."
          >
            <Input
              id="senhaManual"
              type="password"
              value={senhaManual}
              onChange={(e) => setSenhaManual(e.target.value)}
              autoComplete="new-password"
              disabled={alterarSenha.isPending}
            />
          </Campo>
          <div className="flex items-end">
            <Button
              type="button"
              variante="outline"
              onClick={aplicarSenhaManual}
              disabled={alterarSenha.isPending || !senhaManual}
              className="w-full"
            >
              {alterarSenha.isPending ? 'Aplicando…' : 'Aplicar nova senha'}
            </Button>
          </div>
        </div>

        <label className="mt-3 flex items-center gap-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={exigirTroca}
            onChange={(e) => setExigirTroca(e.target.checked)}
            disabled={alterarSenha.isPending}
          />
          <span>Exigir que o usuário troque a senha no próximo login</span>
        </label>

        <div className="mt-4 border-t border-gray-200 pt-3">
          <Button
            type="button"
            variante="secundaria"
            onClick={gerarNovaSenha}
            disabled={gerarSenha.isPending}
          >
            <Wand2 className="h-4 w-4" />
            {gerarSenha.isPending ? 'Gerando…' : 'Gerar nova senha aleatória'}
          </Button>
          <p className="mt-2 text-xs text-gray-500">
            A senha gerada será exibida uma única vez. O usuário sempre será obrigado a trocá-la
            no próximo login.
          </p>
        </div>
      </section>

      <Modal
        aberto={senhaGerada !== null}
        aoFechar={() => setSenhaGerada(null)}
        titulo="Senha gerada"
        descricao="Esta senha será exibida apenas uma vez. Anote ou copie agora e entregue ao usuário."
        largura="sm"
      >
        <div className="space-y-4">
          <div className="rounded-md border-2 border-dashed border-primary-200 bg-primary-50 px-4 py-3 text-center">
            <code className="select-all font-mono text-lg font-semibold tracking-wide text-primary-800">
              {senhaGerada}
            </code>
          </div>
          <p className="rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-800">
            O usuário será obrigado a trocar esta senha no próximo login.
          </p>
          <div className="flex items-center justify-end gap-2">
            <Button type="button" variante="outline" onClick={copiarSenha}>
              <Copy className="h-4 w-4" />
              {senhaCopiada ? 'Copiada!' : 'Copiar'}
            </Button>
            <Button type="button" onClick={() => setSenhaGerada(null)}>
              Pronto
            </Button>
          </div>
        </div>
      </Modal>
    </>
  );
}
