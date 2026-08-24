import { Modal } from '@/shared/ui/Modal';
import { ListaSessoesElegiveis } from '@/features/translados/components/ListaSessoesElegiveis';
import type { SessaoElegivel } from '@/features/translados/types';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  sessoes: SessaoElegivel[];
  dataRota: string;
  carregando?: boolean;
  aoEscolher: (s: SessaoElegivel) => void;
  tituloAssento?: string;
};

export function ModalEscolherSessao({
  aberto,
  aoFechar,
  sessoes,
  dataRota,
  carregando,
  aoEscolher,
  tituloAssento,
}: Props) {
  return (
    <Modal
      aberto={aberto}
      aoFechar={aoFechar}
      titulo={tituloAssento ? `Alocar paciente no ${tituloAssento}` : 'Alocar paciente'}
      descricao="Escolha o paciente que vai ocupar este assento. Sessões atrasadas são remarcadas para o dia do translado."
      largura="md"
    >
      <ListaSessoesElegiveis
        sessoes={sessoes}
        dataRota={dataRota}
        carregando={carregando}
        aoSelecionar={(s) => {
          if (s) aoEscolher(s);
        }}
      />
    </Modal>
  );
}
