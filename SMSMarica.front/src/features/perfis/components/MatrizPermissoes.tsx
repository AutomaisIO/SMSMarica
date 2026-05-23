import type { AcaoPermissao, ModuloPermissao } from '@/shared/auth/authStore';
import type { MatrizEdicao } from '@/features/perfis/types';
import { ACOES, MODULOS } from '@/features/perfis/lib/acoes';
import { cn } from '@/shared/lib/cn';

type Props = {
  matriz: MatrizEdicao;
  /** Permissões herdadas que aparecem marcadas + desabilitadas (não podem ser desmarcadas). */
  herdadas?: MatrizEdicao;
  aoMudar: (proxima: MatrizEdicao) => void;
  desabilitado?: boolean;
};

export function MatrizPermissoes({ matriz, herdadas = {}, aoMudar, desabilitado }: Props) {
  function temAcao(modulo: ModuloPermissao, acao: AcaoPermissao): boolean {
    return (matriz[modulo] ?? []).includes(acao);
  }

  function herdou(modulo: ModuloPermissao, acao: AcaoPermissao): boolean {
    return (herdadas[modulo] ?? []).includes(acao);
  }

  function alternar(modulo: ModuloPermissao, acao: AcaoPermissao) {
    if (desabilitado || herdou(modulo, acao)) return;
    const atuais = new Set(matriz[modulo] ?? []);
    if (atuais.has(acao)) atuais.delete(acao);
    else atuais.add(acao);
    aoMudar({ ...matriz, [modulo]: Array.from(atuais) });
  }

  function todasAcoes(modulo: ModuloPermissao): boolean {
    return ACOES.every((a) => temAcao(modulo, a.id));
  }

  function alternarLinha(modulo: ModuloPermissao) {
    if (desabilitado) return;
    // Marca tudo que não é herdado; se já está tudo marcado, desmarca o não-herdado.
    const todasMarcadas = todasAcoes(modulo);
    const proxima = new Set(herdadas[modulo] ?? []); // sempre mantém herdadas
    if (!todasMarcadas) {
      for (const a of ACOES) proxima.add(a.id);
    }
    aoMudar({ ...matriz, [modulo]: Array.from(proxima) });
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
                const marcado = temAcao(m.id, a.id);
                const fixo = herdou(m.id, a.id);
                return (
                  <td key={a.id} className="px-3 py-2 text-center">
                    <input
                      type="checkbox"
                      checked={marcado || fixo}
                      disabled={desabilitado || fixo}
                      onChange={() => alternar(m.id, a.id)}
                      title={fixo ? 'Herdada do perfil — não pode ser desmarcada.' : undefined}
                      className={cn(fixo && 'opacity-70')}
                    />
                  </td>
                );
              })}
              <td className="px-3 py-2 text-center">
                <input
                  type="checkbox"
                  checked={todasAcoes(m.id)}
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
