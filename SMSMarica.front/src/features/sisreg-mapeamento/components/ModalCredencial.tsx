import { useState } from 'react';
import { KeyRound, Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { useSalvarCredencial } from '@/features/sisreg-mapeamento/api/queries';
import type { SisregCredencialUnidade } from '@/features/sisreg-mapeamento/types';

type Props = {
  aberto: boolean;
  unidadeId: string | null;
  credencial: SisregCredencialUnidade | undefined;
  aoFechar: () => void;
};

/**
 * Troca do usuário/senha do SISREG da unidade.
 *
 * A senha nunca é lida de volta da API — este modal só envia. O backend autentica de verdade
 * no SISREG e confere se a unidade da sessão é a unidade selecionada antes de gravar; por isso
 * o erro retornado aqui é informativo (ex.: credencial de outra unidade).
 */
export function ModalCredencial({ aberto, unidadeId, credencial, aoFechar }: Props) {
  const salvar = useSalvarCredencial(unidadeId);
  const [usuario, setUsuario] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  function fechar() {
    setUsuario('');
    setSenha('');
    setErro(null);
    aoFechar();
  }

  async function confirmar() {
    setErro(null);
    try {
      await salvar.mutateAsync({ usuario: usuario.trim(), senha });
      fechar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const podeSalvar = usuario.trim().length > 0 && senha.length > 0 && !salvar.isPending;

  return (
    <Modal
      aberto={aberto}
      aoFechar={fechar}
      titulo={credencial?.usuario ? 'Trocar credencial do SISREG' : 'Cadastrar credencial do SISREG'}
    >
      <div className="space-y-4">
        <p className="text-sm text-gray-600">
          Usuário de coordenação do SISREG da unidade{' '}
          <strong>{credencial?.unidadeNome}</strong>
          {credencial?.unidadeCnes ? ` (CNES ${credencial.unidadeCnes})` : ''}. Ao salvar,
          autenticamos no SISREG e conferimos se a credencial pertence mesmo a esta unidade.
        </p>

        <div className="rounded-md bg-amber-50 border border-amber-200 p-3 text-sm text-amber-800">
          O SISREG aceita <strong>uma sessão por operador</strong>: ao validar aqui, a sessão que
          esse usuário tiver aberta no navegador é encerrada.
        </div>

        {/* O placeholder mostra só o FORMATO. Nunca o login de um operador real: ele fica visível
            para todo mundo que abre esta tela, em qualquer unidade. */}
        <Campo label="Usuário do SISREG" htmlFor="sisreg-usuario">
          <Input
            id="sisreg-usuario"
            value={usuario}
            onChange={(e) => setUsuario(e.target.value.toUpperCase())}
            placeholder="EX.: 000-USUARIO"
            autoFocus
          />
        </Campo>

        <Campo label="Senha" htmlFor="sisreg-senha">
          <Input
            id="sisreg-senha"
            type="password"
            value={senha}
            onChange={(e) => setSenha(e.target.value)}
            placeholder="Senha do SISREG"
          />
        </Campo>

        {erro && (
          <div className="rounded-md bg-red-50 border border-red-200 p-3 text-sm text-red-700">
            {erro}
          </div>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <Button variante="secundaria" onClick={fechar} disabled={salvar.isPending}>
            Cancelar
          </Button>
          <Button onClick={confirmar} disabled={!podeSalvar}>
            {salvar.isPending ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" /> Validando no SISREG…
              </>
            ) : (
              <>
                <KeyRound className="h-4 w-4" /> Validar e salvar
              </>
            )}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
