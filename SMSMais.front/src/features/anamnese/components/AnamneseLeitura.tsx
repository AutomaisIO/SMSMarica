import { DiagramaMamas } from '@/features/anamnese/components/DiagramaMamas';
import {
  CRITERIOS_RISCO,
  PERGUNTAS_HISTORICO,
  ROTULOS_CIRURGIA,
  ROTULOS_LADO,
  ROTULOS_MAMAS_EXAMINADAS,
  ROTULOS_SIM_NAO_NAO_SABE,
  ROTULOS_SINTOMAS,
  SINTOMAS_QUEIXA,
  type AnamneseMamografiaConteudo,
} from '@/features/anamnese/types';

/** Cabeçalho de seção no estilo do formulário em papel (pílula colorida). */
function TituloSecao({ numero, titulo, cor }: { numero: number; titulo: string; cor: string }) {
  return (
    <div className={`inline-flex items-center gap-2 rounded-full px-4 py-1.5 text-sm font-bold text-white ${cor}`}>
      <span className="flex h-5 w-5 items-center justify-center rounded-full bg-white/25 text-xs">
        {numero}
      </span>
      {titulo}
    </div>
  );
}

/** Marcação Sim/Não escolhida (sem checkbox) — só a resposta selecionada. */
function MarcaSimNao({ valor }: { valor: boolean | null }) {
  if (valor === true) {
    return (
      <span className="rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-semibold text-green-700">
        Sim
      </span>
    );
  }
  if (valor === false) {
    return (
      <span className="rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-semibold text-gray-600">
        Não
      </span>
    );
  }
  return <span className="text-xs italic text-gray-400">não informado</span>;
}

/** Conteúdo de um campo de texto, em div de linha fina (não em input). */
function TextoLeitura({ label, valor }: { label: string; valor: string }) {
  const v = valor?.trim();
  if (!v) return null;
  return (
    <div className="mt-2">
      <span className="text-xs font-medium text-gray-500">{label}</span>
      <div className="mt-0.5 whitespace-pre-wrap rounded-md border border-gray-200 px-3 py-2 text-sm text-gray-800">
        {v}
      </div>
    </div>
  );
}

/**
 * Visualização (somente leitura) da anamnese já preenchida: em vez de
 * checkboxes/inputs, mostra a frase com a marcação selecionada e os campos de
 * texto apenas com o conteúdo, dentro de uma div de linha fina.
 */
