/**
 * Tipos do módulo Ouvidoria (ADR-0060). Espelham o contrato de `SMSMais.ouvidoria/PLANO-FASE-1.md`
 * §1.1 (enums → união de literais; a API serializa enum como STRING) e §3.3 (DTOs em camelCase).
 * `DateOnly` do .NET chega como "aaaa-mm-dd"; instantes chegam em ISO UTC.
 */

// ---- Enums (§1.1) ----

export type OuvidoriaTipo = 'Solicitacao' | 'Reclamacao' | 'Denuncia' | 'Sugestao' | 'Elogio' | 'Informacao';
export type OuvidoriaIdentificacao = 'Identificada' | 'Sigilosa' | 'Anonima';
export type OuvidoriaCanal =
  | 'Painel'
  | 'SitePublico'
  | 'AppCidadao'
  | 'WhatsApp'
  | 'Presencial'
  | 'Telefone'
  | 'Email'
  | 'Carta'
  | 'Urna'
  | 'BuscaAtiva'
  | 'Disque136'
  | 'FalaBr'
  | 'OuvidoriaGeral'
  | 'Outro';
export type OuvidoriaOrigem = 'Cidadao' | 'OuvidoriaAtiva' | 'DeOficio' | 'Coletiva';
export type OuvidoriaPrioridade = 'Normal' | 'Alta' | 'Urgente';
export type OuvidoriaStatus =
  | 'Registrada'
  | 'EmTriagem'
  | 'Encaminhada'
  | 'AguardandoComplementacao'
  | 'RespondidaPelaArea'
  | 'EmValidacao'
  | 'Respondida'
  | 'EmRecurso'
  | 'Concluida'
  | 'Arquivada'
  | 'EncaminhadaOutroOrgao';
export type OuvidoriaResolutividade = 'Resolvida' | 'NaoResolvida';
export type OuvidoriaSituacaoFinal =
  | 'Atendida'
  | 'NaoAtendida'
  | 'NaoLocalizado'
  | 'Faleceu'
  | 'Procede'
  | 'NaoProcede'
  | 'Inconclusiva';
export type OuvidoriaMotivoNaoAtendimento =
  | 'FaltaRecursos'
  | 'NaoCobertoSus'
  | 'FezParticular'
  | 'VagasInsuficientes'
  | 'NaoCompareceu'
  | 'Outro';
export type OuvidoriaMotivoArquivamento =
  | 'Duplicidade'
  | 'TextoIncompreensivel'
  | 'FaltaUrbanidade'
  | 'Impropria'
  | 'CopiaConhecimento'
  | 'PerdaObjeto'
  | 'SemComplementacao'
  | 'SemElementosMinimos'
  | 'Outro';
export type OuvidoriaTipoEvento =
  | 'Registro'
  | 'Triagem'
  | 'Reclassificacao'
  | 'Encaminhamento'
  | 'PedidoComplementacao'
  | 'Complementacao'
  | 'RespostaArea'
  | 'DevolucaoParaReanalise'
  | 'RespostaIntermediaria'
  | 'RespostaConclusiva'
  | 'Prorrogacao'
  | 'Cobranca'
  | 'Escalonamento'
  | 'Recurso'
  | 'Conclusao'
  | 'Arquivamento'
  | 'EncaminhamentoExterno'
  | 'Anotacao'
  | 'Habilitacao'
  | 'Reabertura'
  | 'AcessoIdentidade';
export type OuvidoriaTipoPontoResposta = 'Unidade' | 'AreaCentral' | 'Apuracao';

// ---- Paginação ----

export type PaginaDto<T> = { itens: T[]; total: number; pagina: number; tamanho: number };

// ---- DTOs (§3.3) ----

export type ManifestanteDto = {
  nome: string | null;
  cpf: string | null;
  telefone: string | null;
  email: string | null;
  patientId: string | null;
};

export type ReferidoDto = {
  patientId: string | null;
  nome: string | null;
  cpf: string | null;
  cns: string | null;
};

export type ManifestacaoCriadaDto = {
  id: string;
  protocolo: string;
  /** Mostrado UMA vez; o backend guarda só o hash. Null quando anônima. */
  codigoAcesso: string | null;
  prazoRespostaEm: string;
};

