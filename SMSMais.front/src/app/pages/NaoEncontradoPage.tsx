import { Link } from 'react-router-dom';

export function NaoEncontradoPage() {
  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-10">
      <div className="text-center">
        <p className="text-sm font-semibold uppercase tracking-wide text-primary-700">404</p>
        <h1 className="mt-2 text-3xl font-bold text-gray-900">Página não encontrada</h1>
        <p className="mt-2 text-sm text-gray-600">O recurso solicitado não existe ou foi movido.</p>
        <Link to="/" className="btn btn-primary mt-6">
          Voltar ao início
        </Link>
      </div>
    </div>
  );
}
