/**
 * Anamnese — questionário pré-exame vinculado 1:1 à solicitação.
 * O conteúdo é versionado por tipo de questionário; hoje só existe o de
 * mamografia (CDT Maricá, v1), espelhando o formulário em papel.
 */

export type ClassificacaoRisco = 'Baixo' | 'Moderado' | 'Alto';

/** Pergunta Sim/Não com observação livre (seção 3 do formulário). */
export type RespostaSimNao = {
  resposta: boolean | null;
  observacao: string;
};

/** Saúde reprodutiva (seção 6) — anticoncepcional, menstruação e nº de filhos. */
export type SaudeReprodutiva = {
  /** Faz uso de anticoncepcional? Se sim, `observacao` = qual. */
  usoAnticoncepcional: RespostaSimNao;
  /** Ainda menstrua? Se sim, `dataUltimaMenstruacao` (yyyy-mm-dd) = data da última menstruação. */
  aindaMenstrua: { resposta: boolean | null; dataUltimaMenstruacao: string };
  /** Número de filhos. */
  numeroFilhos: number | null;
};

/** Lado da mama no diagrama. */
export type LadoMama = 'direita' | 'esquerda';

/** Marcação pontual no diagrama das mamas (coordenadas em % do desenho de cada mama). */
export type MarcacaoMama = {
  mama: LadoMama;
  x: number;
  y: number;
};

/** Ponto de um traço livre (mesmas coordenadas do diagrama). */
export type PontoTraco = { x: number; y: number };

/**
 * Traço livre ("brush") desenhado sobre o diagrama de uma mama — ex.: cicatriz
 * de cirurgia. É uma sequência de pontos ligados por uma linha.
 */
export type TracoMama = {
  mama: LadoMama;
  pontos: PontoTraco[];
};

export const SINTOMAS_QUEIXA = [
  'dor',
  'noduloPalpavel',
  'secrecaoMamilar',
  'alteracaoNaPele',
  'vermelhidao',
  'retracao',
  'edema',
  'outro',
] as const;

export type SintomaQueixa = (typeof SINTOMAS_QUEIXA)[number];

export const ROTULOS_SINTOMAS: Record<SintomaQueixa, string> = {
  dor: 'Dor',
  noduloPalpavel: 'Nódulo palpável',
  secrecaoMamilar: 'Secreção mamilar',
  alteracaoNaPele: 'Alteração na pele',
  vermelhidao: 'Vermelhidão',
  retracao: 'Retração',
  edema: 'Edema',
  outro: 'Outro',
};

export const PERGUNTAS_HISTORICO = [
  ['historicoFamiliarCancerMama', 'Tem histórico familiar de câncer de mama?'],
  ['historicoFamiliarCancerOvario', 'Tem histórico familiar de câncer de ovário?'],
  ['jaRealizouMamografia', 'Já realizou mamografia?'],
  ['jaRealizouUltrassonografiaMamaria', 'Já realizou ultrassonografia mamária?'],
  ['possuiProteseMamaria', 'Possui prótese mamária?'],
  ['jaRealizouCirurgiaMamaria', 'Já realizou cirurgia mamária?'],
  ['estaGestante', 'Está gestante?'],
  ['estaAmamentando', 'Já amamentou?'],
  ['fazUsoHormonios', 'Faz uso de hormônios?'],
  ['eTabagista', 'É tabagista?'],
] as const;

export type PerguntaHistorico = (typeof PERGUNTAS_HISTORICO)[number][0];

export const CRITERIOS_RISCO = [
  ['familiar1GrauCancerMama', 'Familiar de 1º grau com câncer de mama'],
  ['cancerMamaAntes50Familia', 'Câncer de mama antes dos 50 anos na família'],
  ['historicoPessoalCancer', 'Histórico pessoal de câncer'],
  ['mutacaoGeneticaConhecida', 'Mutação genética conhecida'],
] as const;

export type CriterioRisco = (typeof CRITERIOS_RISCO)[number][0];

// ---- v2: o que o SISCAN exige e o formulário de papel não perguntava ----
//
// A requisição de mamografia do SISCAN tem 6 perguntas obrigatórias. Quatro já
// vinham do nosso questionário; estas faltavam, e sem elas a requisição só
// poderia ser gerada respondendo "Não sabe" — que o SISCAN aceita, mas que é
// informação jogada fora justamente com a paciente na nossa frente.

