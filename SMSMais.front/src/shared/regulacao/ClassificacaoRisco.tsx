import { useEffect } from 'react';

import type { OpcaoRegulacao } from './tiposCampo';

/**
 * Classificação de risco do SER/SERNIT, com a mesma cor nos dois.
 *
 * <p>Os dois sistemas são a mesma aplicação JSF e gravam os mesmos códigos (`EMERGENCIA`,
 * `URGENCIA`, `PRIORIDADE_NAO_URGENTE`, `CONSULTA_BAIXA_COMPLEXIDADE`), mas escrevem a opção de
 * jeitos diferentes: o SER mostra só "Prioridade 1..4", o SERNIT só o nome ("Emergência"…) e
 * ainda tem `NAO_CLASSIFICADO`, que o SER não tem. Aqui a opção aparece como "P1 · Emergência"
 * num badge colorido — mas o valor que se grava e se envia continua o do sistema, e a lista de
 * opções é a copiada dele: o que um sistema não tem, a tela não inventa.</p>
 *
 * <p>Cores semânticas, fixas de propósito (não são da identidade do tenant): são as quatro do
 * SISREG — vermelho, amarelo, verde, azul — para quem lida com os três sistemas se acostumar a
 * uma régua só. O ESUS SG fica de fora: a prioridade dele é outra coisa (idade entra junto) e
 * mantém as cores dele (`PrioridadeEsusSg`). Ver docs/ser-criar-solicitacao.md §2.1.</p>
 */

/** `classe` pinta o badge marcado; `ponto` é a bolinha de cor das opções ainda não marcadas. */
type Nivel = { sigla: string | null; nome: string; classe: string; ponto: string };

const NIVEIS: Record<string, Nivel> = {
  EMERGENCIA: { sigla: 'P1', nome: 'Emergência', classe: 'bg-red-100 text-red-800 ring-red-300', ponto: 'bg-red-500' },
  URGENCIA: { sigla: 'P2', nome: 'Urgência', classe: 'bg-amber-100 text-amber-800 ring-amber-300', ponto: 'bg-amber-400' },
  PRIORIDADE_NAO_URGENTE: {
    sigla: 'P3',
    nome: 'Prioridade não urgente',
    classe: 'bg-emerald-100 text-emerald-800 ring-emerald-300',
    ponto: 'bg-emerald-500',
  },
  // O nome sai do código: o SER só escreve "Prioridade 4". O SERNIT escreve "Não urgente", e
  // esse rótulo dele vence (ver `nivelDaOpcao`).
  CONSULTA_BAIXA_COMPLEXIDADE: {
    sigla: 'P4',
    nome: 'Baixa complexidade',
    classe: 'bg-blue-100 text-blue-800 ring-blue-300',
    ponto: 'bg-blue-500',
  },
  NAO_CLASSIFICADO: {
    sigla: null,
    nome: 'Não classificado',
    classe: 'bg-slate-100 text-slate-700 ring-slate-300',
    ponto: 'bg-slate-400',
  },
};

const SEM_NIVEL = { classe: 'bg-slate-100 text-slate-700 ring-slate-300', ponto: 'bg-slate-400' };

/** "Prioridade 3" — o rótulo do SER, que não diz o que o nível é. */
const SO_NUMERO = /^prioridade\s+\d+$/i;

/**
 * Sigla, nome e cor de uma opção. O nome é o rótulo do próprio sistema quando ele diz alguma
 * coisa; quando é só "Prioridade N" (SER), vem do código. Código desconhecido (o sistema
 * acrescentou um nível) aparece com o rótulo dele, sem cor — nunca some.
 */
export function nivelDaOpcao(valor: string, rotulo?: string | null): Nivel {
  const nivel = NIVEIS[valor];
  const proprio = rotulo?.trim();
  if (!nivel) return { sigla: null, nome: proprio || valor, ...SEM_NIVEL };
  return { ...nivel, nome: proprio && !SO_NUMERO.test(proprio) ? proprio : nivel.nome };
}

export function textoDoNivel(n: Nivel): string {
  return n.sigla ? `${n.sigla} · ${n.nome}` : n.nome;
}

/** O badge de leitura — para mostrar a classificação já escolhida. */
export function BadgeRisco({ valor, rotulo }: { valor: string; rotulo?: string | null }) {
  const n = nivelDaOpcao(valor, rotulo);
  return (
    <span className={`inline-block whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ring-1 ${n.classe}`}>
      {textoDoNivel(n)}
    </span>
  );
}

type SeletorProps = {
  id?: string;
  opcoes: OpcaoRegulacao[];
  valor: string;
  desabilitado?: boolean;
  onChange: (v: string) => void;
};

/**
 * O campo de escolha: um badge por opção, na ordem do sistema. Clicar no escolhido de novo não
 * desmarca — o campo é obrigatório nos dois sistemas, e "voltar a vazio" não tem uso.
 */
export function SeletorRisco({ id, opcoes, valor, desabilitado, onChange }: SeletorProps) {
  // Quando a lista chega unida (destino que não é SER nem SERNIT), "Prioridade 1" (SER) e
  // "Emergência" (SERNIT) são duas opções de MESMO valor — fica um badge só.
  const unicas = opcoes.filter((o, i) => opcoes.findIndex((x) => x.valor === o.valor) === i);

  // Valor que o sistema de destino não tem não fica guardado: trocar o destino de SERNIT para
  // SER com "Não classificado" marcado deixaria no pedido uma opção que o SER não conhece, sem
  // nenhum badge marcado na tela. Volta a vazio e o obrigatório pede de novo. Lista vazia =
  // catálogo ainda carregando (não apaga rascunho); desabilitado = só leitura (não mexe).
  const invalido = !desabilitado && !!valor && opcoes.length > 0 && !opcoes.some((o) => o.valor === valor);
  useEffect(() => {
    if (invalido) onChange('');
  }, [invalido, onChange]);

  return (
    <div id={id} role="radiogroup" className="flex flex-wrap gap-1.5 pt-1">
      {unicas.map((o) => {
        const n = nivelDaOpcao(o.valor, o.rotulo);
        const marcado = valor === o.valor;
        return (
          <button
            key={o.valor}
            type="button"
            role="radio"
            aria-checked={marcado}
            disabled={desabilitado}
            onClick={() => onChange(o.valor)}
            // O rótulo do sistema no title: é por ele que o técnico acha a opção na tela de lá.
            title={o.rotulo}
            className={`whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-medium ring-1 transition disabled:cursor-not-allowed ${
              marcado
                ? `${n.classe} ring-2 shadow-sm`
                : 'bg-white text-slate-500 ring-slate-200 hover:ring-slate-400 disabled:hover:ring-slate-200'
            }`}
          >
            <span className={`mr-1.5 inline-block size-2 rounded-full align-middle ${n.ponto}`} />
            {textoDoNivel(n)}
          </button>
        );
      })}
    </div>
  );
}
