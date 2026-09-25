import {
  RISCO_ELEVADO_SISCAN,
  ROTULOS_CIRURGIA,
  ROTULOS_LADO,
  ROTULOS_MAMAS_EXAMINADAS,
  ROTULOS_SIM_NAO_NAO_SABE,
  TIPOS_CIRURGIA_MAMA,
  type CirurgiaMama,
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
 * paciente está ali para dar. O ano da última mamografia só aparece quando a
 * seção 3 já disse que houve — perguntar ano de coisa que não aconteceu é ruído.
 * As cirurgias (#106) e o risco elevado (#139) também são do SISCAN, mas moram
 * nas seções 3 e 5, junto das perguntas a que pertencem.
 */
export type SecaoSiscanProps = {
  valor: ComplementoSiscan;
  /** Resposta da seção 3 que decide se faz sentido perguntar o ano da última mamografia. */
  jaFezMamografia: boolean;
  somenteLeitura: boolean;
  aoMudar: (mudanca: Partial<ComplementoSiscan>) => void;
};

const OPCOES_MAMAS: MamasExaminadasAntes[] = ['sim', 'nunca', 'naoSabe'];
const OPCOES_SNN: SimNaoNaoSabe[] = ['sim', 'nao', 'naoSabe'];
const OPCOES_LADO: LadoOuAmbas[] = ['direita', 'esquerda', 'ambas'];

/** Botão de escolha única no estilo do formulário (pílula que marca/desmarca). */
export function Opcao<T extends string>({
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
  somenteLeitura,
  aoMudar,
}: SecaoSiscanProps) {
  function mudarRadioterapia(mudanca: Partial<ComplementoSiscan['radioterapia']>) {
    aoMudar({ radioterapia: { ...valor.radioterapia, ...mudanca } });
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

      {/* Cirurgias: moraram aqui até o ticket #106 — agora a tabela abre na seção 3, logo
          abaixo da pergunta, que é onde quem preenche está pensando nelas. */}
      <p className="text-sm text-gray-500">
        Cirurgias de mama: informadas na seção 3, na tabela que abre em &quot;Já realizou cirurgia
        mamária?&quot;.
      </p>

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

/**
 * "Apresenta risco elevado para câncer de mama?" exatamente como o SISCAN pergunta (ticket
 * #139): as três opções dele e o quadro "RISCO ELEVADO SÃO:" com o texto dele. Fica na seção 5,
 * ao lado da avaliação de risco nossa — que continua. Respondida, é esta que vai ao SISCAN.
 */
export function PerguntaRiscoElevadoSiscan({
  valor,
  somenteLeitura,
  aoMudar,
}: {
  valor: SimNaoNaoSabe | null;
  somenteLeitura: boolean;
  aoMudar: (v: SimNaoNaoSabe | null) => void;
}) {
  return (
    <fieldset disabled={somenteLeitura} className="rounded-md border border-gray-300 p-3">
      <legend className="px-1 text-xs font-semibold uppercase tracking-wide text-gray-600">
        Apresenta risco elevado para câncer de mama? <span className="text-red-500">*</span>
        <span className="ml-2 rounded bg-teal-100 px-1.5 py-0.5 text-[10px] font-bold text-teal-700">
          SISCAN
        </span>
      </legend>
      <div className="flex flex-wrap gap-2">
        {OPCOES_SNN.map((o) => (
          <Opcao
            key={o}
            valor={o}
            atual={valor}
            rotulo={ROTULOS_SIM_NAO_NAO_SABE[o]}
            aoEscolher={aoMudar}
          />
        ))}
      </div>
      <div className="mt-3 rounded-md border border-gray-200 p-2.5">
        <p className="text-xs font-semibold uppercase text-gray-500">Risco elevado são:</p>
        <ul className="mt-1 space-y-1 text-sm text-gray-700">
          {RISCO_ELEVADO_SISCAN.map((linha) => (
            <li key={linha} className={linha.startsWith('-') ? 'pl-4' : undefined}>
              {linha}
            </li>
          ))}
        </ul>
      </div>
    </fieldset>
  );
}

/**
 * Tabela de cirurgias de mama (ticket #106): abre na seção 3, logo abaixo de "Já realizou
 * cirurgia mamária?", no formato do papel — tipo × mama direita × mama esquerda, com o ano.
 * Os tipos são os 13 do SISCAN, e não os do papel antigo, porque é esta resposta que vai na
 * requisição e o SISCAN só aceita os dele (um "retirada de nódulo" não tem par exato lá).
 */
export function TabelaCirurgiasSiscan({
  cirurgias,
  somenteLeitura,
  aoMudar,
}: {
  cirurgias: CirurgiaMama[];
  somenteLeitura: boolean;
  aoMudar: (cirurgias: CirurgiaMama[]) => void;
}) {
  const achar = (tipo: TipoCirurgiaMama, lado: LadoMama) =>
    cirurgias.find((c) => c.tipo === tipo && c.lado === lado);

  function alternar(tipo: TipoCirurgiaMama, lado: LadoMama) {
    aoMudar(
      achar(tipo, lado)
        ? cirurgias.filter((c) => !(c.tipo === tipo && c.lado === lado))
        : [...cirurgias, { tipo, lado, ano: '' }],
    );
  }

  function mudarAno(tipo: TipoCirurgiaMama, lado: LadoMama, ano: string) {
    // Só dígitos: o SISCAN quer o ano cru, e "2019?" ou "há 5 anos" não passa lá.
    const limpo = ano.replace(/\D/g, '').slice(0, 4);
    aoMudar(cirurgias.map((c) => (c.tipo === tipo && c.lado === lado ? { ...c, ano: limpo } : c)));
  }

  // Função que devolve JSX (e não componente): um componente declarado aqui dentro seria um
  // tipo novo a cada render, e o campo do ano perderia o foco a cada tecla.
  function celula(tipo: TipoCirurgiaMama, lado: LadoMama) {
    const item = achar(tipo, lado);
    return (
      <td key={lado} className="border border-gray-300 px-2 py-1">
        <div className="flex items-center gap-2">
          <input
            type="checkbox"
            checked={Boolean(item)}
            onChange={() => alternar(tipo, lado)}
            aria-label={`${ROTULOS_CIRURGIA[tipo]} — mama ${lado}`}
            className="h-4 w-4 rounded border-gray-300"
          />
          {item ? (
            <input
              type="text"
              inputMode="numeric"
              maxLength={4}
              placeholder="Ano"
              value={item.ano}
              onChange={(e) => mudarAno(tipo, lado, e.target.value)}
              aria-label={`Ano — ${ROTULOS_CIRURGIA[tipo]}, mama ${lado}`}
              className="w-16 rounded-md border border-gray-300 px-1.5 py-0.5 text-sm"
            />
          ) : null}
        </div>
      </td>
    );
  }

  return (
    <fieldset disabled={somenteLeitura} className="mt-2 overflow-x-auto">
      <table className="w-full border-collapse text-sm">
        <thead>
          <tr className="bg-gray-100 text-xs font-bold uppercase text-gray-700">
            <th className="border border-gray-300 px-2 py-1.5 text-left">Cirurgia</th>
            <th className="border border-gray-300 px-2 py-1.5">D · quando</th>
            <th className="border border-gray-300 px-2 py-1.5">E · quando</th>
          </tr>
        </thead>
        <tbody>
          {TIPOS_CIRURGIA_MAMA.map(([tipo, rotulo]) => (
            <tr key={tipo}>
              <td className="border border-gray-300 px-2 py-1 text-gray-800">{rotulo}</td>
              {celula(tipo, 'direita')}
              {celula(tipo, 'esquerda')}
            </tr>
          ))}
        </tbody>
      </table>
      <p className="mt-1 text-xs text-gray-500">
        Marque a mama operada e o ano. Os tipos são os do SISCAN, que guarda um ano por tipo e por
        mama.
      </p>
    </fieldset>
  );
}
