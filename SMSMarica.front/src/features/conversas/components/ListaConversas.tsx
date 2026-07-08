import { useEffect, useMemo, useState } from 'react';
import { Search } from 'lucide-react';
import { useListarConversas } from '@/features/conversas/api/queries';
import { useChat } from '@/features/conversas/store/chatStore';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { ROTULO_ASSUNTO, type AbaConversas } from '@/features/conversas/types';

type Props = {
  conversaAtivaId: string | null;
  onSelecionar: (id: string) => void;
  podeSupervisao: boolean;
  /** Só a instância do widget global deve alimentar o total (evita escrita duplicada). */
  alimentarTotalGlobal?: boolean;
};

const ABAS: { id: AbaConversas; rotulo: string }[] = [
  { id: 'Minhas', rotulo: 'Minhas' },
  { id: 'Unidade', rotulo: 'Da unidade' },
  { id: 'NaoAtribuidas', rotulo: 'Não atribuídas' },
];

function formatarHora(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  const hoje = new Date();
  const mesmoDia = d.toDateString() === hoje.toDateString();
  return mesmoDia
    ? d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
    : d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
}

export function ListaConversas({ conversaAtivaId, onSelecionar, podeSupervisao, alimentarTotalGlobal }: Props) {
  const [aba, setAba] = useState<AbaConversas>('Unidade');
  const [busca, setBusca] = useState('');
  const setTotalNaoLidas = useChat((s) => s.setTotalNaoLidas);

  const abas = useMemo(
    () => (podeSupervisao ? [...ABAS, { id: 'Todas' as AbaConversas, rotulo: 'Todas' }] : ABAS),
    [podeSupervisao],
  );

  const { data: conversas, isLoading } = useListarConversas(aba, busca);

  useEffect(() => {
    if (!alimentarTotalGlobal || !conversas) return;
    setTotalNaoLidas(conversas.reduce((acc, c) => acc + c.naoLidas, 0));
  }, [alimentarTotalGlobal, conversas, setTotalNaoLidas]);

  return (
    <div className="flex h-full flex-col">
      <div className="flex gap-1 border-b border-gray-200 px-2 pt-2">
        {abas.map((a) => (
          <button
            key={a.id}
            type="button"
            onClick={() => setAba(a.id)}
            className={`rounded-t-md px-2.5 py-1.5 text-xs font-medium ${
              aba === a.id ? 'bg-primary-50 text-primary-700' : 'text-gray-500 hover:bg-gray-50'
            }`}
          >
            {a.rotulo}
          </button>
        ))}
      </div>

      <div className="border-b border-gray-100 p-2">
        <div className="flex items-center gap-2 rounded-md border border-gray-200 px-2">
          <Search className="h-4 w-4 text-gray-400" />
          <input
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Buscar por nome ou telefone"
            className="w-full bg-transparent py-1.5 text-sm outline-none"
          />
        </div>
      </div>

      <ul className="flex-1 divide-y divide-gray-100 overflow-y-auto">
        {isLoading && <li className="p-4 text-sm text-gray-500">Carregando…</li>}
        {!isLoading && (conversas?.length ?? 0) === 0 && (
          <li className="p-4 text-sm text-gray-500">Nenhuma conversa nesta lista.</li>
        )}
        {conversas?.map((c) => (
          <li key={c.id}>
            {/* div clicável (não <button>) para poder aninhar o botão do resumo do paciente. */}
            <div
              role="button"
              tabIndex={0}
              onClick={() => onSelecionar(c.id)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') onSelecionar(c.id);
              }}
              className={`flex w-full cursor-pointer items-start gap-2 px-3 py-2.5 text-left hover:bg-gray-50 ${
                conversaAtivaId === c.id ? 'bg-primary-50' : ''
              }`}
            >
              <div className="min-w-0 flex-1">
                <div className="flex items-center justify-between gap-2">
                  <span className="flex min-w-0 items-center gap-1">
                    {/* Nome do PERFIL do WhatsApp — nunca sobreposto pelo nome do banco. */}
                    <span className="truncate text-sm font-medium text-gray-900">
                      {c.nomeContato || c.telefoneCanonical}
                    </span>
                    {c.pacienteId ? (
                      <span onClick={(e) => e.stopPropagation()}>
                        <NomePacienteComResumo pacienteId={c.pacienteId} />
                      </span>
                    ) : null}
                  </span>
                  <span className="shrink-0 text-[11px] text-gray-400">{formatarHora(c.ultimaMensagemEm)}</span>
                </div>
                <div className="flex items-center justify-between gap-2">
                  <span className="truncate text-xs text-gray-500">
                    {c.ultimaMensagemDirecao === 'Saida' ? 'Você: ' : ''}
                    {c.ultimaMensagemPreview || '—'}
                  </span>
                  {c.naoLidas > 0 && (
                    <span className="ml-1 inline-flex h-5 min-w-5 shrink-0 items-center justify-center rounded-full bg-primary-600 px-1.5 text-[11px] font-semibold text-white">
                      {c.naoLidas}
                    </span>
                  )}
                </div>
                <div className="mt-1 flex flex-wrap items-center gap-1">
                  {c.assunto && (
                    <span className="rounded bg-gray-100 px-1.5 py-0.5 text-[10px] text-gray-600">
                      {ROTULO_ASSUNTO[c.assunto]}
                    </span>
                  )}
                  {c.unidadeNome && (
                    <span className="truncate rounded bg-gray-100 px-1.5 py-0.5 text-[10px] text-gray-600">
                      {c.unidadeNome}
                    </span>
                  )}
                  {c.operadorResponsavelNome && (
                    <span className="truncate rounded bg-sky-50 px-1.5 py-0.5 text-[10px] text-sky-700">
                      {c.operadorResponsavelNome}
                    </span>
                  )}
                  {!c.podeTextoLivre && c.status !== 'Pendente' && (
                    <span className="rounded bg-amber-50 px-1.5 py-0.5 text-[10px] text-amber-700">
                      janela expirada
                    </span>
                  )}
                </div>
              </div>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
