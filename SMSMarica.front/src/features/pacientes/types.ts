export const SEXOS = ['NaoInformado', 'Masculino', 'Feminino', 'Outro'] as const;
export type Sexo = (typeof SEXOS)[number];

export const ESTADOS_CIVIS = [
  'NaoInformado',
  'Solteiro',
  'Casado',
  'UniaoEstavel',
  'Divorciado',
  'Viuvo',
  'Separado',
] as const;
export type EstadoCivil = (typeof ESTADOS_CIVIS)[number];

export const RACAS = ['NaoInformado', 'Branca', 'Preta', 'Parda', 'Amarela', 'Indigena'] as const;
export type RacaCor = (typeof RACAS)[number];

export const ESCOLARIDADES = [
  'NaoInformado',
  'Analfabeto',
  'SemEscolaridade',
  'FundamentalIncompleto',
  'FundamentalCompleto',
  'MedioIncompleto',
  'MedioCompleto',
  'SuperiorIncompleto',
  'SuperiorCompleto',
  'PosGraduacao',
] as const;
export type Escolaridade = (typeof ESCOLARIDADES)[number];

export const TIPOS_SANGUINEOS = ['NaoInformado', 'A', 'B', 'AB', 'O'] as const;
export type TipoSanguineo = (typeof TIPOS_SANGUINEOS)[number];

export const FATORES_RH = ['NaoInformado', 'Positivo', 'Negativo'] as const;
export type FatorRh = (typeof FATORES_RH)[number];

export type Endereco = {
  cep: string;
  logradouro: string;
  numero?: string | null;
  complemento?: string | null;
  bairro: string;
  cidade: string;
  uf: string;
  pontoReferencia?: string | null;
};

export type ContatoEmergencia = {
  nome: string;
  parentesco?: string | null;
  telefone: string;
};

/** Identificador FHIR (system + valor): CPF, CNS, RG, PIS, prontuários, etc. */
export type Identificador = {
  sistema: string;
  valor: string;
};

export type PacienteListItem = {
  id: string;
  nomeCompleto: string;
  nomeSocial?: string | null;
  cpf: string;
  dataNascimento?: string | null;
  nomeDaMae?: string | null;
  telefonePrincipal?: string | null;
  fotoBase64?: string | null;
  ativo: boolean;
  /**
   * Paciente sem CPF, vindo de um PEP. Não dá para uni-lo ao mesmo cidadão em outra base,
   * então PODE aparecer repetido — por isso o selo ao lado do nome.
   */
  identidadeIncompleta?: boolean;
};

export type Paciente = {
  id: string;
  nomeCompleto: string;
  nomeSocial: string | null;
  cpf: string;
  cns: string | null;
  rg: string | null;
  dataNascimento: string | null;
  sexo: Sexo;
  estadoCivil: EstadoCivil;
  racaCor: RacaCor;
  escolaridade: Escolaridade;
  ocupacao: string | null;
  naturalidade: string | null;
  nacionalidade: string;
  nomeDaMae: string | null;
  nomeDoPai: string | null;
  responsavelLegal: string | null;
  endereco: Endereco | null;
  latitude: number;
  longitude: number;
  telefonePrincipal: string | null;
  telefoneCelular: string | null;
  telefoneResidencial: string | null;
  /** Número do contato VERIFICADO por OTP (marcador no telecom FHIR) — já vem no objeto. */
  telefoneVerificado: string | null;
  telefoneVerificadoEm: string | null;
  email: string | null;
  contatoEmergencia: ContatoEmergencia | null;
  alturaCm: number | null;
  pesoKg: number | null;
  tipoSanguineo: TipoSanguineo;
  fatorRh: FatorRh;
  alergias: string[];
  medicamentosContinuos: string[];
  comorbidades: string[];
  deficiencias: string[];
  planoSaude: string | null;
  observacoes: string | null;
  fotoBase64: string | null;
  ativo: boolean;
  cadastradoEm: string;
  // --- Tudo que vem do recurso FHIR (hub) ---
  identificadores?: Identificador[] | null;
  dataObito?: string | null;
  nomeConjuge?: string | null;
  /** Sistema de origem do recurso no hub (FHIR Meta.source). */
  fonte?: string | null;
  /** Dados crus da fonte (ex.: códigos Salux: cor, religião, etnia, escolaridade). */
  dadosFonte?: Record<string, string> | null;
};

