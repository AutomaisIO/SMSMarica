import { Settings2 } from 'lucide-react';

import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { AbaEsusSgConfiguracao } from '@/features/esussg/components/AbaEsusSgConfiguracao';

/**
 * Configuração do ESUS de São Gonçalo (ADR-0063): credencial, motor de varredura, catálogo e
 * rodada diária. Em abas, como a do SERNIT, para caber o que vier depois.
 */
export default function RegulacaoEsusSgConfiguracaoPage() {
  const abas: Aba[] = [
    {
      id: 'esussg',
      rotulo: 'ESUS São Gonçalo',
      conteudo: <AbaEsusSgConfiguracao />,
    },
  ];

  return (
    <div className="space-y-4">
      <header className="flex items-center gap-3">
        <Settings2 className="size-6 text-red-600" />
        <div>
          <div className="flex items-center gap-1">
            <h1 className="text-xl font-semibold">Regulação — Configuração do ESUS São Gonçalo</h1>
            <AjudaManual artigo="esus-sao-goncalo" secao="configuracao" />
          </div>
          <p className="text-sm text-slate-500">
            Credencial e motor do espelho do ESUS de São Gonçalo. A integração é só leitura: o motor
            lê a fila e os agendados, e nada é escrito no ESUS.
          </p>
        </div>
      </header>

      <Tabs abas={abas} />
    </div>
  );
}
