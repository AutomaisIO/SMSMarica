import { useRegistrarFollowUpSer } from '@/features/ser/api/queries';
import { ModalLoginSer } from '@/features/ser/components/ModalLoginSer';
import { useSessaoSerObrigatoria } from '@/features/ser/lib/sessaoSer';
import { ModalFollowUp } from '@/shared/regulacao/ModalFollowUp';

/**
 * FollowUP na solicitação do SER — liga a sessão e a mutation do SER ao modal compartilhado
 * {@link ModalFollowUp} (o mesmo do SERNIT). Toda a UI vive lá; aqui fica só a fiação da feature.
 */
export function PainelFollowUpSer({ solicitacaoId }: { solicitacaoId: string }) {
  const { usuarioSer, ...sessao } = useSessaoSerObrigatoria();
  const registrar = useRegistrarFollowUpSer(solicitacaoId);

  return (
    <ModalFollowUp
      sistema="SER"
      sessao={{ ...sessao, usuarioSistema: usuarioSer }}
      registrar={registrar}
      ModalLogin={ModalLoginSer}
    />
  );
}