export type PacienteFormPayload = {
  nomeCompleto: string;
  nomeSocial?: string | null;
  cpf: string;
  dataNascimento: string;
  cns?: string | null;
  rg?: string | null;
  sexo: Sexo;
  estadoCivil: EstadoCivil;
  racaCor: RacaCor;
  escolaridade: Escolaridade;
  ocupacao?: string | null;
  naturalidade?: string | null;
  nacionalidade?: string | null;
  nomeDaMae?: string | null;
  nomeDoPai?: string | null;
  responsavelLegal?: string | null;
  endereco: Endereco | null;
  telefonePrincipal?: string | null;
  telefoneCelular?: string | null;
  telefoneResidencial?: string | null;
  email?: string | null;
  contatoEmergencia: ContatoEmergencia | null;
  alturaCm?: number | null;
  pesoKg?: number | null;
  tipoSanguineo: TipoSanguineo;
  fatorRh: FatorRh;
  alergias: string[];
  medicamentosContinuos: string[];
  comorbidades: string[];
  deficiencias: string[];
  planoSaude?: string | null;
  observacoes?: string | null;
  fotoBase64?: string | null;
};

export type CadastrarPacientePayload = PacienteFormPayload;

export type AtualizarPacientePayload = Omit<PacienteFormPayload, 'nomeCompleto' | 'cpf' | 'dataNascimento'>;

/** Diagnóstico (CID-10) de um atendimento. */
export type Diagnostico = {
  codigo: string;
  descricao?: string | null;
};

/** Medicamento prescrito (MedicationRequest) de um atendimento. */
export type Medicamento = {
  id: string;
  descricao: string;
  posologia?: string | null;
  urgente: boolean;
};

/** Documento clínico (DocumentReference) com o conteúdo HTML pronto para exibir. */
export type Documento = {
  id: string;
  tipo: string;
  data?: string | null;
  conteudoHtml: string;
};

/** Sinal vital aferido (Observation). `valor2` só na pressão arterial (sist./diast.). */
export type SinalVital = {
  codigo: string;
  nome: string;
  valor?: number | null;
  valor2?: number | null;
  unidade?: string | null;
  em?: string | null;
};

/** Classificação de risco (cor da triagem) do atendimento. */
export type Risco = {
  cor: string;
  descricao?: string | null;
  em?: string | null;
};

/**
 * Atendimento do histórico clínico (Encounter do hub FHIR). `fonte` é o PEP que gerou o
 * registro (Salux, Klinikos); `unidade*` é onde o paciente foi atendido. São dimensões
 * independentes — a base do Salux serve três unidades, e a mesma unidade acumula
 * atendimentos dos dois sistemas por causa do cutover.
 */
export type Atendimento = {
  id: string;
  inicio?: string | null;
  fim?: string | null;
  tipo: string;
  status: string;
  medicoNome?: string | null;
  fonte?: string | null;
  unidadeNome?: string | null;
  unidadeCnes?: string | null;
  diagnosticos: Diagnostico[];
  medicamentos: Medicamento[];
  documentos: Documento[];
  sinaisVitais: SinalVital[];
  risco?: Risco | null;
};

export type PacienteExistencia = {
  id: string;
  nomeCompleto: string;
  cpf: string;
  ativo: boolean;
};

// ---- Agendamentos (aba do cadastro) ----

export type OrigemAgendamento = 'Ser' | 'Sisreg' | 'Local';

export type SituacaoAgendamentoPaciente =
  | 'EmFila'
  | 'Pendente'
  | 'Agendado'
  | 'Confirmado'
  | 'Compareceu'
  | 'ChegadaNaoConfirmada'
  | 'Faltou'
  | 'Cancelado'
  | 'Concluido';

export type AgendamentoPacienteItem = {
  id: string;
  origem: OrigemAgendamento;
  tipo: string;
  descricao: string;
  unidade?: string | null;
  dataHora?: string | null;
  temHora: boolean;
  dataSolicitacao?: string | null;
  situacao: SituacaoAgendamentoPaciente;
  situacaoDescricao: string;
  situacaoOrigem?: string | null;
};

export type AgendamentosPaciente = {
  proximos: AgendamentoPacienteItem[];
  historico: AgendamentoPacienteItem[];
};