/** Resposta de três estados, como o SISCAN pergunta. */
export type SimNaoNaoSabe = 'sim' | 'nao' | 'naoSabe';

/** Lado da radioterapia, como o SISCAN pergunta. */
export type LadoOuAmbas = 'direita' | 'esquerda' | 'ambas';

/** "Antes desta consulta, teve as mamas examinadas por um profissional de saúde?" */
export type MamasExaminadasAntes = 'sim' | 'nunca' | 'naoSabe';

export const ROTULOS_MAMAS_EXAMINADAS: Record<MamasExaminadasAntes, string> = {
  sim: 'Sim',
  nunca: 'Nunca foram examinadas anteriormente',
  naoSabe: 'Não sabe',
};

export const ROTULOS_SIM_NAO_NAO_SABE: Record<SimNaoNaoSabe, string> = {
  sim: 'Sim',
  nao: 'Não',
  naoSabe: 'Não sabe',
};

export const ROTULOS_LADO: Record<LadoOuAmbas, string> = {
  direita: 'Mama direita',
  esquerda: 'Mama esquerda',
  ambas: 'Ambas',
};

/**
 * Os 13 tipos de cirurgia de mama do SISCAN. A chave espelha o nome do campo
 * deles (`frm:ano<Chave><Lado>`) para o de-para ser conferível a olho — ex.:
 * `mastectomiaPoupadoraPele` → `frm:anoMastectomiaPoupadoraPeleDireita`.
 */
export const TIPOS_CIRURGIA_MAMA = [
  ['biopsiaCirurgicaIncisional', 'Biópsia cirúrgica incisional'],
  ['biopsiaCirurgicaExcisional', 'Biópsia cirúrgica excisional'],
  ['segmentectomia', 'Segmentectomia'],
  ['centralectomia', 'Centralectomia'],
  ['dutectomia', 'Dutectomia'],
  ['mastectomia', 'Mastectomia'],
  ['mastectomiaPoupadoraPele', 'Mastectomia poupadora de pele'],
  ['mastectomiaPoupadoraPeleComplexoPapilar', 'Mastectomia poupadora de pele e complexo papilar'],
  ['linfadenectomiaAxilar', 'Linfadenectomia axilar'],
  ['biopsiaLinfonodoSentinela', 'Biópsia de linfonodo sentinela'],
  ['reconstrucaoMamaria', 'Reconstrução mamária'],
  ['mastoplastiaRedutora', 'Mastoplastia redutora'],
  ['inclusaoImplantes', 'Inclusão de implantes'],
] as const;

export type TipoCirurgiaMama = (typeof TIPOS_CIRURGIA_MAMA)[number][0];

export const ROTULOS_CIRURGIA = Object.fromEntries(TIPOS_CIRURGIA_MAMA) as Record<
  TipoCirurgiaMama,
  string
>;

/** Uma cirurgia relatada: tipo + lado + ano (o SISCAN guarda um ano por par). */
export type CirurgiaMama = {
  tipo: TipoCirurgiaMama;
  lado: LadoMama;
  ano: string;
};

/**
 * Quem assina a requisição no SISCAN.
 *
 * O SISCAN identifica o profissional pelo **CNS**, que não existe nem em
 * `fhir.practitioner` nem na ficha do SISREG (medido em 22/09/2026: nome em
 * 100% das solicitações, CPF em 33%, nº de conselho em 0%). Por isso guardamos
 * o par inteiro: o nome é para a tela, o CNS é o que vai no POST. Sem CNS não
 * existe requisição.
 */
export type ResponsavelSiscan = {
  nome: string;
  cns: string;
};

/**
 * O quadro "RISCO ELEVADO SÃO:" que o SISCAN mostra junto da pergunta — texto dele, sem
 * adaptação: quem responde precisa ver a mesma régua que vale lá.
 */
