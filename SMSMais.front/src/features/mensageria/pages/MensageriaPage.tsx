import { MessageSquareText } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { Tabs } from '@/shared/ui/Tabs';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { AbaCampanhas } from '@/features/mensageria/components/AbaCampanhas';
import { AbaEnvios } from '@/features/mensageria/components/AbaEnvios';
import { AbaLote } from '@/features/mensageria/components/AbaLote';
import { AbaRegras } from '@/features/mensageria/components/AbaRegras';
import { AbaRespostas } from '@/features/mensageria/components/AbaRespostas';
import { AbaTesteModelo } from '@/features/mensageria/components/AbaTesteModelo';
import { AbaResumoDiario } from '@/features/mensageria/components/AbaResumoDiario';

const ABAS = ['resumo', 'envios', 'respostas', 'lote', 'campanhas', 'regras', 'teste'] as const;

/**
 * Mensageria (módulo 38): a gestão dos envios de WhatsApp ao paciente — o que saiu, chegou, falhou
 * e por quê, o resumo por dia, as respostas, o disparo manual em lote e as regras. É a tela do
 * gestor; o trabalho das atendentes fica em Confirmações.
 */
export function MensageriaPage() {
  const [params, setParams] = useSearchParams();
  const aba = (ABAS as readonly string[]).includes(params.get('aba') ?? '') ? (params.get('aba') as string) : 'resumo';

  return (
    <div className="space-y-6">
      <header className="flex items-start gap-3">
        <MessageSquareText className="mt-1 h-6 w-6 text-primary-600" />
        <div>
          <div className="flex items-center gap-1">
            <h1 className="text-2xl font-semibold text-gray-900">Mensageria</h1>
            <AjudaManual artigo="mensageria" />
          </div>
          <p className="mt-1 text-sm text-gray-600">
            Envios de WhatsApp ao paciente: qualidade da entrega dia a dia, falhas, respostas, disparo em lote, campanhas e regras.
          </p>
        </div>
      </header>
      <Tabs
        abaAtiva={aba}
        aoTrocarAba={(id) => setParams((p) => { p.set('aba', id); return p; }, { replace: true })}
        abas={[
          { id: 'resumo', rotulo: 'Resumo diário', conteudo: <AbaResumoDiario /> },
          { id: 'envios', rotulo: 'Envios', conteudo: <AbaEnvios /> },
          { id: 'respostas', rotulo: 'Respostas dos pacientes', conteudo: <AbaRespostas /> },
          { id: 'lote', rotulo: 'Disparar lote', conteudo: <AbaLote /> },
          { id: 'campanhas', rotulo: 'Campanhas', conteudo: <AbaCampanhas /> },
          { id: 'regras', rotulo: 'Regras e parâmetros', conteudo: <AbaRegras /> },
          { id: 'teste', rotulo: 'Testar modelo', conteudo: <AbaTesteModelo /> },
        ]}
      />
    </div>
  );
}
