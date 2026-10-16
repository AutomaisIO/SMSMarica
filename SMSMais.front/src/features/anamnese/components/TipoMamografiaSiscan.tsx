import type { ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Opcao } from '@/features/anamnese/components/SecaoSiscan';
import {
  ACHADOS_RADIOLOGICOS,
  CATEGORIAS_BIRADS,
  INDICACOES_DIAGNOSTICA,
  REGIOES_MAMA,
  ROTULOS_LADO,
  ROTULOS_POPULACAO,
  ROTULOS_TIPO_MAMOGRAFIA,
  diagnosticaVazia,
  indicacaoPreenchida,
  indicacaoVazia,
  populacaoPelosCriterios,
  riscoElevadoEfetivo,
  tipoPelaIdade,
  type AchadosClinicosMama,
  type AnamneseMamografiaConteudo,
  type CategoriaBiRads,
  type ComplementoSiscan,
  type DescargaPapilar,
  type DiagnosticaSiscan,
  type GradeAchados,
  type IndicacaoDiagnostica,
  type LadoMama,
  type LadoOuAmbas,
  type LinfonodoPalpavel,
  type PopulacaoRastreamento,
  type RegiaoMama,
  type TipoMamografia,
} from '@/features/anamnese/types';

/**
 * O bloco "TIPO DE MAMOGRAFIA" da requisição do SISCAN, com a mesma dinâmica da tela deles
 * (16/10/2026): Rastreamento abre a população; Diagnóstica abre 6 indicações, e cada indicação
 * abre o seu sub-bloco. Até aqui o tipo saía só da idade e a diagnóstica ia vazia — o SISCAN
 * recusava. O mapa da tela está em Automais.SISCAN/docs/FLUXO-NOVA-REQUISICAO.md §6b-2.
 *
 * Sem escolha, o tipo segue a régua da idade e a população segue os critérios da seção 5 — e a tela
 * diz isso. O servidor resolve igual (SiscanRequisicaoMapper), então o que se vê é o que vai.
 */
export type TipoMamografiaSiscanProps = {
  conteudo: AnamneseMamografiaConteudo;
  idade: number | null;
  somenteLeitura: boolean;
  aoMudar: (mudanca: Partial<ComplementoSiscan>) => void;
};

const OPCOES_TIPO: TipoMamografia[] = ['diagnostica', 'rastreamento'];
const OPCOES_POPULACAO: PopulacaoRastreamento[] = ['alvo', 'riscoElevado', 'jaTratada'];
const OPCOES_LADO: LadoOuAmbas[] = ['direita', 'esquerda', 'ambas'];
const LADOS: [LadoMama, string][] = [
  ['direita', 'Mama direita'],
  ['esquerda', 'Mama esquerda'],
];

// Texto do próprio SISCAN ao desmarcar "Achados no exame clínico".
const AVISO_PERDA = 'As informações da mamografia diagnóstica desta indicação serão perdidas. Deseja continuar?';

/** Legenda no estilo da tela do SISCAN — a mesma de "Apresenta risco elevado". */
function Legenda({ texto }: { texto: string }) {
  return (
    <legend className="px-1 text-xs font-semibold uppercase tracking-wide text-gray-600">
      {texto} <span className="text-red-500">*</span>
      <span className="ml-2 rounded bg-teal-100 px-1.5 py-0.5 text-[10px] font-bold text-teal-700">SISCAN</span>
    </legend>
  );
}

function Aviso({ children }: { children: ReactNode }) {
  return (
    <p className="mt-2 flex items-start gap-1.5 text-sm text-amber-800">
      <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
      <span>{children}</span>
    </p>
  );
}

/** Pílula de marcar várias (as regiões, os linfonodos). */
function Chip({
  marcado,
  rotulo,
  titulo,
  aoAlternar,
}: {
  marcado: boolean;
  rotulo: string;
  titulo?: string;
  aoAlternar: () => void;
}) {
  return (
    <button
      type="button"
      onClick={aoAlternar}
      aria-pressed={marcado}
      title={titulo}
      className={`rounded-md border px-2 py-0.5 text-xs transition ${
        marcado
          ? 'border-teal-600 bg-teal-600 font-semibold text-white'
          : 'border-gray-300 bg-white text-gray-700 hover:border-teal-400'
      }`}
    >
      {rotulo}
    </button>
  );
}

function alternar<T>(lista: T[], item: T): T[] {
  return lista.includes(item) ? lista.filter((i) => i !== item) : [...lista, item];
}