export const RISCO_ELEVADO_SISCAN = [
  'Mulheres com história familiar de, pelo menos, um parente de primeiro grau com diagnóstico de:',
  '- Câncer de mama antes dos 50 anos de idade;',
  '- Câncer de mama bilateral ou câncer de ovário em qualquer faixa etária;',
  'Mulheres com história familiar de câncer de mama masculino;',
  'Mulheres com diagnóstico histopatológico de lesão mamária proliferativa com atipia ou neoplasia lobular in situ;',
  'Mulheres com história pessoal de câncer de mama;',
] as const;

// ---- Tipo de mamografia, como o bloco da requisição do SISCAN (16/10/2026) ----
//
// Mapa medido na tela deles, sem gravar (Automais.SISCAN/docs/FLUXO-NOVA-REQUISICAO.md §6b-2):
// o rastreamento pede uma população; a diagnóstica pede ao menos uma de 6 indicações, e cada uma
// abre o seu sub-bloco. Os códigos de cada opção estão no SiscanRequisicaoMapper do servidor.

export type TipoMamografia = 'diagnostica' | 'rastreamento';

export const ROTULOS_TIPO_MAMOGRAFIA: Record<TipoMamografia, string> = {
  diagnostica: 'Diagnóstica',
  rastreamento: 'Rastreamento',
};

export type PopulacaoRastreamento = 'alvo' | 'riscoElevado' | 'jaTratada';

/** Texto da tela do SISCAN. */
export const ROTULOS_POPULACAO: Record<PopulacaoRastreamento, string> = {
  alvo: 'População alvo',
  riscoElevado: 'População de risco elevado (história familiar)',
  jaTratada: 'Paciente já tratado de câncer de mama',
};

/** As 10 regiões da mama do exame clínico, na ordem da tela, com o significado da sigla. */
export const REGIOES_MAMA = [
  ['QSL', 'Quadrante superior lateral'],
  ['QIL', 'Quadrante inferior lateral'],
  ['QSM', 'Quadrante superior medial'],
  ['QIM', 'Quadrante inferior medial'],
  ['UQLat', 'União dos quadrantes laterais'],
  ['UQsup', 'União dos quadrantes superiores'],
  ['UQmed', 'União dos quadrantes mediais'],
  ['UQinf', 'União dos quadrantes inferiores'],
  ['RRA', 'Região retroareolar'],
  ['PA', 'Prolongamento axilar'],
] as const;

export type RegiaoMama = (typeof REGIOES_MAMA)[number][0];

export type DescargaPapilar = 'cristalina' | 'hemorragica';
export type LinfonodoPalpavel = 'axilar' | 'supraclavicular';
export type CategoriaBiRads = '0' | '3' | '4' | '5';

export const CATEGORIAS_BIRADS: CategoriaBiRads[] = ['0', '3', '4', '5'];

/** Os 7 achados da grade que três indicações repetem, × 2 mamas. */
export const ACHADOS_RADIOLOGICOS = [
  ['nodulo', 'Nódulo'],
  ['microcalcificacoes', 'Microcalcificações'],
  ['assimetriaFocal', 'Assimetria focal'],
  ['assimetriaDifusa', 'Assimetria difusa'],
  ['areaDensa', 'Área densa'],
  ['distorcaoFocal', 'Distorção focal'],
  ['linfonodoAxilar', 'Linfonodo axilar'],
] as const;

export type AchadoRadiologico = (typeof ACHADOS_RADIOLOGICOS)[number][0];
export type GradeAchados = Record<AchadoRadiologico, { direita: boolean; esquerda: boolean }>;

/** "Achados no exame clínico" de uma mama. */
export type AchadosClinicosMama = {
  lesaoPapilar: boolean;
  /** Exclusiva: na tela, marcar uma desmarca a outra. */
  descargaPapilar: DescargaPapilar | null;
  nodulo: RegiaoMama[];
  espessamento: RegiaoMama[];
  linfonodo: LinfonodoPalpavel[];
};

export type DiagnosticaSiscan = {
  achadosExameClinico: { marcada: boolean; direita: AchadosClinicosMama; esquerda: AchadosClinicosMama };
  controleRadiologico: { marcada: boolean; achados: GradeAchados };
  lesaoDiagnosticoCancer: { marcada: boolean; achados: GradeAchados };
  respostaQuimioterapia: { marcada: boolean; lado: LadoOuAmbas | null };
  /** Uma categoria BI-RADS por mama (exclusiva por lado, como na tela). */
  revisaoMamografia: {
    marcada: boolean;
    categoriaDireita: CategoriaBiRads | null;
    categoriaEsquerda: CategoriaBiRads | null;
  };
  controleAposBiopsia: { marcada: boolean; achados: GradeAchados };
};

