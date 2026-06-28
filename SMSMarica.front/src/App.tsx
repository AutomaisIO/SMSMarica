import { BrowserRouter } from 'react-router-dom';
import { AppRouter } from '@/app/router/AppRouter';
import { ErrorBoundary } from '@/app/providers/ErrorBoundary';
import { QueryProvider } from '@/app/providers/QueryProvider';
import { Notificacoes } from '@/shared/ui/Notificacoes';

export function App() {
  return (
    <ErrorBoundary>
      <QueryProvider>
        <BrowserRouter>
          <AppRouter />
          <Notificacoes />
        </BrowserRouter>
      </QueryProvider>
    </ErrorBoundary>
  );
}
