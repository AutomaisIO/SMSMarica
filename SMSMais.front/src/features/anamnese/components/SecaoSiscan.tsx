import { Plus, Trash2 } from 'lucide-react';
import {
  ROTULOS_CIRURGIA,
  ROTULOS_LADO,
  ROTULOS_MAMAS_EXAMINADAS,
  ROTULOS_SIM_NAO_NAO_SABE,
  TIPOS_CIRURGIA_MAMA,
  type ComplementoSiscan,
  type LadoMama,
  type LadoOuAmbas,
  type MamasExaminadasAntes,
  type SimNaoNaoSabe,
  type TipoCirurgiaMama,
} from '@/features/anamnese/types';

/**
 * Seção 7 — as perguntas que existem porque a **requisição do SISCAN** as exige.
 *
 * Não são capricho de formulário: sem elas, a requisição só pode ser gerada
 * respondendo "Não sabe", que o SISCAN aceita e que joga fora informação que a
 * paciente está ali para dar. O ano da última mamografia e as cirurgias só
 * aparecem quando a seção 3 já disse que houve — perguntar ano de coisa que não
 * aconteceu é ruído.
 */
export type SecaoSiscanProps = {
  valor: ComplementoSiscan;
  /** Respostas da seção 3 que decidem o que faz sentido perguntar aqui. */
  jaFezMamografia: boolean;
  jaFezCirurgia: boolean;
  somenteLeitura: boolean;
  aoMudar: (mudanca: Partial<ComplementoSiscan>) => void;
};

const OPCOES_MAMAS: MamasExaminadasAntes[] = ['sim', 'nunca', 'naoSabe'];
const OPCOES_SNN: SimNaoNaoSabe[] = ['sim', 'nao', 'naoSabe'];
const OPCOES_LADO: LadoOuAmbas[] = ['direita', 'esquerda', 'ambas'];

/** Botão de escolha única no estilo do formulário (pílula que marca/desmarca). */
function Opcao<T extends string>({
  valor,
  atual,
  rotulo,
  aoEscolher,
}: {
  valor: T;
  atual: T | null;
  rotulo: string;
  aoEscolher: (v: T | null) => void;
}) {
  const marcada = atual === valor;
  return (
    <button
      type="button"
      onClick={() => aoEscolher(marcada ? null : valor)}
      aria-pressed={marcada}
      className={`rounded-full border px-3 py-1 text-sm transition ${
        marcada
          ? 'border-teal-600 bg-teal-600 font-semibold text-white'
          : 'border-gray-300 bg-white text-gray-700 hover:border-teal-400'
      }`}
    >
      {rotulo}
    </button>
  );
}

function CampoAno({
  rotulo,
  valor,
  aoMudar,
}: {
  rotulo: string;
  valor: string;
  aoMudar: (v: string) => void;
}) {
  return (
    <label className="text-sm text-gray-800">
      {rotulo}
      <input
        type="text"
        inputMode="numeric"
        maxLength={4}
        placeholder="AAAA"
        value={valor}
        // Só dígitos: o SISCAN quer o ano cru, e "2019?" ou "há 5 anos" não passa lá.
        onChange={(e) => aoMudar(e.target.value.replace(/\D/g, '').slice(0, 4))}
        className="ml-2 w-20 rounded-md border border-gray-300 px-2 py-1 text-sm"
      />
    </label>
  );
}