/** "Achados no exame clínico" de uma mama: o que a tela do SISCAN pede, por lado. */
function AchadosDaMama({
  titulo,
  valor,
  aoMudar,
}: {
  titulo: string;
  valor: AchadosClinicosMama;
  aoMudar: (v: AchadosClinicosMama) => void;
}) {
  const regioes = (campo: 'nodulo' | 'espessamento', rotulo: string) => (
    <div>
      <p className="text-xs font-medium text-gray-600">{rotulo} — localização</p>
      <div className="mt-1 flex flex-wrap gap-1">
        {REGIOES_MAMA.map(([sigla, nome]) => (
          <Chip
            key={sigla}
            rotulo={sigla}
            titulo={nome}
            marcado={valor[campo].includes(sigla)}
            aoAlternar={() => aoMudar({ ...valor, [campo]: alternar<RegiaoMama>(valor[campo], sigla) })}
          />
        ))}
      </div>
    </div>
  );

  return (
    <div className="space-y-2.5 rounded-md border border-gray-200 bg-white p-3">
      <p className="text-sm font-semibold text-gray-800">{titulo}</p>
      <label className="flex items-center gap-2 text-sm text-gray-800">
        <input
          type="checkbox"
          checked={valor.lesaoPapilar}
          onChange={() => aoMudar({ ...valor, lesaoPapilar: !valor.lesaoPapilar })}
          className="h-4 w-4 rounded border-gray-300"
        />
        Lesão papilar
      </label>
      <div>
        <p className="text-xs font-medium text-gray-600">Descarga papilar espontânea</p>
        <div className="mt-1 flex flex-wrap gap-2">
          {(['cristalina', 'hemorragica'] as DescargaPapilar[]).map((d) => (
            <Opcao
              key={d}
              valor={d}
              atual={valor.descargaPapilar}
              rotulo={d === 'cristalina' ? 'Cristalina' : 'Hemorrágica'}
              aoEscolher={(v) => aoMudar({ ...valor, descargaPapilar: v })}
            />
          ))}
        </div>
      </div>
      {regioes('nodulo', 'Nódulo')}
      {regioes('espessamento', 'Espessamento')}
      <div>
        <p className="text-xs font-medium text-gray-600">Linfonodo palpável</p>
        <div className="mt-1 flex flex-wrap gap-1">
          {(['axilar', 'supraclavicular'] as LinfonodoPalpavel[]).map((l) => (
            <Chip
              key={l}
              rotulo={l === 'axilar' ? 'Axilar' : 'Supraclavicular'}
              marcado={valor.linfonodo.includes(l)}
              aoAlternar={() => aoMudar({ ...valor, linfonodo: alternar(valor.linfonodo, l) })}
            />
          ))}
        </div>
      </div>
    </div>
  );
}

