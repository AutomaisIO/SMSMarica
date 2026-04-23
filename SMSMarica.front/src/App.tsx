import { BrowserRouter } from 'react-router-dom';
import { AppRouter } from '@/app/router/AppRouter';
import { ErrorBoundary } from '@/app/providers/ErrorBoundary';
import { QueryProvider } from '@/app/providers/QueryProvider';

export function App() {
  return (
    <ErrorBoundary>
      <QueryProvider>
        <BrowserRouter>
          <AppRouter />
        </BrowserRouter>
      </QueryProvider>
    </ErrorBoundary>
  );
}