export type IndicacaoDiagnostica = keyof DiagnosticaSiscan;

/** As 6 indicações, na ordem e com o texto da tela do SISCAN. */
export const INDICACOES_DIAGNOSTICA: [IndicacaoDiagnostica, string][] = [
  ['achadosExameClinico', 'Achados no exame clínico'],
  ['controleRadiologico', 'Controle radiológico de lesão categoria 3'],
  ['lesaoDiagnosticoCancer', 'Lesão com diagnóstico de câncer'],
  ['respostaQuimioterapia', 'Avaliação da resposta de quimioterapia neoadjuvante'],
  ['revisaoMamografia', 'Revisão de mamografia com lesão realizada em outra instituição'],
  ['controleAposBiopsia', 'Controle de lesão após biópsia de fragmento ou PAAF com resultado benigno'],
];

function gradeVazia(): GradeAchados {
  const lado = () => ({ direita: false, esquerda: false });
  return {
    nodulo: lado(),
    microcalcificacoes: lado(),
    assimetriaFocal: lado(),
    assimetriaDifusa: lado(),
    areaDensa: lado(),
    distorcaoFocal: lado(),
    linfonodoAxilar: lado(),
  };
}

function achadosClinicosVazios(): AchadosClinicosMama {
  return { lesaoPapilar: false, descargaPapilar: null, nodulo: [], espessamento: [], linfonodo: [] };
}

/** O sub-bloco de uma indicação, em branco — é o que a tela deixa ao desmarcá-la. */
export function indicacaoVazia<K extends IndicacaoDiagnostica>(chave: K): DiagnosticaSiscan[K] {
  return diagnosticaVazia()[chave];
}

export function diagnosticaVazia(): DiagnosticaSiscan {
  return {
    achadosExameClinico: {
      marcada: false,
      direita: achadosClinicosVazios(),
      esquerda: achadosClinicosVazios(),
    },
    controleRadiologico: { marcada: false, achados: gradeVazia() },
    lesaoDiagnosticoCancer: { marcada: false, achados: gradeVazia() },
    respostaQuimioterapia: { marcada: false, lado: null },
    revisaoMamografia: { marcada: false, categoriaDireita: null, categoriaEsquerda: null },
    controleAposBiopsia: { marcada: false, achados: gradeVazia() },
  };
}

/** Merge defensivo do bloco gravado sobre o vazio (anamnese anterior a 16/10/2026 não tem). */
export function mesclarDiagnostica(gravada?: Partial<DiagnosticaSiscan>): DiagnosticaSiscan {
  const base = diagnosticaVazia();
  if (!gravada) return base;
  const grade = (g?: Partial<GradeAchados>) => ({ ...gradeVazia(), ...g });
  return {
    achadosExameClinico: {
      ...base.achadosExameClinico,
      ...gravada.achadosExameClinico,
      direita: { ...achadosClinicosVazios(), ...gravada.achadosExameClinico?.direita },
      esquerda: { ...achadosClinicosVazios(), ...gravada.achadosExameClinico?.esquerda },
    },
    controleRadiologico: {
      ...base.controleRadiologico,
      ...gravada.controleRadiologico,
      achados: grade(gravada.controleRadiologico?.achados),
    },
    lesaoDiagnosticoCancer: {
      ...base.lesaoDiagnosticoCancer,
      ...gravada.lesaoDiagnosticoCancer,
      achados: grade(gravada.lesaoDiagnosticoCancer?.achados),
    },
    respostaQuimioterapia: { ...base.respostaQuimioterapia, ...gravada.respostaQuimioterapia },
    revisaoMamografia: { ...base.revisaoMamografia, ...gravada.revisaoMamografia },
    controleAposBiopsia: {
      ...base.controleAposBiopsia,
      ...gravada.controleAposBiopsia,
      achados: grade(gravada.controleAposBiopsia?.achados),
    },
  };
}