/** A grade achado × mama que três indicações repetem (controle radiológico, câncer, pós-biópsia). */
function GradeDeAchados({ valor, aoMudar }: { valor: GradeAchados; aoMudar: (v: GradeAchados) => void }) {
  return (
    <table className="w-full max-w-md border-collapse text-sm">
      <thead>
        <tr className="bg-gray-100 text-xs font-bold uppercase text-gray-700">
          <th className="border border-gray-300 px-2 py-1 text-left">Achado</th>
          <th className="border border-gray-300 px-2 py-1">Direita</th>
          <th className="border border-gray-300 px-2 py-1">Esquerda</th>
        </tr>
      </thead>
      <tbody>
        {ACHADOS_RADIOLOGICOS.map(([chave, rotulo]) => (
          <tr key={chave}>
            <td className="border border-gray-300 px-2 py-1 text-gray-800">{rotulo}</td>
            {LADOS.map(([lado]) => (
              <td key={lado} className="border border-gray-300 px-2 py-1 text-center">
                <input
                  type="checkbox"
                  checked={valor[chave][lado]}
                  onChange={() =>
                    aoMudar({ ...valor, [chave]: { ...valor[chave], [lado]: !valor[chave][lado] } })
                  }
                  aria-label={`${rotulo} — mama ${lado}`}
                  className="h-4 w-4 rounded border-gray-300"
                />
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export function TipoMamografiaSiscan({ conteudo, idade, somenteLeitura, aoMudar }: TipoMamografiaSiscanProps) {
  const siscan = conteudo.siscan;
  const sugerido = tipoPelaIdade(idade);
  const tipo = siscan.tipoMamografia ?? sugerido;
  const risco = riscoElevadoEfetivo(conteudo);
  // Na tela do SISCAN a "risco elevado" só habilita com "Apresenta risco elevado" = Sim.
  const podeRiscoElevado = risco === 'sim';
  const populacaoEscolhida = siscan.populacaoRastreamento ?? populacaoPelosCriterios(conteudo);
  const populacao = populacaoEscolhida === 'riscoElevado' && !podeRiscoElevado ? null : populacaoEscolhida;
  const diagnostica = siscan.diagnostica;
  const algumaIndicacao = INDICACOES_DIAGNOSTICA.some(([chave]) => diagnostica[chave].marcada);

  function trocarTipo(novo: TipoMamografia | null) {
    const efetivo = novo ?? sugerido;
    if (efetivo !== 'diagnostica' && algumaIndicacao && !window.confirm(AVISO_PERDA)) return;
    aoMudar({
      tipoMamografia: novo,
      // Trocar de tipo limpa o bloco do outro: resíduo de uma resposta anterior é o jeito clássico
      // de mandar dado errado adiante (e a tela do SISCAN redesenha o bloco do zero).
      populacaoRastreamento: efetivo === 'rastreamento' ? siscan.populacaoRastreamento : null,
      diagnostica: efetivo === 'diagnostica' ? diagnostica : diagnosticaVazia(),
    });
  }

  /** Mexeu dentro do bloco: o tipo que está na tela fica escolhido — não muda se a idade mudar. */
  function mudarDentro(mudanca: Partial<ComplementoSiscan>) {
    aoMudar({ tipoMamografia: tipo, ...mudanca });
  }

  function mudarIndicacao<K extends IndicacaoDiagnostica>(chave: K, valor: DiagnosticaSiscan[K]) {
    mudarDentro({ diagnostica: { ...diagnostica, [chave]: valor } });
  }

  function alternarIndicacao(chave: IndicacaoDiagnostica) {
    if (diagnostica[chave].marcada) {
      if (indicacaoPreenchida(diagnostica, chave) && !window.confirm(AVISO_PERDA)) return;
      mudarIndicacao(chave, indicacaoVazia(chave));
      return;
    }
    mudarIndicacao(chave, { ...indicacaoVazia(chave), marcada: true });
  }

  function subBloco(chave: IndicacaoDiagnostica) {
    switch (chave) {
      case 'achadosExameClinico': {
        const v = diagnostica.achadosExameClinico;
        return (
          <div className="grid gap-3 md:grid-cols-2">
            {LADOS.map(([lado, titulo]) => (
              <AchadosDaMama
                key={lado}
                titulo={titulo}
                valor={v[lado]}
                aoMudar={(novo) => mudarIndicacao('achadosExameClinico', { ...v, [lado]: novo })}
              />
            ))}
          </div>
        );
      }
      case 'respostaQuimioterapia': {
        const v = diagnostica.respostaQuimioterapia;
        return (
          <div className="flex flex-wrap gap-2">
            {OPCOES_LADO.map((o) => (
              <Opcao
                key={o}
                valor={o}
                atual={v.lado}
                rotulo={ROTULOS_LADO[o]}
                aoEscolher={(lado) => mudarIndicacao('respostaQuimioterapia', { ...v, lado })}
              />
            ))}
          </div>
        );
      }
      case 'revisaoMamografia': {
        const v = diagnostica.revisaoMamografia;
        return (
          <div className="space-y-2">
            {LADOS.map(([lado, titulo]) => {
              const campo = lado === 'direita' ? 'categoriaDireita' : 'categoriaEsquerda';
              return (
                <div key={lado} className="flex flex-wrap items-center gap-2">
                  <span className="w-28 text-sm text-gray-700">{titulo}</span>
                  {CATEGORIAS_BIRADS.map((c) => (
                    <Opcao<CategoriaBiRads>
                      key={c}
                      valor={c}
                      atual={v[campo]}
                      rotulo={`Categoria ${c}`}
                      aoEscolher={(cat) => mudarIndicacao('revisaoMamografia', { ...v, [campo]: cat })}
                    />
                  ))}
                </div>
              );
            })}
          </div>
        );
      }
      default: {
        const v = diagnostica[chave];
        return (
          <GradeDeAchados valor={v.achados} aoMudar={(achados) => mudarIndicacao(chave, { ...v, achados })} />
        );
      }
    }
  }

  return (
    <fieldset disabled={somenteLeitura} className="rounded-md border border-gray-300 p-3">
      <Legenda texto="Tipo de mamografia" />
      <div className="flex flex-wrap gap-2">
        {OPCOES_TIPO.map((o) => (
          <Opcao key={o} valor={o} atual={tipo} rotulo={ROTULOS_TIPO_MAMOGRAFIA[o]} aoEscolher={trocarTipo} />
        ))}
      </div>
      {siscan.tipoMamografia === null && sugerido ? (
        <p className="mt-1.5 text-xs text-gray-500">
          Marcado pela idade ({idade} anos): até 35 anos, diagnóstica; 36 ou mais, rastreamento. Pode
          trocar — na tela do SISCAN o tipo é escolha de quem preenche.
        </p>
      ) : null}
      {tipo === null ? (
        <Aviso>A paciente está sem data de nascimento: escolha o tipo de mamografia.</Aviso>
      ) : null}

      {tipo === 'rastreamento' ? (
        <fieldset className="mt-3 rounded-md border border-gray-200 p-3">
          <Legenda texto="Mamografia de rastreamento" />
          <div className="flex flex-wrap gap-2">
            {OPCOES_POPULACAO.map((o) => {
              const desabilitada = o === 'riscoElevado' && !podeRiscoElevado;
              return (
                <Opcao
                  key={o}
                  valor={o}
                  atual={populacao}
                  rotulo={ROTULOS_POPULACAO[o]}
                  desabilitada={desabilitada}
                  titulo={
                    desabilitada
                      ? 'Só habilita quando "Apresenta risco elevado" (seção 5) é Sim — como no SISCAN.'
                      : undefined
                  }
                  aoEscolher={(v) => mudarDentro({ populacaoRastreamento: v })}
                />
              );
            })}
          </div>
          {siscan.populacaoRastreamento === null && populacao ? (
            <p className="mt-1.5 text-xs text-gray-500">Marcada pela avaliação de risco (seção 5).</p>
          ) : null}
          {populacao === null ? (
            <Aviso>
              Escolha a população. &quot;Risco elevado (história familiar)&quot; só pode ser marcada
              quando &quot;Apresenta risco elevado&quot; (seção 5) é Sim — no SISCAN ela fica travada.
            </Aviso>
          ) : null}
        </fieldset>
      ) : null}

      {tipo === 'diagnostica' ? (
        <fieldset className="mt-3 rounded-md border border-gray-200 p-3">
          <Legenda texto="Diagnóstica" />
          <div className="space-y-2">
            {INDICACOES_DIAGNOSTICA.map(([chave, rotulo]) => {
              const marcada = diagnostica[chave].marcada;
              return (
                <div key={chave} className={marcada ? 'rounded-md bg-gray-50 p-2' : undefined}>
                  <label className="flex items-start gap-2 text-sm font-medium text-gray-800">
                    <input
                      type="checkbox"
                      checked={marcada}
                      onChange={() => alternarIndicacao(chave)}
                      className="mt-0.5 h-4 w-4 rounded border-gray-300"
                    />
                    {rotulo}
                  </label>
                  {marcada ? (
                    <div className="mt-2 pl-6">
                      {subBloco(chave)}
                      {!indicacaoPreenchida(diagnostica, chave) ? (
                        <Aviso>Marque o que foi visto — sem isso a requisição não é gerada.</Aviso>
                      ) : null}
                    </div>
                  ) : null}
                </div>
              );
            })}
          </div>
          {!algumaIndicacao ? (
            <Aviso>Marque ao menos uma indicação — o SISCAN recusa a requisição diagnóstica sem nenhuma.</Aviso>
          ) : null}
        </fieldset>
      ) : null}
    </fieldset>
  );
}

/** Uma indicação marcada, em uma linha — para a leitura da anamnese. */
export function descreverIndicacao(d: DiagnosticaSiscan, chave: IndicacaoDiagnostica): string {
  const lado = (l: LadoMama) => (l === 'direita' ? 'D' : 'E');
  switch (chave) {
    case 'achadosExameClinico': {
      const partes = LADOS.flatMap(([l]) => {
        const m = d.achadosExameClinico[l];
        const itens = [
          m.lesaoPapilar ? 'lesão papilar' : null,
          m.descargaPapilar ? `descarga ${m.descargaPapilar === 'cristalina' ? 'cristalina' : 'hemorrágica'}` : null,
          m.nodulo.length ? `nódulo ${m.nodulo.join('/')}` : null,
          m.espessamento.length ? `espessamento ${m.espessamento.join('/')}` : null,
          m.linfonodo.length ? `linfonodo ${m.linfonodo.join('/')}` : null,
        ].filter(Boolean);
        return itens.length ? [`${lado(l)}: ${itens.join(', ')}`] : [];
      });
      return partes.join(' · ');
    }
    case 'respostaQuimioterapia':
      return d.respostaQuimioterapia.lado ? ROTULOS_LADO[d.respostaQuimioterapia.lado] : '';
    case 'revisaoMamografia':
      return [
        d.revisaoMamografia.categoriaDireita !== null ? `D: categoria ${d.revisaoMamografia.categoriaDireita}` : null,
        d.revisaoMamografia.categoriaEsquerda !== null ? `E: categoria ${d.revisaoMamografia.categoriaEsquerda}` : null,
      ]
        .filter(Boolean)
        .join(' · ');
    default:
      return ACHADOS_RADIOLOGICOS.flatMap(([a, rotulo]) => {
        const v = d[chave].achados[a];
        const lados = [v.direita ? 'D' : null, v.esquerda ? 'E' : null].filter(Boolean).join('+');
        return lados ? [`${rotulo.toLowerCase()} ${lados}`] : [];
      }).join(', ');
  }
}
