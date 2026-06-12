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

/** Marcação no diagrama das mamas (coordenadas em % do desenho de cada mama). */
export type MarcacaoMama = {
  mama: 'direita' | 'esquerda';
  x: number;
  y: number;
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
  ['estaAmamentando', 'Está amamentando?'],
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

/** Conteúdo completo do questionário de mamografia (v1) — gravado como JSON. */
export type AnamneseMamografiaConteudo = {
  avaliacaoClinica: {
    marcacoes: MarcacaoMama[];
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
};

export function conteudoVazio(): AnamneseMamografiaConteudo {
  const simNao = (): RespostaSimNao => ({ resposta: null, observacao: '' });
  const porMama = () => ({ direita: false, esquerda: false });
  return {
    avaliacaoClinica: {
      marcacoes: [],
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
};

export type SalvarAnamnesePayload = {
  tipo: string;
  versao: number;
  conteudoJson: string;
  classificacaoRisco: ClassificacaoRisco | null;
};
