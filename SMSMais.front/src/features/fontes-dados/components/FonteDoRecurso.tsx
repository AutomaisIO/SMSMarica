import { Link } from 'react-router-dom';
import { DatabaseZap, ExternalLink } from 'lucide-react';
import { usePermissao } from '@/shared/auth/authStore';

type Props = {
  /** Nome da fonte como aparece em Sistema → Fontes de dados. */
  nome: string;
  /** Tem login (ou conexão) gravado. Nulo = a tela do recurso não sabe dizer. */
  configurada?: boolean | null;
  /** Complemento curto (ex.: "login: maria.souza"). */
  detalhe?: string | null;
};

/**
 * A fonte que um recurso usa, com o atalho para onde ela se edita (ADR-0077). O recurso escolhe e
 * usa a fonte; a conexão — endereço, login, senha — só se mexe em Sistema → Fontes de dados.
 */
export function FonteDoRecurso({ nome, configurada, detalhe }: Props) {
  const podeVer = usePermissao('FontesDados', 'Consulta');

  return (
    <section className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-slate-200 bg-white p-4">
      <div className="flex min-w-0 items-start gap-2 text-sm">
        <DatabaseZap className="mt-0.5 size-4 shrink-0 text-primary-600" />
        <div className="min-w-0">
          <div>
            <span className="text-slate-500">Fonte:</span> <span className="font-medium text-slate-900">{nome}</span>
            {configurada === true ? <span className="ml-2 text-xs font-medium text-green-700">login configurado</span> : null}
            {configurada === false ? <span className="ml-2 text-xs font-medium text-red-700">falta o login</span> : null}
          </div>
          {detalhe ? <div className="text-xs text-slate-500">{detalhe}</div> : null}
          <div className="text-xs text-slate-500">
            Login, senha e endereço se editam em Sistema → Fontes de dados.
            {!podeVer ? ' Peça a quem administra essa tela.' : null}
          </div>
        </div>
      </div>
      {podeVer ? (
        <Link
          to="/app/fontes-dados"
          className="inline-flex items-center gap-1 rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          <ExternalLink className="size-4" />
          Fontes de dados
        </Link>
      ) : null}
    </section>
  );
}
