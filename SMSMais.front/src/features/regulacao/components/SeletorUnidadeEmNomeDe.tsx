import { SelectComBusca } from '@/shared/ui/SelectComBusca';

import { useUnidadesEmNomeDe } from '../api/solicitacoesQueries';

/**
 * "Em nome de qual unidade" do NAR. Lista todas as unidades ativas do município com CNES — não só
 * as de quem pede: o NAR existe justamente para pedir em nome de outra. O CNES vai no rótulo
 * porque é com ele que o pedido entra no SISREG, e há unidades de nome parecido.
 */
export function SeletorUnidadeEmNomeDe({
  id,
  valor,
  onChange,
  desabilitado,
}: {
  id: string;
  valor: string;
  onChange: (unidadeId: string) => void;
  desabilitado?: boolean;
}) {
  const unidades = useUnidadesEmNomeDe();

  if (unidades.isLoading) return <p className="text-sm text-slate-500">Carregando as unidades…</p>;
  if (!unidades.data?.length) {
    return (
      <p className="text-sm text-amber-700">
        Nenhuma unidade ativa com CNES cadastrado. O NAR precisa do CNES da unidade.
      </p>
    );
  }

  return (
    <SelectComBusca
      id={id}
      opcoes={unidades.data.map((u) => ({ valor: u.id, rotulo: `${u.nome} — CNES ${u.cnes}` }))}
      valor={valor}
      onChange={onChange}
      desabilitado={desabilitado}
      placeholder="Escolha a unidade…"
    />
  );
}
