import { useAlterarContatosSer, useContatosSer } from '@/features/ser/api/queries';
import { ModalLoginSer } from '@/features/ser/components/ModalLoginSer';
import { SEM_SESSAO_SER, temCodigo, useSessaoSerObrigatoria } from '@/features/ser/lib/sessaoSer';
import { ModalContato } from '@/shared/regulacao/ModalContato';

/**
 * Alterar os telefones da solicitação no SER — liga a sessão e os hooks do SER ao modal
 * compartilhado {@link ModalContato} (o mesmo do SERNIT). Toda a UI vive lá; aqui é só a fiação.
 */
export function PainelContatosSer({ solicitacaoId }: { solicitacaoId: string }) {
  const { usuarioSer, ...sessao } = useSessaoSerObrigatoria();

  return (
    <ModalContato
      sistema="SER"
      solicitacaoId={solicitacaoId}
      sessao={{ ...sessao, usuarioSistema: usuarioSer }}
      ehFaltaDeSessao={(erro) => temCodigo(erro, SEM_SESSAO_SER)}
      useContatos={useContatosSer}
      useAlterar={useAlterarContatosSer}
      ModalLogin={ModalLoginSer}
    />
  );
}
