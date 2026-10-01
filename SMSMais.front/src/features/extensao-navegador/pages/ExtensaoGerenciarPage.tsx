import { Puzzle } from 'lucide-react';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Tabs } from '@/shared/ui/Tabs';
import { useDispositivos } from '@/features/extensao-navegador/api/queries';
import { AbaApiPublicacao } from '@/features/extensao-navegador/components/AbaApiPublicacao';
import { AbaComputadores } from '@/features/extensao-navegador/components/AbaComputadores';
import { AbaVersoes } from '@/features/extensao-navegador/components/AbaVersoes';

/**
 * Gerenciador da extensão do Chrome (Sistema): os computadores autorizados, as versões publicadas
 * da extensão e do atualizador, e a opção de publicar por API.
 */
export function ExtensaoGerenciarPage() {
  const dispositivos = useDispositivos();
  const ativos = (dispositivos.data ?? []).filter((d) => !d.revogadoEm).length;

  return (
    <div className="space-y-6">
      <header className="flex items-start gap-3">
        <Puzzle className="mt-1 h-6 w-6 text-primary-600" />
        <div>
          <div className="flex items-center gap-1.5">
            <h1 className="text-2xl font-semibold text-gray-900">Extensão Chrome — computadores e versões</h1>
            <AjudaManual artigo="extensao-chrome-gerenciar" />
          </div>
          <p className="mt-1 text-sm text-gray-600">
            Quem está com a extensão, em que versão, e o que está publicado para os computadores.
          </p>
        </div>
      </header>

      <Tabs
        abas={[
          { id: 'computadores', rotulo: 'Computadores', badge: ativos || undefined, conteudo: <AbaComputadores /> },
          { id: 'versoes', rotulo: 'Versões', conteudo: <AbaVersoes /> },
          { id: 'api', rotulo: 'Publicação por API', conteudo: <AbaApiPublicacao /> },
        ]}
      />
    </div>
  );
}
