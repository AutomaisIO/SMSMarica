import { useState } from 'react';
import { HelpCircle } from 'lucide-react';

import type { AcaoPermissao, ModuloPermissao } from '@/shared/auth/authStore';
import type { MatrizEdicao } from '@/features/perfis/types';
import { ACOES, APELIDOS_ACOES_POR_MODULO, GRUPOS_PERMISSAO, MODULOS } from '@/features/perfis/lib/acoes';
import { cn } from '@/shared/lib/cn';
import { Modal } from '@/shared/ui/Modal';
import { Tabs } from '@/shared/ui/Tabs';

type Props = {
  /** Ações herdadas (de perfis); aparecem marcadas e desabilitadas. */
  herdadas?: MatrizEdicao;
  /** Ações editáveis (overrides individuais, ou permissões diretas do perfil). */
  editaveis: MatrizEdicao;
  aoMudarEditaveis: (proxima: MatrizEdicao) => void;
  desabilitado?: boolean;
};

type Modulo = (typeof MODULOS)[number];

/**
 * A matriz de permissões, em abas por assunto. Cada linha é o nome curto do módulo; a explicação
 * — e o que cada ação faz naquele módulo — fica no "?", para a tela não virar um manual.
 * A aba mostra quantos módulos dela estão liberados.
 */
export function MatrizPermissoes({
  herdadas = {},
  editaveis,
  aoMudarEditaveis,
  desabilitado,
}: Props) {
  const [ajudaDe, setAjudaDe] = useState<Modulo | null>(null);

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

  const liberado = (m: Modulo) => ACOES.some((a) => marcado(m.id, a.id));

  const abas = GRUPOS_PERMISSAO.map((g) => {
    const modulos = MODULOS.filter((m) => m.grupo === g.id);
    const ligados = modulos.filter(liberado).length;
    return {
      id: g.id,
      rotulo: g.rotulo,
      badge: ligados > 0 ? ligados : undefined,
      conteudo: (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
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
              {modulos.map((m) => {
                const apelidos = APELIDOS_ACOES_POR_MODULO[m.id];
                const temAjuda = !!m.ajuda || !!apelidos;
                return (
                  <tr key={m.id} className={cn(liberado(m) && 'bg-gray-50/60')}>
                    <td className="px-3 py-2 font-medium text-gray-900">
                      <span className="inline-flex items-center gap-1.5">
                        {m.rotulo}
                        {temAjuda && (
                          <button
                            type="button"
                            onClick={() => setAjudaDe(m)}
                            className="text-gray-400 hover:text-gray-700"
                            aria-label={`O que é ${m.rotulo}`}
                          >
                            <HelpCircle className="size-4" />
                          </button>
                        )}
                      </span>
                    </td>
                    {ACOES.map((a) => {
                      const fixo = herdou(m.id, a.id);
                      return (
                        <td key={a.id} className="px-3 py-2 text-center">
                          <input
                            type="checkbox"
                            checked={marcado(m.id, a.id)}
                            disabled={desabilitado || fixo}
                            onChange={() => alternar(m.id, a.id)}
                            title={fixo ? 'Herdada de um perfil' : apelidos?.[a.id]}
                            aria-label={`${m.rotulo}: ${a.rotulo}`}
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
                        aria-label={`${m.rotulo}: todas`}
                      />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ),
    };
  });

  return (
    <>
      <Tabs abas={abas} />
      <AjudaModulo modulo={ajudaDe} aoFechar={() => setAjudaDe(null)} />
    </>
  );
}

function AjudaModulo({ modulo, aoFechar }: { modulo: Modulo | null; aoFechar: () => void }) {
  const apelidos = modulo ? APELIDOS_ACOES_POR_MODULO[modulo.id] : undefined;
  const grupo = GRUPOS_PERMISSAO.find((g) => g.id === modulo?.grupo)?.rotulo;

  return (
    <Modal aberto={!!modulo} aoFechar={aoFechar} titulo={modulo ? `${grupo} · ${modulo.rotulo}` : ''}>
      {modulo && (
        <div className="space-y-3 text-sm">
          {modulo.ajuda && <p className="text-gray-700">{modulo.ajuda}</p>}
          {apelidos && (
            <dl className="space-y-1.5">
              {ACOES.filter((a) => apelidos[a.id]).map((a) => (
                <div key={a.id} className="grid grid-cols-[6rem_1fr] gap-2">
                  <dt className="font-medium text-gray-900">{a.rotulo}</dt>
                  <dd className="text-gray-700">{apelidos[a.id]}</dd>
                </div>
              ))}
            </dl>
          )}
        </div>
      )}
    </Modal>
  );
}
