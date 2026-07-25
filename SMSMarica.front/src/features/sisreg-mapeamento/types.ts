/** Procedimento que um profissional executa na unidade, como cadastrado no SISREG. */
export type SisregProcedimento = {
  id: string;
  codigo: string;
  nome: string;
  /** Entra na varredura de agenda. */
  habilitado: boolean;
  /** Código terminado em 000: a consulta dele já traz os itens individuais. */
  grupo: boolean;
  /** Sumiu da última atualização vinda do SISREG. */
  ausente: boolean;
};

export type SisregProfissional = {
  id: string;
  cpf: string;
  nome: string;
  habilitado: boolean;
  /** Practitioner.id no hub FHIR, quando já sincronizado. */
  practitionerId: string | null;
  sincronizadoEm: string | null;
  ausente: boolean;
  procedimentos: SisregProcedimento[];
};

export type SisregMapeamento = {
  unidadeId: string;
  unidadeNome: string;
  unidadeCnes: string | null;
  atualizadoEm: string | null;
  totalProfissionais: number;
  profissionaisHabilitados: number;
  totalProcedimentos: number;
  procedimentosHabilitados: number;
  /** Pares (profissional × procedimento) habilitados = requisições por varredura. */
  combinacoesHabilitadas: number;
  profissionais: SisregProfissional[];
};

export type SisregMapeamentoAtualizacao = {
  profissionaisEncontrados: number;
  profissionaisNovos: number;
  profissionaisAusentes: number;
  procedimentosEncontrados: number;
  procedimentosNovos: number;
  procedimentosAusentes: number;
  requisicoesFeitas: number;
  mensagem: string;
};

export type SisregSincronizacaoFhir = {
  avaliados: number;
  criados: number;
  vinculados: number;
  jaSincronizados: number;
  erros: string[];
  mensagem: string;
};

/** Credencial do SISREG da unidade. A senha nunca vem da API — só o usuário. */
export type SisregCredencialUnidade = {
  unidadeId: string;
  unidadeNome: string;
  unidadeCnes: string | null;
  usuario: string | null;
  senhaDefinida: boolean;
  cnesConfirmado: string | null;
  unidadeSisregNome: string | null;
  validadoEm: string | null;
  ativo: boolean;
  /** Sem credencial própria: está usando a credencial global das Integrações. */
  usandoFallbackGlobal: boolean;
};

export type SisregAutenticacaoResultado = {
  sucesso: boolean;
  operador: string;
  perfil: string;
  unidadeSisregNome: string;
  cnes: string | null;
  unidadeConfere: boolean;
  mensagem: string;
};

export type SalvarCredencialPayload = { usuario: string; senha: string };