export type ManifestacaoListaDto = {
  id: string;
  protocolo: string;
  tipo: OuvidoriaTipo;
  status: OuvidoriaStatus;
  prioridade: OuvidoriaPrioridade;
  identificacao: OuvidoriaIdentificacao;
  canal: OuvidoriaCanal;
  resumo: string | null;
  assuntoNome: string | null;
  unidadeNome: string | null;
  pontoRespostaNome: string | null;
  /** Null quando a identidade é restrita (sigilosa/denúncia sem `OuvidoriaSigilo`) ou para o ponto de resposta. */
  manifestanteNome: string | null;
  registradaEm: string;
  prazoRespostaEm: string;
  prazoAreaEm: string | null;
  diasAtraso: number;
  atrasada: boolean;
  areaAtrasada: boolean;
  ultimaAtividadeEm: string;
  responsavelNome: string | null;
};

export type AnexoDto = {
  id: string;
  midiaId: string;
  nomeArquivo: string;
  visivelAoCidadao: boolean;
  criadoEm: string;
};

export type EventoDto = {
  id: string;
  tipo: OuvidoriaTipoEvento;
  statusAnterior: OuvidoriaStatus | null;
  statusNovo: OuvidoriaStatus | null;
  autorNome: string | null;
  pontoRespostaNome: string | null;
  texto: string | null;
  visivelAoCidadao: boolean;
  criadoEm: string;
  anexos: AnexoDto[];
};

export type ManifestacaoDetalheDto = ManifestacaoListaDto & {
  teor: string;
  teorPseudonimizado: string | null;
  /** Null quando `identidadeRestrita` — a UI NUNCA renderiza dados do manifestante nesse caso. */
  manifestante: ManifestanteDto | null;
  identidadeRestrita: boolean;
  referido: ReferidoDto | null;
  envolvidoPractitionerId: string | null;
  envolvidoDescricao: string | null;
  assuntoId: string | null;
  subassuntoId: string | null;
  unidadeId: string | null;
  pontoRespostaId: string | null;
  regulacaoSolicitacaoId: string | null;
  protocoloExterno: string | null;
  sistemaExterno: string | null;
  dataFato: string | null;
  localFato: string | null;
  origem: OuvidoriaOrigem;
  prorrogadoEm: string | null;
  prorrogacaoJustificativa: string | null;
  complementacaoUsada: boolean;
  encaminhadaEm: string | null;
  respondidaEm: string | null;
  concluidaEm: string | null;
  resolutividade: OuvidoriaResolutividade | null;
  situacaoFinal: OuvidoriaSituacaoFinal | null;
  motivoNaoAtendimento: OuvidoriaMotivoNaoAtendimento | null;
  motivoArquivamento: OuvidoriaMotivoArquivamento | null;
  respostaConclusiva: string | null;
  habilitadaEm: string | null;
  responsavelId: string | null;
  marcadorIds: string[];
  /** Protocolos de manifestações parecidas (mesmo CPF + assunto + unidade em 90 dias). A decisão é humana. */
  possiveisDuplicatas: string[];
  eventos: EventoDto[];
  anexos: AnexoDto[];
  /** Nomes das ações válidas para o status + perfil, calculados pelo backend. */
  acoesPermitidas: string[];
};

export type AnexoRef = { midiaId: string; nomeArquivo: string };

export type RegistrarManifestacaoRequest = {
  tipo: OuvidoriaTipo;
  identificacao: OuvidoriaIdentificacao;
  canal: OuvidoriaCanal;
  origem: OuvidoriaOrigem;
  teor: string;
  resumo?: string | null;
  assuntoId?: string | null;
  subassuntoId?: string | null;
  unidadeId?: string | null;
  dataFato?: string | null;
  localFato?: string | null;
  manifestante?: ManifestanteDto | null;
  referido?: ReferidoDto | null;
  envolvidoDescricao?: string | null;
  protocoloExterno?: string | null;
  sistemaExterno?: string | null;
  regulacaoSolicitacaoId?: string | null;
  anexos: AnexoRef[];
};

export type TriarRequest = {
  tipo?: OuvidoriaTipo | null;
  assuntoId?: string | null;
  subassuntoId?: string | null;
  prioridade?: OuvidoriaPrioridade | null;
  unidadeId?: string | null;
  resumo?: string | null;
  responsavelId?: string | null;
  regulacaoSolicitacaoId?: string | null;
  marcadorIds?: string[] | null;
};

