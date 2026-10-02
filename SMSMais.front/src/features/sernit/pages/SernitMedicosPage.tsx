import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search, Stethoscope } from 'lucide-react';

import { listarMedicosDoSistema } from '@/features/regulacao/api/medicosApi';
import { PedidosDeCadastroMedico } from '@/features/regulacao/components/PedidosDeCadastroMedico';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Input } from '@/shared/ui/Input';
import { filtrarPorPalavras } from '@/shared/ui/SelectComBusca';

/**
 * Médicos do SERNIT — a lista de médicos do sistema (a cópia do combo "Médico responsável", que é
 * a mesma da Nova Solicitação) e os pedidos de cadastro que as unidades fizeram.
 *
 * <p>Diferente do SER, o SERNIT não tem um cadastro de profissionais mapeado para importar: a
 * fonte é o combo, copiado em "Copiar catálogo do SERNIT". O cadastro de médico novo no SERNIT é
 * feito pela regulação, pelo ícone "Adicionar médico" da tela de solicitação de lá — que tem CPF
 * com busca, nome, tipo/número de documento (CRM…) e especialidade.</p>
 */
export function SernitMedicosPage() {
  const [aba, setAba] = useState<'lista' | 'pedidos'>('lista');
  const [termo, setTermo] = useState('');
  const lista = useQuery({
    queryKey: ['regulacao', 'medicos', 'lista', 'Sernit'],
    queryFn: () => listarMedicosDoSistema('Sernit'),
    staleTime: 5 * 60_000,
  });
  const filtrados = filtrarPorPalavras(lista.data ?? [], termo, (n) => n);

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2">
        <Stethoscope className="size-5 text-slate-500" />
        <h1 className="text-xl font-semibold text-slate-900">Médicos do SERNIT</h1>
        <AjudaManual artigo="sernit-medicos" />
      </div>

      <div className="flex gap-1 border-b border-slate-200">
        {(
          [
            ['lista', `Na lista do SERNIT${lista.data ? ` (${lista.data.length})` : ''}`],
            ['pedidos', 'Pedidos de cadastro'],
          ] as const
        ).map(([id, rotulo]) => (
          <button
            key={id}
            type="button"
            onClick={() => setAba(id)}
            className={`-mb-px border-b-2 px-3 py-1.5 text-sm ${
              aba === id ? 'border-primary-600 font-medium text-primary-700' : 'border-transparent text-slate-600'
            }`}
          >
            {rotulo}
          </button>
        ))}
      </div>

      {aba === 'lista' ? (
        <section className="space-y-3">
          <p className="text-xs text-slate-500">
            É esta a lista do campo “Médico solicitante” da Nova Solicitação com destino SERNIT. Ela vem do
            próprio SERNIT, em “Copiar catálogo do SERNIT” (Configuração).
          </p>
          <div className="relative max-w-md">
            <Search className="absolute left-2.5 top-2.5 size-4 text-slate-400" />
            <Input
              className="pl-8"
              placeholder="Procure por palavras — acha nome abreviado"
              value={termo}
              onChange={(e) => setTermo(e.target.value)}
            />
          </div>
          {lista.isLoading && <p className="text-sm text-slate-500">Carregando…</p>}
          <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200 bg-white">
            {filtrados.map((n) => (
              <li key={n} className="px-3 py-1.5 text-sm text-slate-800">
                {n}
              </li>
            ))}
            {!lista.isLoading && filtrados.length === 0 && (
              <li className="px-3 py-4 text-center text-sm text-slate-500">
                Ninguém com “{termo.trim()}”. Se o médico não está no SERNIT, a unidade o pede pela Nova
                Solicitação (“Incluir médico”).
              </li>
            )}
          </ul>
        </section>
      ) : (
        <PedidosDeCadastroMedico sistema="Sernit" />
      )}
    </div>
  );
}
