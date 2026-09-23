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

/** Bloco v2 — respostas que existem só porque o SISCAN pede. */
export type ComplementoSiscan = {
  mamasExaminadasAntes: MamasExaminadasAntes | null;
  radioterapia: {
    resposta: SimNaoNaoSabe | null;
    lado: LadoOuAmbas | null;
    anoDireita: string;
    anoEsquerda: string;
  };
  /** Ano da última mamografia — só aparece se a seção 3 disse que já fez. */
  anoUltimaMamografia: string;
  /** Cirurgias relatadas — só aparecem se a seção 3 disse que já fez. */
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
};

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
