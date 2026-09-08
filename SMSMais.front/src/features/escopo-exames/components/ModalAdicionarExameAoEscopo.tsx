import { useMemo, useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { useListarTiposExame } from '@/features/tipos-exame/api/queries';
import { useListarEquipamentos } from '@/features/equipamentos/api/queries';
import { useAdicionarEscopo } from '@/features/escopo-exames/api/queries';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  unidadeId: string;
  /** Ids já no escopo — some da busca, para não oferecer duplicata que a API recusaria. */
  jaNoEscopo: string[];
};

/**
 * Traz um exame do catálogo do município para o escopo desta unidade. Entra desligado por padrão:
 * ligar é decisão de quem conhece a operação, e item mal formado no aparelho é pior que item
 * nenhum.
 */
export function ModalAdicionarExameAoEscopo({ aberto, aoFechar, unidadeId, jaNoEscopo }: Props) {
  const [busca, setBusca] = useState('');
  const [tipoExameId, setTipoExameId] = useState('');
  const [equipamentoId, setEquipamentoId] = useState('');
  const [enviar, setEnviar] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const tipos = useListarTiposExame();
  const equipamentos = useListarEquipamentos(unidadeId);
  const adicionar = useAdicionarEscopo();

  const disponiveis = useMemo(() => {
    const noEscopo = new Set(jaNoEscopo);
    const termo = busca.trim().toLowerCase();
    return (tipos.data ?? [])
      .filter((t) => t.ativo && !noEscopo.has(t.id))
      .filter((t) => !termo || t.nome.toLowerCase().includes(termo))
      .slice(0, 50);
  }, [tipos.data, jaNoEscopo, busca]);

  function fechar() {
    setBusca('');
    setTipoExameId('');
    setEquipamentoId('');
    setEnviar(false);
    setErro(null);
    aoFechar();
  }

  function aoSalvar(ev: FormEvent) {
    ev.preventDefault();
    setErro(null);
    if (!tipoExameId) {
      setErro('Escolha o exame.');
      return;
    }
    adicionar.mutate(
      {
        tipoExameId,
        unidadeId,
        enviarParaWorklist: enviar,
        equipamentoId: equipamentoId || null,
      },
      { onSuccess: fechar, onError: (err) => setErro(extrairMensagemDeErro(err)) },
    );
  }

  const aparelhos = (equipamentos.data ?? []).filter((e) => e.ativo && e.identificadorDicom);

  return (
    <Modal aberto={aberto} aoFechar={fechar} titulo="Adicionar exame ao escopo da unidade">
      <form onSubmit={aoSalvar} className="space-y-4">
        <Campo label="Buscar no catálogo" htmlFor="esc-busca">
          <Input
            id="esc-busca"
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Ex.: RADIOGRAFIA DE TORAX"
            autoFocus
          />
        </Campo>

        <Campo label="Exame" htmlFor="esc-tipo" required>
          <Select
            id="esc-tipo"
            value={tipoExameId}
            onChange={(e) => setTipoExameId(e.target.value)}
            required
          >
            <option value="">Selecione…</option>
            {disponiveis.map((t) => (
              <option key={t.id} value={t.id}>
                {t.nome}
              </option>
            ))}
          </Select>
        </Campo>

        <Campo
          label="Aparelho de destino"
          htmlFor="esc-equip"
          dica="Opcional. Em branco, o sistema deduz pela modalidade — e, havendo mais de um aparelho, a recepção escolhe a sala na autorização."
        >
          <Select
            id="esc-equip"
            value={equipamentoId}
            onChange={(e) => setEquipamentoId(e.target.value)}
          >
            <option value="">Deduzir pela modalidade</option>
            {aparelhos.map((e) => (
              <option key={e.id} value={e.id}>
                {e.nome} ({e.identificadorDicom})
              </option>
            ))}
          </Select>
        </Campo>

        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={enviar} onChange={(e) => setEnviar(e.target.checked)} />
          Já enviar à worklist desta unidade
        </label>

        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </div>
        ) : null}

        <div className="flex items-center justify-end gap-3 pt-2">
          <Button type="button" variante="secundaria" onClick={fechar}>
            Cancelar
          </Button>
          <Button type="submit" disabled={adicionar.isPending || !tipoExameId}>
            {adicionar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Adicionar
          </Button>
        </div>
      </form>
    </Modal>
  );
}