/** A indicação tem algo dentro (mama, achado, categoria)? Sem isso o servidor não gera. */
export function indicacaoPreenchida(d: DiagnosticaSiscan, chave: IndicacaoDiagnostica): boolean {
  switch (chave) {
    case 'achadosExameClinico': {
      const mama = (m: AchadosClinicosMama) =>
        m.lesaoPapilar ||
        m.descargaPapilar !== null ||
        m.nodulo.length > 0 ||
        m.espessamento.length > 0 ||
        m.linfonodo.length > 0;
      return mama(d.achadosExameClinico.direita) || mama(d.achadosExameClinico.esquerda);
    }
    case 'respostaQuimioterapia':
      return d.respostaQuimioterapia.lado !== null;
    case 'revisaoMamografia':
      return d.revisaoMamografia.categoriaDireita !== null || d.revisaoMamografia.categoriaEsquerda !== null;
    default:
      return Object.values(d[chave].achados).some((l) => l.direita || l.esquerda);
  }
}

/**
 * Régua do CDT Maricá (22/09/2026) — a mesma de `SiscanRequisicaoMapper.TipoMamografiaPorIdade`:
 * 36 anos ou mais = rastreamento; até 35 = diagnóstica. É sugestão: o tipo é escolha na tela do
 * SISCAN, e a seção 7 deixa trocar.
 */
export function tipoPelaIdade(idade: number | null): TipoMamografia | null {
  if (idade === null) return null;
  return idade >= 36 ? 'rastreamento' : 'diagnostica';
}

/**
 * "Apresenta risco elevado?" como vai ao SISCAN — espelho de `SiscanRequisicaoMapper.RiscoElevado`:
 * a resposta explícita da seção 5 vence; sem ela, Moderado/Alto = Sim, Baixo = Não; sem
 * classificação, qualquer critério marcado = Sim, todos "não" = Não, senão Não sabe.
 */
export function riscoElevadoEfetivo(c: AnamneseMamografiaConteudo): SimNaoNaoSabe {
  if (c.siscan.riscoElevado) return c.siscan.riscoElevado;
  const classificacao = c.avaliacaoRisco.classificacao;
  if (classificacao === 'Alto' || classificacao === 'Moderado') return 'sim';
  if (classificacao === 'Baixo') return 'nao';
  const criterios = CRITERIOS_RISCO.map(([chave]) => c.avaliacaoRisco[chave]);
  if (criterios.some((v) => v === true)) return 'sim';
  return criterios.every((v) => v === false) ? 'nao' : 'naoSabe';
}

/**
 * A população que os critérios da seção 5 indicam — espelho de `SiscanRequisicaoMapper`
 * (PopulacaoRastreamento + MontarRastreamento): histórico pessoal manda; familiar de 1º grau ou
 * câncer antes dos 50 na família = risco elevado — mas só se "Apresenta risco elevado" for Sim, que
 * é quando a tela do SISCAN destrava essa opção; senão, população alvo (o que a operação marcava).
 */
export function populacaoPelosCriterios(c: AnamneseMamografiaConteudo): PopulacaoRastreamento {
  if (c.avaliacaoRisco.historicoPessoalCancer === true) return 'jaTratada';
  if (c.avaliacaoRisco.familiar1GrauCancerMama === true || c.avaliacaoRisco.cancerMamaAntes50Familia === true) {
    return riscoElevadoEfetivo(c) === 'sim' ? 'riscoElevado' : 'alvo';
  }
  return 'alvo';
}

