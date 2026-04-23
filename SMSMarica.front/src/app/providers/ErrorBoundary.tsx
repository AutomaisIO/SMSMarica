import { Component, type ErrorInfo, type ReactNode } from 'react';

type Props = { children: ReactNode };
type State = { erro: Error | null };

export class ErrorBoundary extends Component<Props, State> {
  state: State = { erro: null };

  static getDerivedStateFromError(erro: Error): State {
    return { erro };
  }

  componentDidCatch(erro: Error, info: ErrorInfo): void {
    console.error('Erro fatal capturado pelo ErrorBoundary:', erro, info);
  }

  reset = () => this.setState({ erro: null });

  render() {
    if (this.state.erro) {
      return (
        <div className="flex min-h-screen items-center justify-center bg-gray-50 px-4">
          <div className="w-full max-w-md rounded-xl border border-red-200 bg-white p-6 text-center shadow-lg">
            <h1 className="text-lg font-semibold text-red-700">Algo deu errado</h1>
            <p className="mt-2 text-sm text-gray-600">
              A aplicação encontrou um erro inesperado. Tente recarregar a página. Se o problema
              continuar, contate o suporte.
            </p>
            <pre className="mt-4 max-h-40 overflow-auto rounded bg-gray-50 p-3 text-left text-xs text-gray-700">
              {this.state.erro.message}
            </pre>
            <button type="button" onClick={this.reset} className="btn btn-primary mt-4">
              Tentar de novo
            </button>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}