export function SecaoSiscan({
  valor,
  jaFezMamografia,
  jaFezCirurgia,
  somenteLeitura,
  aoMudar,
}: SecaoSiscanProps) {
  function mudarRadioterapia(mudanca: Partial<ComplementoSiscan['radioterapia']>) {
    aoMudar({ radioterapia: { ...valor.radioterapia, ...mudanca } });
  }

  function mudarCirurgia(indice: number, mudanca: Partial<ComplementoSiscan['cirurgias'][number]>) {
    aoMudar({
      cirurgias: valor.cirurgias.map((c, i) => (i === indice ? { ...c, ...mudanca } : c)),
    });
  }

  const lado = valor.radioterapia.lado;
  const mostraAnoDireita = lado === 'direita' || lado === 'ambas';
  const mostraAnoEsquerda = lado === 'esquerda' || lado === 'ambas';

  return (
    <fieldset disabled={somenteLeitura} className="mt-3 space-y-5">
      {/* Mamas já examinadas antes */}
      <div className="border-b border-gray-100 pb-4">
        <p className="text-sm font-medium text-gray-800">
          Antes desta consulta, teve as mamas examinadas por um profissional de saúde?
        </p>
        <div className="mt-2 flex flex-wrap gap-2">
          {OPCOES_MAMAS.map((o) => (
            <Opcao
              key={o}
              valor={o}
              atual={valor.mamasExaminadasAntes}
              rotulo={ROTULOS_MAMAS_EXAMINADAS[o]}
              aoEscolher={(v) => aoMudar({ mamasExaminadasAntes: v })}
            />
          ))}
        </div>
      </div>

      {/* Radioterapia */}
      <div className="border-b border-gray-100 pb-4">
        <p className="text-sm font-medium text-gray-800">
          Fez radioterapia na mama ou no plastrão?
        </p>
        <div className="mt-2 flex flex-wrap gap-2">
          {OPCOES_SNN.map((o) => (
            <Opcao
              key={o}
              valor={o}
              atual={valor.radioterapia.resposta}
              rotulo={ROTULOS_SIM_NAO_NAO_SABE[o]}
              // Trocar para Não/Não sabe limpa lado e anos: deixar resíduo de uma
              // resposta anterior é o jeito clássico de mandar dado errado adiante.
              aoEscolher={(v) =>
                mudarRadioterapia(
                  v === 'sim'
                    ? { resposta: v }
                    : { resposta: v, lado: null, anoDireita: '', anoEsquerda: '' },
                )
              }
            />
          ))}
        </div>
        {valor.radioterapia.resposta === 'sim' ? (
          <div className="mt-3 space-y-3 rounded-md bg-gray-50 p-3">
            <div>
              <p className="text-sm text-gray-700">Em qual mama?</p>
              <div className="mt-2 flex flex-wrap gap-2">
                {OPCOES_LADO.map((o) => (
                  <Opcao
                    key={o}
                    valor={o}
                    atual={valor.radioterapia.lado}
                    rotulo={ROTULOS_LADO[o]}
                    aoEscolher={(v) =>
                      mudarRadioterapia({
                        lado: v,
                        anoDireita: v === 'esquerda' ? '' : valor.radioterapia.anoDireita,
                        anoEsquerda: v === 'direita' ? '' : valor.radioterapia.anoEsquerda,
                      })
                    }
                  />
                ))}
              </div>
            </div>
            {mostraAnoDireita || mostraAnoEsquerda ? (
              <div className="flex flex-wrap gap-6">
                {mostraAnoDireita ? (
                  <CampoAno
                    rotulo="Ano (direita):"
                    valor={valor.radioterapia.anoDireita}
                    aoMudar={(v) => mudarRadioterapia({ anoDireita: v })}
                  />
                ) : null}
                {mostraAnoEsquerda ? (
                  <CampoAno
                    rotulo="Ano (esquerda):"
                    valor={valor.radioterapia.anoEsquerda}
                    aoMudar={(v) => mudarRadioterapia({ anoEsquerda: v })}
                  />
                ) : null}
              </div>
            ) : null}
          </div>
        ) : null}
      </div>

      {/* Ano da última mamografia — só se a seção 3 disse que já fez */}
      <div className="border-b border-gray-100 pb-4">
        {jaFezMamografia ? (
          <CampoAno
            rotulo="Em que ano foi a última mamografia?"
            valor={valor.anoUltimaMamografia}
            aoMudar={(v) => aoMudar({ anoUltimaMamografia: v })}
          />
        ) : (
          <p className="text-sm text-gray-400">
            Ano da última mamografia — aparece quando a seção 3 marcar que já realizou mamografia.
          </p>
        )}
      </div>

      {/* Cirurgias — só se a seção 3 disse que já fez */}
      <div>
        <p className="text-sm font-medium text-gray-800">Cirurgias de mama realizadas</p>
        {jaFezCirurgia ? (
          <>
            <p className="mt-0.5 text-xs text-gray-500">
              O SISCAN guarda um ano por tipo e por mama. Acrescente uma linha para cada cirurgia.
            </p>
            <div className="mt-2 space-y-2">
              {valor.cirurgias.map((c, i) => (
                <div key={i} className="flex flex-wrap items-center gap-2">
                  <select
                    value={c.tipo}
                    onChange={(e) =>
                      mudarCirurgia(i, { tipo: e.target.value as TipoCirurgiaMama })
                    }
                    className="min-w-[16rem] flex-1 rounded-md border border-gray-300 px-2 py-1.5 text-sm"
                  >
                    {TIPOS_CIRURGIA_MAMA.map(([chave]) => (
                      <option key={chave} value={chave}>
                        {ROTULOS_CIRURGIA[chave]}
                      </option>
                    ))}
                  </select>
                  <select
                    value={c.lado}
                    onChange={(e) => mudarCirurgia(i, { lado: e.target.value as LadoMama })}
                    className="rounded-md border border-gray-300 px-2 py-1.5 text-sm"
                  >
                    <option value="direita">Mama direita</option>
                    <option value="esquerda">Mama esquerda</option>
                  </select>
                  <input
                    type="text"
                    inputMode="numeric"
                    maxLength={4}
                    placeholder="Ano"
                    value={c.ano}
                    onChange={(e) =>
                      mudarCirurgia(i, { ano: e.target.value.replace(/\D/g, '').slice(0, 4) })
                    }
                    className="w-20 rounded-md border border-gray-300 px-2 py-1.5 text-sm"
                  />
                  {somenteLeitura ? null : (
                    <button
                      type="button"
                      onClick={() =>
                        aoMudar({ cirurgias: valor.cirurgias.filter((_, j) => j !== i) })
                      }
                      title="Remover esta cirurgia"
                      className="rounded-md p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  )}
                </div>
              ))}
            </div>
            {somenteLeitura ? null : (
              <button
                type="button"
                onClick={() =>
                  aoMudar({
                    cirurgias: [
                      ...valor.cirurgias,
                      { tipo: TIPOS_CIRURGIA_MAMA[0][0], lado: 'direita' as LadoMama, ano: '' },
                    ],
                  })
                }
                className="mt-2 inline-flex items-center gap-1 rounded-md border border-dashed border-gray-300 px-3 py-1.5 text-sm text-gray-600 hover:border-teal-400 hover:text-teal-700"
              >
                <Plus className="h-4 w-4" /> Acrescentar cirurgia
              </button>
            )}
          </>
        ) : (
          <p className="mt-0.5 text-sm text-gray-400">
            Aparece quando a seção 3 marcar que já realizou cirurgia mamária.
          </p>
        )}
      </div>

      {/* Responsável pela requisição */}
      <div className="border-t border-gray-100 pt-4">
        <p className="text-sm font-medium text-gray-800">Responsável pela requisição no SISCAN</p>
        {valor.responsavel ? (
          <p className="mt-1 text-sm text-gray-700">
            {valor.responsavel.nome}{' '}
            <span className="font-mono text-xs text-gray-500">
              · CNS {valor.responsavel.cns}
            </span>
          </p>
        ) : (
          <p className="mt-1 text-sm text-gray-500">
            Escolhido ao gerar a requisição. A lista de profissionais é do próprio SISCAN — ele
            identifica o responsável pelo <strong>CNS</strong>, que não existe no nosso cadastro
            nem na ficha do SISREG, e só aparece depois que a unidade e o tipo de mamografia são
            informados lá.
          </p>
        )}
      </div>
    </fieldset>
  );
}
