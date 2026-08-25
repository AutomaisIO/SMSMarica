import { Settings2 } from 'lucide-react';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { AbaSernitConfiguracao } from '@/features/sernit/components/AbaSernitConfiguracao';

/**
 * Configuração da Regulação. Nasce com uma aba (SERNIT) porque é a primeira fonte externa de
 * regulação a ganhar motor próprio — as demais frentes da Regulação (triagem, decisão médica,
 * agendamento) entram aqui como abas novas conforme forem implementadas.
 */
export default function RegulacaoSernitConfiguracaoPage() {
  const abas: Aba[] = [
    {
      id: 'sernit',
      rotulo: 'SERNIT',
      conteudo: <AbaSernitConfiguracao />,
    },
  ];

  return (
    <div className="space-y-4">
      <header className="flex items-center gap-3">
        <Settings2 className="size-6 text-red-600" />
        <div>
          <h1 className="text-xl font-semibold">Regulação — Configuração do SERNIT (Niterói)</h1>
          <p className="text-sm text-slate-500">
            Credenciais e motores das fontes externas de regulação.
          </p>
        </div>
      </header>

      <Tabs abas={abas} />
    </div>
  );
}
