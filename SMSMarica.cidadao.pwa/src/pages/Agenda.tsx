import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { http, extrairMensagemDeErro } from '@/lib/httpClient';
import { useAuth } from '@/store/auth';
import { Tela } from '@/components/Tela';

type TransladoResumo = {
  id: string;
  data: string;
  destino: string;
  status: string;
};

export function Agenda() {
  const paciente = useAuth((s) => s.paciente);
  const sair = useAuth((s) => s.sair);
  const [itens, setItens] = useState<TransladoResumo[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    let ativo = true;
    // TODO(FT7): endpoint a implementar no backend — agenda do paciente autenticado.
    http
      .get<TransladoResumo[]>('/auth/paciente/meus-translados')
      .then(({ data }) => ativo && setItens(data))
      .catch((e) => ativo && setErro(extrairMensagemDeErro(e)));
    return () => {
      ativo = false;
    };
  }, []);

  return (
    <Tela
      titulo={paciente ? `Olá, ${paciente.nome.split(' ')[0]}` : 'Minha agenda'}
      acao={
        <button onClick={sair} className="text-sm underline">
          Sair
        </button>
      }
    >
      <h2 className="text-base font-semibold text-neutral-700 mb-3">Próximos transportes</h2>

      {erro && (
        <div className="rounded-xl border border-neutral-200 bg-white p-4 text-sm text-neutral-600">
          Não foi possível carregar sua agenda agora. {erro}
        </div>
      )}

      {!erro && itens === null && <p className="text-neutral-500">Carregando…</p>}

      {!erro && itens?.length === 0 && (
        <p className="text-neutral-500">Você não tem transportes agendados no momento.</p>
      )}

      <ul className="space-y-3">
        {itens?.map((t) => (
          <li key={t.id}>
            <Link
              to={`/translado/${t.id}`}
              className="block rounded-xl border border-neutral-200 bg-white p-4 active:bg-neutral-50"
            >
              <div className="flex items-center justify-between">
                <span className="font-medium">{t.destino}</span>
                <span className="text-xs rounded-full bg-marica/10 px-2 py-0.5 text-marica">
                  {t.status}
                </span>
              </div>
              <div className="mt-1 text-sm text-neutral-500">{t.data}</div>
            </Link>
          </li>
        ))}
      </ul>
    </Tela>
  );
}