export function AnamneseLeitura({ conteudo }: { conteudo: AnamneseMamografiaConteudo }) {
  const { avaliacaoClinica, historicoClinico, queixas, avaliacaoRisco, saudeReprodutiva, siscan } =
    conteudo;
  const dataMenstruacao = saudeReprodutiva?.aindaMenstrua?.dataUltimaMenstruacao?.trim();
  // Anos da radioterapia, montados só com o que foi informado.
  const anosRadioterapia = [
    siscan?.radioterapia?.anoDireita?.trim() ? `D ${siscan.radioterapia.anoDireita.trim()}` : null,
    siscan?.radioterapia?.anoEsquerda?.trim() ? `E ${siscan.radioterapia.anoEsquerda.trim()}` : null,
  ]
    .filter(Boolean)
    .join(' · ');

  const achados: string[] = [];
  if (avaliacaoClinica.semAlteracoes) achados.push('Sem alterações');
  if (avaliacaoClinica.alteracoesPalpaveis) achados.push('Alterações palpáveis');

  // Sintomas com pelo menos um lado marcado.
  const sintomasMarcados = SINTOMAS_QUEIXA.map((s) => {
    const lados = queixas.sintomas[s];
    const onde = [lados.direita ? 'Direita' : null, lados.esquerda ? 'Esquerda' : null].filter(
      Boolean,
    ) as string[];
    return onde.length > 0 ? { s, onde } : null;
  }).filter(Boolean) as { s: (typeof SINTOMAS_QUEIXA)[number]; onde: string[] }[];

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
      {/* 2. Avaliação clínica */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <TituloSecao numero={2} titulo="AVALIAÇÃO CLÍNICA PELO PROFISSIONAL" cor="bg-cyan-600" />
        <div className="mt-4">
          <DiagramaMamas
            marcacoes={avaliacaoClinica.marcacoes}
            tracos={avaliacaoClinica.tracos ?? []}
            aoMudar={() => {}}
            aoMudarTracos={() => {}}
            somenteLeitura
          />
        </div>
        <div className="mt-4">
          <span className="text-sm font-semibold text-cyan-700">Achados clínicos</span>
          {achados.length > 0 ? (
            <div className="mt-1.5 flex flex-wrap gap-2">
              {achados.map((a) => (
                <span
                  key={a}
                  className="rounded-full bg-cyan-100 px-2.5 py-0.5 text-xs font-semibold text-cyan-700"
                >
                  {a}
                </span>
              ))}
            </div>
          ) : (
            <p className="mt-1 text-xs italic text-gray-400">Nenhum achado marcado.</p>
          )}
          <TextoLeitura label="Especificar" valor={avaliacaoClinica.especificar} />
          <TextoLeitura label="Outras observações" valor={avaliacaoClinica.outrasObservacoes} />
        </div>
      </section>

      {/* 3. Histórico clínico */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <TituloSecao numero={3} titulo="HISTÓRICO CLÍNICO" cor="bg-rose-500" />
        <div className="mt-3 space-y-2">
          {PERGUNTAS_HISTORICO.map(([chave, rotulo]) => {
            const r = historicoClinico[chave];
            return (
              <div key={chave} className="border-b border-gray-100 pb-2">
                <div className="flex items-center justify-between gap-3">
                  <span className="text-sm text-gray-800">{rotulo}</span>
                  <MarcaSimNao valor={r.resposta} />
                </div>
                {r.resposta === true && r.observacao.trim() ? (
                  <div className="mt-1 whitespace-pre-wrap rounded-md border border-gray-200 px-2.5 py-1.5 text-xs text-gray-700">
                    {r.observacao.trim()}
                  </div>
                ) : null}
              </div>
            );
          })}
        </div>
        <TextoLeitura label="Outras informações relevantes" valor={historicoClinico.outrasInformacoes} />
      </section>

      {/* 4. Queixas referidas */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <TituloSecao numero={4} titulo="QUEIXAS REFERIDAS" cor="bg-sky-600" />
        {sintomasMarcados.length > 0 ? (
          <ul className="mt-3 space-y-1.5">
            {sintomasMarcados.map(({ s, onde }) => (
              <li key={s} className="flex items-center justify-between gap-3 text-sm text-gray-800">
                <span>
                  {ROTULOS_SINTOMAS[s]}
                  {s === 'outro' && queixas.outroTexto.trim() ? (
                    <span className="text-gray-500"> — {queixas.outroTexto.trim()}</span>
                  ) : null}
                </span>
                <span className="flex gap-1.5">
                  {onde.map((lado) => (
                    <span
                      key={lado}
                      className="rounded-full bg-sky-100 px-2.5 py-0.5 text-xs font-semibold text-sky-700"
                    >
                      {lado}
                    </span>
                  ))}
                </span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="mt-3 text-xs italic text-gray-400">Nenhuma queixa referida.</p>
        )}
        <TextoLeitura label="Descrever" valor={queixas.descrever} />
      </section>

      {/* 5. Avaliação de risco */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <TituloSecao numero={5} titulo="AVALIAÇÃO DE RISCO" cor="bg-orange-500" />
        <div className="mt-3 space-y-2">
          {CRITERIOS_RISCO.map(([chave, rotulo]) => (
            <div
              key={chave}
              className="flex items-center justify-between gap-3 border-b border-gray-100 pb-2"
            >
              <span className="text-sm text-gray-800">{rotulo}</span>
              <MarcaSimNao valor={avaliacaoRisco[chave]} />
            </div>
          ))}
        </div>
        <div className="mt-4">
          <span className="text-sm font-semibold text-orange-600">Classificação do Risco</span>
          <div className="mt-2">
            {avaliacaoRisco.classificacao ? (
              <span
                className={`rounded-md border px-4 py-1.5 text-sm font-medium ${
                  avaliacaoRisco.classificacao === 'Baixo'
                    ? 'border-green-300 bg-green-50 text-green-800'
                    : avaliacaoRisco.classificacao === 'Moderado'
                      ? 'border-amber-300 bg-amber-50 text-amber-800'
                      : 'border-red-300 bg-red-50 text-red-800'
                }`}
              >
                {avaliacaoRisco.classificacao}
              </span>
            ) : (
              <span className="text-xs italic text-gray-400">não informada</span>
            )}
          </div>
        </div>
      </section>

      {/* 6. Saúde reprodutiva */}
      <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <TituloSecao numero={6} titulo="SAÚDE REPRODUTIVA" cor="bg-fuchsia-600" />
        <div className="mt-3 space-y-2">
          <div className="border-b border-gray-100 pb-2">
            <div className="flex items-center justify-between gap-3">
              <span className="text-sm text-gray-800">Faz uso de anticoncepcional?</span>
              <MarcaSimNao valor={saudeReprodutiva?.usoAnticoncepcional?.resposta ?? null} />
            </div>
            {saudeReprodutiva?.usoAnticoncepcional?.resposta === true &&
            saudeReprodutiva.usoAnticoncepcional.observacao.trim() ? (
              <div className="mt-1 rounded-md border border-gray-200 px-2.5 py-1.5 text-xs text-gray-700">
                Qual: {saudeReprodutiva.usoAnticoncepcional.observacao.trim()}
              </div>
            ) : null}
          </div>
          <div className="border-b border-gray-100 pb-2">
            <div className="flex items-center justify-between gap-3">
              <span className="text-sm text-gray-800">Ainda menstrua?</span>
              <MarcaSimNao valor={saudeReprodutiva?.aindaMenstrua?.resposta ?? null} />
            </div>
            {saudeReprodutiva?.aindaMenstrua?.resposta === true && dataMenstruacao ? (
              <div className="mt-1 rounded-md border border-gray-200 px-2.5 py-1.5 text-xs text-gray-700">
                Última menstruação:{' '}
                {new Date(dataMenstruacao).toLocaleDateString('pt-BR', { timeZone: 'UTC' })}
              </div>
            ) : null}
          </div>
          <div className="flex items-center justify-between gap-3">
            <span className="text-sm text-gray-800">Número de filhos</span>
            {saudeReprodutiva?.numeroFilhos != null ? (
              <span className="rounded-full bg-fuchsia-100 px-2.5 py-0.5 text-xs font-semibold text-fuchsia-700">
                {saudeReprodutiva.numeroFilhos}
              </span>
            ) : (
              <span className="text-xs italic text-gray-400">não informado</span>
            )}
          </div>
        </div>
      </section>

      {/* 7. Requisição do SISCAN — v2. Anamnese v1 não tem o bloco: some inteira. */}
      {siscan ? (
        <section className="rounded-lg border border-gray-200 bg-white p-3">
          <TituloSecao numero={7} titulo="REQUISIÇÃO DO SISCAN" cor="bg-teal-600" />
          <div className="mt-2 space-y-2">
            <div className="flex items-center justify-between gap-3">
              <span className="text-sm text-gray-800">Mamas já examinadas antes desta consulta</span>
              {siscan.mamasExaminadasAntes ? (
                <span className="rounded-full bg-teal-100 px-2.5 py-0.5 text-xs font-semibold text-teal-700">
                  {ROTULOS_MAMAS_EXAMINADAS[siscan.mamasExaminadasAntes]}
                </span>
              ) : (
                <span className="text-xs italic text-gray-400">não informado</span>
              )}
            </div>
            <div className="flex items-center justify-between gap-3">
              <span className="text-sm text-gray-800">Radioterapia na mama ou no plastrão</span>
              {siscan.radioterapia?.resposta ? (
                <span className="rounded-full bg-teal-100 px-2.5 py-0.5 text-xs font-semibold text-teal-700">
                  {ROTULOS_SIM_NAO_NAO_SABE[siscan.radioterapia.resposta]}
                  {siscan.radioterapia.resposta === 'sim' && siscan.radioterapia.lado
                    ? ` · ${ROTULOS_LADO[siscan.radioterapia.lado]}`
                    : ''}
                  {anosRadioterapia ? ` · ${anosRadioterapia}` : ''}
                </span>
              ) : (
                <span className="text-xs italic text-gray-400">não informado</span>
              )}
            </div>
            {siscan.anoUltimaMamografia?.trim() ? (
              <div className="flex items-center justify-between gap-3">
                <span className="text-sm text-gray-800">Ano da última mamografia</span>
                <span className="rounded-full bg-teal-100 px-2.5 py-0.5 text-xs font-semibold text-teal-700">
                  {siscan.anoUltimaMamografia.trim()}
                </span>
              </div>
            ) : null}
            {siscan.cirurgias?.length ? (
              <div>
                <span className="text-sm text-gray-800">Cirurgias de mama</span>
                <ul className="mt-1 space-y-0.5">
                  {siscan.cirurgias.map((c, i) => (
                    <li key={i} className="text-xs text-gray-600">
                      {ROTULOS_CIRURGIA[c.tipo]} · {c.lado === 'direita' ? 'direita' : 'esquerda'}
                      {c.ano?.trim() ? ` · ${c.ano.trim()}` : ''}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
            {siscan.responsavel ? (
              <div className="flex items-center justify-between gap-3">
                <span className="text-sm text-gray-800">Responsável pela requisição</span>
                <span className="text-xs text-gray-600">
                  {siscan.responsavel.nome}{' '}
                  <span className="font-mono text-gray-400">CNS {siscan.responsavel.cns}</span>
                </span>
              </div>
            ) : null}
          </div>
        </section>
      ) : null}
    </div>
  );
}
