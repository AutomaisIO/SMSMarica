import type { AcaoPermissao, ModuloPermissao } from '@/shared/auth/authStore';
import type { MatrizEdicao } from '@/features/perfis/types';
import { ACOES, MODULOS } from '@/features/perfis/lib/acoes';
import { cn } from '@/shared/lib/cn';

type Props = {
  /** Ações herdadas (de perfis); aparecem marcadas e desabilitadas. */
  herdadas?: MatrizEdicao;
  /** Ações editáveis (overrides individuais, ou permissões diretas do perfil). */
  editaveis: MatrizEdicao;
  aoMudarEditaveis: (proxima: MatrizEdicao) => void;
  desabilitado?: boolean;
};

export function MatrizPermissoes({
  herdadas = {},
  editaveis,
  aoMudarEditaveis,
  desabilitado,
}: Props) {
  function herdou(modulo: ModuloPermissao, acao: AcaoPermissao): boolean {
    return (herdadas[modulo] ?? []).includes(acao);
  }

  function temEditavel(modulo: ModuloPermissao, acao: AcaoPermissao): boolean {
    return (editaveis[modulo] ?? []).includes(acao);
  }

  function marcado(modulo: ModuloPermissao, acao: AcaoPermissao): boolean {
    return herdou(modulo, acao) || temEditavel(modulo, acao);
  }

  function alternar(modulo: ModuloPermissao, acao: AcaoPermissao) {
    if (desabilitado || herdou(modulo, acao)) return;
    const atuais = new Set(editaveis[modulo] ?? []);
    if (atuais.has(acao)) atuais.delete(acao);
    else atuais.add(acao);
    aoMudarEditaveis({ ...editaveis, [modulo]: Array.from(atuais) });
  }

  function todasMarcadas(modulo: ModuloPermissao): boolean {
    return ACOES.every((a) => marcado(modulo, a.id));
  }

  function alternarLinha(modulo: ModuloPermissao) {
    if (desabilitado) return;
    const todas = todasMarcadas(modulo);
    const proxima = new Set<AcaoPermissao>(editaveis[modulo] ?? []);
    if (todas) {
      // Desmarca apenas as não-herdadas.
      for (const a of ACOES) if (!herdou(modulo, a.id)) proxima.delete(a.id);
    } else {
      for (const a of ACOES) if (!herdou(modulo, a.id)) proxima.add(a.id);
    }
    aoMudarEditaveis({ ...editaveis, [modulo]: Array.from(proxima) });
  }

  return (
    <div className="overflow-hidden rounded-lg border border-gray-200">
      <table className="min-w-full text-sm">
        <thead className="bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
          <tr>
            <th className="px-3 py-2 text-left">Módulo</th>
            {ACOES.map((a) => (
              <th key={a.id} className="px-3 py-2 text-center">
                {a.rotulo}
              </th>
            ))}
            <th className="px-3 py-2 text-center">Todas</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {MODULOS.map((m) => (
            <tr key={m.id}>
              <td className="px-3 py-2 font-medium text-gray-900">{m.rotulo}</td>
              {ACOES.map((a) => {
                const fixo = herdou(m.id, a.id);
                return (
                  <td key={a.id} className="px-3 py-2 text-center">
                    <input
                      type="checkbox"
                      checked={marcado(m.id, a.id)}
                      disabled={desabilitado || fixo}
                      onChange={() => alternar(m.id, a.id)}
                      title={fixo ? 'Herdada de um perfil — não pode ser desmarcada.' : undefined}
                      className={cn(fixo && 'opacity-70')}
                    />
                  </td>
                );
              })}
              <td className="px-3 py-2 text-center">
                <input
                  type="checkbox"
                  checked={todasMarcadas(m.id)}
                  disabled={desabilitado}
                  onChange={() => alternarLinha(m.id)}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
