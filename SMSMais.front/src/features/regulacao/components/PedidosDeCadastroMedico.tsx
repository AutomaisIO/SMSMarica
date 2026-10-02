import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { UserPlus } from 'lucide-react';

import { useTemConsulta } from '@/shared/auth/authStore';
import { formatarInstante } from '@/shared/lib/datas';

import { listarMedicosPendentes, type SituacaoMedicoPendente } from '../api/medicosApi';
import { ROTULO_SISTEMA_REGULACAO, type SistemaRegulacao } from '../types';
import { MedicoPendenteCard } from './MedicoPendenteCard';

/**
 * Os médicos que as unidades pediram na abertura da solicitação e que ainda não estão (ou já
 * foram resolvidos) na lista do sistema. É a fila do técnico da regulação para cadastrar no
 * sistema de destino — a mesma resolução do cartão do detalhe da solicitação, num lugar só.
 */
export function PedidosDeCadastroMedico({ sistema }: { sistema: SistemaRegulacao }) {
  const [situacao, setSituacao] = useState<SituacaoMedicoPendente | ''>('Pendente');
  const podeResolver = useTemConsulta('RegulacaoTriagem');
  const pedidos = useQuery({
    queryKey: ['regulacao', 'medicos', 'pendentes', sistema, situacao],
    queryFn: () => listarMedicosPendentes(sistema, situacao || null),
  });
  const nome = ROTULO_SISTEMA_REGULACAO[sistema];

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
          <UserPlus className="size-4" /> Pedidos de cadastro no {nome}
        </h2>
        <select
          value={situacao}
          onChange={(e) => setSituacao(e.target.value as SituacaoMedicoPendente | '')}
          className="rounded-md border border-slate-300 px-2 py-1 text-sm"
        >
          <option value="Pendente">Aguardando cadastro</option>
          <option value="Cadastrado">Cadastrados</option>
          <option value="JaExistia">Já existiam</option>
          <option value="Recusado">Recusados</option>
          <option value="">Todos</option>
        </select>
      </div>
      <p className="text-xs text-slate-500">
        Médicos que as unidades pediram ao abrir a solicitação. Quem cadastra no {nome} é a regulação, pela
        tela de lá (ícone “Adicionar médico” ao lado do médico responsável), e confirma aqui.
      </p>

      {pedidos.isLoading && <p className="text-sm text-slate-500">Carregando…</p>}
      {!pedidos.isLoading && (pedidos.data ?? []).length === 0 && (
        <p className="text-sm text-slate-500">Nenhum pedido nesta situação.</p>
      )}
      <ul className="space-y-2">
        {(pedidos.data ?? []).map((m) => (
          <li key={m.id}>
            <MedicoPendenteCard valorMedico={m.valor} podeResolver={podeResolver} />
            <p className="mt-0.5 text-[11px] text-slate-400">Pedido em {formatarInstante(m.criadoEm)}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