/** Bloco v2 — respostas que existem só porque o SISCAN pede. */
export type ComplementoSiscan = {
  /**
   * "TIPO DE MAMOGRAFIA" (16/10/2026). Null = ninguém escolheu: vale a régua da idade, que a
   * seção 7 mostra marcada "pela idade".
   */
  tipoMamografia: TipoMamografia | null;
  /** "MAMOGRAFIA DE RASTREAMENTO". Null = vale o que os critérios da seção 5 indicam. */
  populacaoRastreamento: PopulacaoRastreamento | null;
  /** "DIAGNÓSTICA": as 6 indicações e o que cada uma abre. */
  diagnostica: DiagnosticaSiscan;
  /**
   * "Apresenta risco elevado para câncer de mama?" como o SISCAN pergunta (ticket #139).
   * Convive com a avaliação de risco da seção 5; quando respondida, é ela que vai ao SISCAN.
   */
  riscoElevado: SimNaoNaoSabe | null;
  mamasExaminadasAntes: MamasExaminadasAntes | null;
  radioterapia: {
    resposta: SimNaoNaoSabe | null;
    lado: LadoOuAmbas | null;
    anoDireita: string;
    anoEsquerda: string;
  };
  /** Ano da última mamografia — só aparece se a seção 3 disse que já fez. */
  anoUltimaMamografia: string;
  /**
   * O ano que a paciente declarou, quando ele era anterior ao que o SISCAN já tinha e foi
   * corrigido ao gerar a requisição. Escrito só pelo servidor; a tela nunca preenche.
   */
  anoUltimaMamografiaDeclarado?: string;
  /**
   * A requisição saiu por outra unidade, porque a do pedido não está na conta do SISCAN de quem
   * gerou. Escrito só pelo servidor, ao gerar; a tarja da anamnese mostra.
   */
  /**
   * A Raça/Cor que faltava no CADSUS e foi informada ao SISCAN ao gerar (autodeclarada; etnia só se
   * Indígena). Escrito só pelo servidor; a tarja da anamnese mostra.
   */
  racaCorInformada?: {
    codigo: string;
    rotulo: string;
    etniaCodigo: string | null;
    etniaRotulo: string | null;
    informadaPor: string | null;
    informadaEm: string;
  };
  unidadeRequisitanteEscolhida?: {
    cnes: string;
    nome: string;
    cnesDoPedido: string;
    nomeDoPedido: string;
    escolhidaPor: string | null;
    escolhidaEm: string;
  };
  /** Cirurgias relatadas — preenchidas na seção 3, logo abaixo de "Já realizou cirurgia mamária?". */
  cirurgias: CirurgiaMama[];
  responsavel: ResponsavelSiscan | null;
};

/** Conteúdo completo do questionário de mamografia (v2) — gravado como JSON. */
export type AnamneseMamografiaConteudo = {
  avaliacaoClinica: {
    marcacoes: MarcacaoMama[];
    tracos: TracoMama[];
    semAlteracoes: boolean;
    alteracoesPalpaveis: boolean;
    especificar: string;
    outrasObservacoes: string;
  };
  historicoClinico: Record<PerguntaHistorico, RespostaSimNao> & {
    outrasInformacoes: string;
  };
  queixas: {
    sintomas: Record<SintomaQueixa, { direita: boolean; esquerda: boolean }>;
    outroTexto: string;
    descrever: string;
  };
  avaliacaoRisco: Record<CriterioRisco, boolean | null> & {
    classificacao: ClassificacaoRisco | null;
  };
  saudeReprodutiva: SaudeReprodutiva;
  siscan: ComplementoSiscan;
};

export function conteudoVazio(): AnamneseMamografiaConteudo {
  const simNao = (): RespostaSimNao => ({ resposta: null, observacao: '' });
  const porMama = () => ({ direita: false, esquerda: false });
  return {
    avaliacaoClinica: {
      marcacoes: [],
      tracos: [],
      semAlteracoes: false,
      alteracoesPalpaveis: false,
      especificar: '',
      outrasObservacoes: '',
    },
    historicoClinico: {
      historicoFamiliarCancerMama: simNao(),
      historicoFamiliarCancerOvario: simNao(),
      jaRealizouMamografia: simNao(),
      jaRealizouUltrassonografiaMamaria: simNao(),
      possuiProteseMamaria: simNao(),
      jaRealizouCirurgiaMamaria: simNao(),
      estaGestante: simNao(),
      estaAmamentando: simNao(),
      fazUsoHormonios: simNao(),
      eTabagista: simNao(),
      outrasInformacoes: '',
    },
    queixas: {
      sintomas: {
        dor: porMama(),
        noduloPalpavel: porMama(),
        secrecaoMamilar: porMama(),
        alteracaoNaPele: porMama(),
        vermelhidao: porMama(),
        retracao: porMama(),
        edema: porMama(),
        outro: porMama(),
      },
      outroTexto: '',
      descrever: '',
    },
    avaliacaoRisco: {
      familiar1GrauCancerMama: null,
      cancerMamaAntes50Familia: null,
      historicoPessoalCancer: null,
      mutacaoGeneticaConhecida: null,
      classificacao: null,
    },
    saudeReprodutiva: {
      usoAnticoncepcional: simNao(),
      aindaMenstrua: { resposta: null, dataUltimaMenstruacao: '' },
      numeroFilhos: null,
    },
    siscan: {
      tipoMamografia: null,
      populacaoRastreamento: null,
      diagnostica: diagnosticaVazia(),
      riscoElevado: null,
      mamasExaminadasAntes: null,
      radioterapia: { resposta: null, lado: null, anoDireita: '', anoEsquerda: '' },
      anoUltimaMamografia: '',
      cirurgias: [],
      responsavel: null,
    },
  };
}

