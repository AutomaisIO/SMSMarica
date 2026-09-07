import { ShieldX } from 'lucide-react';
import { Link, Outlet } from 'react-router-dom';

import { useTemConsulta } from '@/shared/auth/authStore';
import type { ModuloPermissao } from '@/shared/auth/authStore';

/**
 * Gate de rota por módulo de permissão.
 *
 * <p>Até aqui, esconder o item do menu era a única barreira do front — e menu escondido não é
 * barreira: quem digita a URL entra na tela, que então dispara chamadas e recebe 403 solto. O
 * backend continua sendo a trava de verdade (`[RequerPermissao]`); isto existe para a pessoa ver
 * uma explicação em vez de uma tela quebrada.</p>
 *
 * <p>Usa <b>Consulta</b> como piso: quem não pode nem consultar não tem o que fazer na rota. As
 * ações mais finas (Edição, Exclusão) ficam nos botões dentro da tela.</p>
 */
export function RotaComModulo({ modulo, rotulo }: { modulo: ModuloPermissao; rotulo?: string }) {
  const podeVer = useTemConsulta(modulo);

  if (podeVer) return <Outlet />;

  return (
    <div className="mx-auto max-w-lg py-16 text-center">
      <ShieldX className="mx-auto size-10 text-slate-400" />
      <h1 className="mt-3 text-lg font-semibold text-slate-900">Você não tem acesso a esta tela</h1>
      <p className="mt-2 text-sm text-slate-600">
        Ela depende da permissão <strong>{rotulo ?? modulo}</strong>. Se você precisa dela, peça a
        quem administra os perfis do sistema.
      </p>
      <Link
        to="/app"
        className="mt-5 inline-block rounded bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700"
      >
        Voltar ao início
      </Link>
    </div>
  );
}
