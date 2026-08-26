import { useRegistrarFollowUpSernit } from '@/features/sernit/api/queries';
import { ModalLoginSernit } from '@/features/sernit/components/ModalLoginSernit';
import { useSessaoSernitObrigatoria } from '@/features/sernit/lib/sessaoSernit';
import { ModalFollowUp } from '@/shared/regulacao/ModalFollowUp';

/**
 * FollowUP na solicitação do SERNIT — liga a sessão e a mutation do SERNIT ao modal compartilhado
 * {@link ModalFollowUp} (o mesmo do SER). Toda a UI vive lá; aqui fica só a fiação da feature.
 */
export function PainelFollowUpSernit({ solicitacaoId }: { solicitacaoId: string }) {
  const { usuarioSernit, ...sessao } = useSessaoSernitObrigatoria();
  const registrar = useRegistrarFollowUpSernit(solicitacaoId);

  return (
    <ModalFollowUp
      sistema="SERNIT"
      sessao={{ ...sessao, usuarioSistema: usuarioSernit }}
      registrar={registrar}
      ModalLogin={ModalLoginSernit}
    />
  );
}
