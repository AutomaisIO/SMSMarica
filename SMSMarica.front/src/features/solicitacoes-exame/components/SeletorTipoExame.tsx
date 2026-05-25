import { Select } from '@/shared/ui/Select';
import { useListarTiposExame } from '@/features/tipos-exame/api/queries';
import type { ModalidadeDicom } from '@/features/tipos-exame/types';

type Props = {
  valor: string;
  aoMudar: (id: string) => void;
  modalidade?: ModalidadeDicom;
  id?: string;
};

export function SeletorTipoExame({ valor, aoMudar, modalidade, id }: Props) {
  const lista = useListarTiposExame(modalidade, false);
  const ativos = (lista.data ?? []).filter((t) => t.ativo);

  return (
    <Select id={id} value={valor} onChange={(e) => aoMudar(e.target.value)} disabled={lista.isPending}>
      <option value="">— Selecione —</option>
      {ativos.map((t) => (
        <option key={t.id} value={t.id}>
          {t.nome} ({t.modalidadeDicom})
        </option>
      ))}
    </Select>
  );
}