// ---- DTOs da API ----

export type AnamneseDto = {
  id: string;
  solicitacaoExameId: string;
  tipo: string;
  versao: number;
  conteudoJson: string;
  classificacaoRisco: ClassificacaoRisco | null;
  preenchidoPorNome: string | null;
  criadoEm: string;
  atualizadoEm: string | null;
  /** O que aconteceu com a worklist ao salvar (Holter). Null = nada a dizer. */
  avisoWorklist?: string | null;
};

export type AnamneseContexto = {
  solicitacaoExameId: string;
  accessionNumber: string;
  tipoExameNome: string;
  modalidadeDicom: string;
  pacienteId: string;
  pacienteNome: string;
  pacienteCpf: string | null;
  pacienteCns: string | null;
  pacienteNascimento: string | null;
  anamnese: AnamneseDto | null;
  /** Protocolo da requisição no SISCAN, quando já foi gerada. Null = ainda não. */
  siscanProtocolo: string | null;
  /** Nº do exame no SISCAN — o outro número, que abre o resultado lá. */
  siscanNumeroExame: string | null;
  /** "Diagnóstico inicial" do pedido = CID-10 do SISREG. Null = o pedido não trouxe CID. */
  cidCodigo: string | null;
  /** Descrição do CID por extenso quando o catálogo reconhece; null = mostra só o código. */
  cidDescricao: string | null;
  /** Qual questionário a tela abre — pela modalidade do tipo de exame (ECG = Holter). */
  questionario?: 'mamografia' | 'holter';
};

// ---- Anamnese do Holter (vai à worklist do CardioNet) ----

export type RespostaFumante = 'sim' | 'nao' | 'parou';

/** Conteúdo da anamnese do Holter — o servidor confere o mesmo shape (AnamneseHolter.cs). */
export type AnamneseHolterConteudo = {
  alturaCm: number | null;
  pesoKg: number | null;
  fumante: RespostaFumante | null;
};

export const holterVazio = (): AnamneseHolterConteudo => ({ alturaCm: null, pesoKg: null, fumante: null });

export type SalvarAnamnesePayload = {
  tipo: string;
  versao: number;
  conteudoJson: string;
  classificacaoRisco: ClassificacaoRisco | null;
};

// ---- Anexos de exame (documentos digitalizados via PWA "Arquivos Saúde Maricá") ----

export type AnexoExameStatus = 'Pendente' | 'Salvo';

/** Documento (PDF) anexado a uma solicitação de exame. Espelha AnexoExameDto do backend. */
export type AnexoExameDto = {
  id: string;
  nome: string;
  descricao: string | null;
  mimeType: string;
  tamanhoBytes: number;
  status: AnexoExameStatus;
  origem: string | null;
  paginas: number | null;
  criadoEm: string;
  urlConteudo: string;
};

/** Resposta de POST /anamneses/{id}/anexos/tokens — abre a ponte QR → PWA. */
export type AnexoUploadTokenDto = {
  token: string;
  url: string;
  expiraEm: string;
  solicitacaoExameId: string;
  paciente: { id: string; nome: string };
};

/**
 * Corpo de POST /anexos/{id}/salvar — confirma o documento (Pendente → Salvo).
 * O backend (SalvarAnexoDto) aceita só nome/descrição opcionais; o status é
 * setado para Salvo pela própria ação. Sem corpo = apenas confirma.
 */
export type SalvarAnexoPayload = {
  nome?: string;
  descricao?: string;
};