export type EncaminharRequest = {
  pontoRespostaId: string;
  prazoDias?: number | null;
  texto?: string | null;
  teorPseudonimizado?: string | null;
};

export type TextoRequest = { texto: string };
export type TextoComAnexosRequest = { texto: string; anexos: AnexoRef[] };

export type ResponderCidadaoRequest = {
  texto: string;
  conclusiva: boolean;
  resolutividade?: OuvidoriaResolutividade | null;
  situacaoFinal?: OuvidoriaSituacaoFinal | null;
  motivoNaoAtendimento?: OuvidoriaMotivoNaoAtendimento | null;
};

export type ArquivarRequest = { motivo: OuvidoriaMotivoArquivamento; texto?: string | null };
export type EncaminharExternoRequest = { sistemaExterno: string; protocoloExterno?: string | null; texto: string };

export type OuvidoriaResumoDto = {
  registradas: number;
  emTriagem: number;
  encaminhadas: number;
  aguardandoComplementacao: number;
  aguardandoValidacao: number;
  atrasadas: number;
  areaAtrasadas: number;
  emRecurso: number;
  /** Encaminhadas aos pontos de que o usuário é membro. */
  meuPonto: number;
};

export type PontoRespostaMembroDto = { usuarioId: string; nome: string; titular: boolean };

export type PontoRespostaDto = {
  id: string;
  nome: string;
  tipo: OuvidoriaTipoPontoResposta;
  unidadeId: string | null;
  unidadeNome: string | null;
  prazoDias: number | null;
  ativo: boolean;
  membros: PontoRespostaMembroDto[];
  /** Encaminhadas em aberto. */
  pendentes: number;
};

export type SalvarMembroRequest = { usuarioId: string; titular: boolean };
export type SalvarPontoRespostaRequest = {
  nome: string;
  tipo: OuvidoriaTipoPontoResposta;
  unidadeId?: string | null;
  prazoDias?: number | null;
  ativo: boolean;
  membros: SalvarMembroRequest[];
};

export type AssuntoDto = {
  id: string;
  paiId: string | null;
  nome: string;
  codigoOuvidorSus: string | null;
  ordem: number;
  ativo: boolean;
};
export type SalvarAssuntoRequest = {
  paiId?: string | null;
  nome: string;
  codigoOuvidorSus?: string | null;
  ordem: number;
  ativo: boolean;
};

export type MarcadorDto = { id: string; nome: string; ativo: boolean };
export type SalvarMarcadorRequest = { nome: string; ativo: boolean };

export type OuvidoriaConfiguracaoDto = {
  prazoCidadaoDias: number;
  prorrogacaoDias: number;
  prazoAreaDias: number;
  prazoAreaAltaDias: number;
  prazoAreaUrgenteDiasUteis: number;
  complementacaoDias: number;
  arquivamentoAutomaticoDias: number;
  notificarPorWhatsApp: boolean;
  /** `{protocolo}` e `{prazo}` são substituídos no envio. */
  textoRecibo: string | null;
};

export type ContagemDto = { chave: string; rotulo: string; quantidade: number };

export type OuvidoriaPainelDto = {
  total: number;
  porTipo: ContagemDto[];
  porStatus: ContagemDto[];
  porCanal: ContagemDto[];
  porAssunto: ContagemDto[];
  porUnidade: ContagemDto[];
  respondidas: number;
  noPrazo: number;
  foraPrazo: number;
  tempoMedioDias: number | null;
  tempoMedioAreaDias: number | null;
  estoque: number;
  resolvidas: number;
  naoResolvidas: number;
  /** Chaves: ate30 | 31a60 | mais60. */
  faixasPrazo: ContagemDto[];
};

// ---- Filtro da listagem (§3.1, query string) ----

export type FiltroManifestacoes = {
  status?: OuvidoriaStatus[];
  tipo?: OuvidoriaTipo | '';
  unidadeId?: string;
  pontoRespostaId?: string;
  prioridade?: OuvidoriaPrioridade | '';
  atrasadas?: boolean;
  aguardandoValidacao?: boolean;
  busca?: string;
  de?: string;
  ate?: string;
  pagina: number;
  tamanho: number;
};
