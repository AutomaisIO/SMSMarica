import { BrowserRouter } from 'react-router-dom';
import { AppRouter } from '@/app/router/AppRouter';
import { ErrorBoundary } from '@/app/providers/ErrorBoundary';
import { QueryProvider } from '@/app/providers/QueryProvider';
import { Notificacoes } from '@/shared/ui/Notificacoes';
import { FaixaConexao } from '@/shared/ui/FaixaConexao';

export function App() {
  return (
    <ErrorBoundary>
      <QueryProvider>
        <BrowserRouter>
          <AppRouter />
          <Notificacoes />
          <FaixaConexao />
        </BrowserRouter>
      </QueryProvider>
    </ErrorBoundary>
  );
}
