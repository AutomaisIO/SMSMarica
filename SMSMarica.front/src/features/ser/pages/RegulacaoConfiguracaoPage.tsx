import { Settings2 } from 'lucide-react';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { AbaSerConfiguracao } from '@/features/ser/components/AbaSerConfiguracao';

/**
 * Configuração da Regulação. Nasce com uma aba (SER) porque é a primeira fonte externa de
 * regulação a ganhar motor próprio — as demais frentes da Regulação (triagem, decisão médica,
 * agendamento) entram aqui como abas novas conforme forem implementadas.
 */
export function RegulacaoConfiguracaoPage() {
  const abas: Aba[] = [
    {
      id: 'ser',
      rotulo: 'SER',
      conteudo: <AbaSerConfiguracao />,
    },
  ];

  return (
    <div className="space-y-4">
      <header className="flex items-center gap-3">
        <Settings2 className="size-6 text-red-600" />
        <div>
          <h1 className="text-xl font-semibold">Configuração da Regulação</h1>
          <p className="text-sm text-slate-500">
            Credenciais e motores das fontes externas de regulação.
          </p>
        </div>
      </header>

      <Tabs abas={abas} />
    </div>
  );
}
