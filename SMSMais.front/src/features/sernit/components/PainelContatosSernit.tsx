import { useAlterarContatosSernit, useContatosSernit } from '@/features/sernit/api/queries';
import { ModalLoginSernit } from '@/features/sernit/components/ModalLoginSernit';
import {
  SEM_SESSAO_SERNIT,
  temCodigo,
  useSessaoSernitObrigatoria,
} from '@/features/sernit/lib/sessaoSernit';
import { ModalContato } from '@/shared/regulacao/ModalContato';

/**
 * Alterar os telefones da solicitação no SERNIT — liga a sessão e os hooks do SERNIT ao modal
 * compartilhado {@link ModalContato} (o mesmo do SER). Toda a UI vive lá; aqui é só a fiação.
 */
export function PainelContatosSernit({ solicitacaoId }: { solicitacaoId: string }) {
  const { usuarioSernit, ...sessao } = useSessaoSernitObrigatoria();

  return (
    <ModalContato
      sistema="SERNIT"
      solicitacaoId={solicitacaoId}
      sessao={{ ...sessao, usuarioSistema: usuarioSernit }}
      ehFaltaDeSessao={(erro) => temCodigo(erro, SEM_SESSAO_SERNIT)}
      useContatos={useContatosSernit}
      useAlterar={useAlterarContatosSernit}
      ModalLogin={ModalLoginSernit}
    />
  );
}
