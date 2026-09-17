/**
 * Fila do SERNIT (SER de Niterói) espelhada na nossa base — subsistema irmão do SER-RJ (ADR-0042).
 *
 * A tela lê o NOSSO banco, não o SERNIT: quem fala com o SERNIT é o motor de varredura, em
 * background. Espelho do `features/ser/types.ts`, sem o ramo "ambulatório estadual" (o SERNIT não o
 * tem) e com `idSernit` no lugar de `idSer`.
 */

import type { CategoriaFollowUp } from '@/shared/regulacao/categoriasFollowUp';
import type { EventoResumoExterno, TipoEventoExterno } from '@/shared/regulacao/eventosExternos';

export const SITUACOES_SERNIT = [
  'EmFila',
  'Pendente',
  'Agendada',
  'ChegadaNaoConfirmada',
  'ChegadaConfirmada',
  'Cancelada',
  'Alta',
] as const;

export type SituacaoSernit = (typeof SITUACOES_SERNIT)[number];

export const ROTULO_SITUACAO: Record<SituacaoSernit, string> = {
  EmFila: 'Em fila',
  Pendente: 'Pendente',
  Agendada: 'Agendada',
  ChegadaNaoConfirmada: 'Chegada não confirmada',
  ChegadaConfirmada: 'Chegada confirmada',
  Cancelada: 'Cancelada',
  Alta: 'Alta',
};

export type TipoRecursoSernit = 'Consulta' | 'Exame';

export type ModoVarreduraSernit = 'CargaInicial' | 'Diaria' | 'SomenteGrade';

export type StatusVarreduraSernit =
  | 'Pendente'
  | 'EmExecucao'
  | 'Concluida'
  | 'Parcial'
  | 'Erro'
  | 'Cancelada'
  | 'Interrompida';

export type FaseVarreduraSernit = 'Grade' | 'Historico' | 'Finalizada';

export type SolicitacaoSernitLista = {
  id: string;
  /** "ID Solicitação" do SERNIT. */
  idSernit: string;
  tipo: TipoRecursoSernit;
  recurso: string;
  dataSolicitacao: string | null;
  pacienteNome: string;
  pacienteId: string | null;
  idadeTexto: string | null;
  cpf: string | null;
  cns: string | null;
  cid: string | null;
  solicitanteNome: string | null;
  municipioSolicitante: string | null;
  agendadoParaTexto: string | null;
  situacao: SituacaoSernit;
  situacaoAnterior: SituacaoSernit | null;
  situacaoMudouEm: string | null;
  sincronizadoEm: string;
  historicoLidoEm: string | null;
  eventosCount: number;
  historicoIndisponivel: boolean;
  diasNaFila: number | null;
};

export type EventoSernit = {
  id: string;
  dataEvento: string;
  evento: string;
  estadoAnterior: string | null;
  estadoAtual: string | null;
  centralRegulacao: string | null;
  unidadeExecutora: string | null;
  usuario: string | null;
  lotacaoEvento: string | null;
  ip: string | null;
  observacao: string | null;
};

export type SolicitacaoSernitDetalhe = {
  resumo: SolicitacaoSernitLista;
  nomeMae: string | null;
  sexo: string | null;
  dataNascimento: string | null;
  etnia: string | null;
  cep: string | null;
  uf: string | null;
  municipioPaciente: string | null;
  bairro: string | null;
  tipoLogradouro: string | null;
  logradouro: string | null;
  numero: string | null;
  complemento: string | null;
  telefoneResidencial: string | null;
  telefoneWhatsapp: string | null;
  telefoneContato: string | null;
  eventos: EventoSernit[];
};

export type BuscaSernitFiltro = {
  situacao?: SituacaoSernit;
  tipo?: TipoRecursoSernit;
  termo?: string;
  dataSolicitacaoInicio?: string;
  dataSolicitacaoFim?: string;
  mudouDesde?: string;
  pagina?: number;
  tamanho?: number;
};

export type BuscaSernitResultado = {
  itens: SolicitacaoSernitLista[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type ResumoSituacaoSernit = { situacao: SituacaoSernit; quantidade: number };

export type ExecucaoSernit = {
  id: string;
  modo: ModoVarreduraSernit;
  disparo: 'Manual' | 'Agendado';
  status: StatusVarreduraSernit;
  janelaInicio: string;
  janelaFim: string;
  situacoesVarridas: string;
  buscas: number;
  paginas: number;
  solicitacoesEncontradas: number;
  solicitacoesNovas: number;
  solicitacoesAtualizadas: number;
  mudancasSituacao: number;
  historicosLidos: number;
  eventosNovos: number;
  followUpsNovos: number;
  historicosIndisponiveis: number;
  gatilhosGerados: number;
  fatiasTruncadas: number;
  mensagemErro: string | null;
  iniciadoEm: string;
  finalizadoEm: string | null;
  duracaoSegundos: number | null;
  criadoPorNome: string | null;
  fase: FaseVarreduraSernit;
  cursorSituacao: SituacaoSernit | null;
  cursorData: string | null;
  cursorIdSernit: string | null;
  historicosPendentes: number;
  retomadas: number;
  retomadaEm: string | null;
  ultimoSinalEm: string | null;
};

export type StatusMotorSernit = {
  credencialConfigurada: boolean;
  varreduraEmAndamento: boolean;
  totalSolicitacoes: number;
  totalEventos: number;
  gatilhosPendentes: number;
  ultimaExecucao: ExecucaoSernit | null;
};

export type DispararVarreduraSernitPayload = {
  modo: ModoVarreduraSernit;
  inicio?: string;
  fim?: string;
  situacoes?: SituacaoSernit[];
};

// ---------------------------------------------------------------- notificações

export type TipoGatilhoSernit =
  | 'MudancaSituacao'
  | 'NovoFollowUp'
  | 'NovaSolicitacao'
  | 'MudancaAgendamento';

export type NotificacaoSernit = {
  id: string;
  solicitacaoId: string;
  idSernit: string;
  tipo: TipoGatilhoSernit;
  situacaoAnterior: SituacaoSernit | null;
  situacaoAtual: SituacaoSernit | null;
  criadoEm: string;
  tipoRecurso: TipoRecursoSernit | null;
  pacienteNome: string | null;
  pacienteId: string | null;
  recurso: string | null;
  dataSolicitacao: string | null;
  agendadoParaTexto: string | null;
  unidadeExecutora: string | null;
  /** FollowUP mais recente da solicitação (em qualquer notificação). Null se nunca houve. */
  ultimoFollowUp: FollowUpResumoSernit | null;
  /** Último evento da trilha, de qualquer verbo. Null se o histórico ainda não foi lido. */
  ultimoEvento: EventoResumoExterno | null;
  /** Técnico regulador que incluiu a solicitação (chave normalizada). Null se o histórico não foi lido. */
  tecnico: string | null;
};

export type FollowUpResumoSernit = {
  dataEvento: string;
  usuario: string | null;
  observacao: string | null;
  /** Categoria do classificador de FollowUP (FalhaContato, SemVaga…). Null enquanto o worker
   * não classificou; 'Outro' quando nenhuma regra casou. */
  categoria: CategoriaFollowUp | null;
};

export type NotificacoesSernitPagina = {
  itens: NotificacaoSernit[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type NotificacaoSernitContador = {
  tipo: TipoRecursoSernit | null;
  situacao: SituacaoSernit;
  quantidade: number;
};

export type NotificacoesSernitResumo = {
  total: number;
  contadores: NotificacaoSernitContador[];
};

export type NotificacoesSernitFiltro = {
  tipo?: TipoRecursoSernit;
  situacao?: SituacaoSernit;
  tipoGatilho?: TipoGatilhoSernit;
  /** Só solicitações cujo ÚLTIMO FollowUP tem esta categoria. */
  categoriaFollowUp?: CategoriaFollowUp;
  /** Só solicitações cujo ÚLTIMO evento da trilha (qualquer verbo) é deste tipo. */
  tipoUltimoEvento?: TipoEventoExterno;
  /** Só solicitações incluídas por estes técnicos (chaves). Vazio = todos. */
  tecnicos?: string[];
  pagina?: number;
  tamanho?: number;
};

/** Disparo diário do motor: ligado/desligado e a que horas (Brasília). */
export type VarreduraAutomaticaSernit = {
  ativo: boolean;
  /** `HH:mm`. */
  horaLocal: string;
};

// ---------------------------------------------------------------- nova solicitação
// O formulário de criação do SERNIT, lido AO VIVO. Sem "ambulatório estadual".

export type OpcaoSernit = { valor: string; rotulo: string };

export type CampoDinamicoSernit = {
  numero: string;
  campo: string;
  rotulo: string;
  /** `text`, `textarea`, `select`, `radio`, `checkbox` ou `date`. */
  tipo: string;
  obrigatorio: boolean;
  opcoes: OpcaoSernit[] | null;
};

export type FormularioNovaSernit = {
  tipos: OpcaoSernit[];
  classificacoesRisco: OpcaoSernit[];
  medicos: OpcaoSernit[];
  camposDinamicosPadrao: CampoDinamicoSernit[];
};

// ---------------------------------------------------------------- catálogo + rascunhos

export type StatusRascunhoSernit = 'Rascunho' | 'Pronto' | 'Enviado' | 'Falhou';

export type CatalogoRecursoSernit = {
  tipo: TipoRecursoSernit;
  valor: string;
  rotulo: string;
  /** false = os campos dinâmicos deste recurso ainda não foram copiados do SERNIT. */
  camposLidos: boolean;
};

export type CatalogoFormularioSernit = {
  classificacoesRisco: OpcaoSernit[];
  medicos: OpcaoSernit[];
  recursos: CatalogoRecursoSernit[];
  sincronizadoEm: string | null;
  recursosSemCampos: number;
  cidsCopiados: number;
  recursosSemCid: number;
  copiaEmAndamento: boolean;
  ultimoErro: string | null;
};

export type CidSernit = {
  codigo: string;
  descricao: string;
  /** O que o SERNIT escreve no campo ao clicar (`(A09 ) Diarréia…`) — vai de volta em `procedimento`. */
  texto: string;
};

export type SugestoesCidSernit = {
  itens: CidSernit[];
  truncado: boolean;
};

export type AnexoRascunhoSernit = {
  id: string;
  midiaId: string;
  nomeArquivo: string;
  contentType: string | null;
  tamanho: number;
  enviadoEm: string | null;
  criadoEm: string;
};

export type RascunhoSernitLista = {
  id: string;
  status: StatusRascunhoSernit;
  tipo: TipoRecursoSernit | null;
  recursoRotulo: string | null;
  pacienteNome: string | null;
  cns: string | null;
  hipotese: string | null;
  idSernitGerado: string | null;
  criadoPorNome: string | null;
  criadoEm: string;
  atualizadoEm: string | null;
  enviadoEm: string | null;
  anexos: number;
};

export type RascunhoSernitDetalhe = {
  id: string;
  status: StatusRascunhoSernit;
  tipo: TipoRecursoSernit | null;
  recursoValor: string | null;
  recursoRotulo: string | null;
  cns: string | null;
  pacienteNome: string | null;
  hipotese: string | null;
  campos: Record<string, string>;
  idSernitGerado: string | null;
  mensagemErro: string | null;
  criadoPorNome: string | null;
  criadoEm: string;
  atualizadoEm: string | null;
  enviadoEm: string | null;
  anexos: AnexoRascunhoSernit[];
};

export type RascunhoSernitRequest = {
  tipo?: TipoRecursoSernit;
  recursoValor?: string;
  recursoRotulo?: string;
  cns?: string;
  pacienteNome?: string;
  hipotese?: string;
  campos?: Record<string, string>;
};

export type CatalogoSyncSernitResultado = {
  recursos: number;
  campos: number;
  listas: number;
  cids: number;
  falhas: number;
  duracaoSegundos: number;
};

/** Um campo do cadastro do paciente como o SERNIT devolve na pesquisa por CNS/CPF. */
export type CampoPacienteSernit = {
  campo: string;
  rotulo: string;
  valor: string | null;
  tipo: 'text' | 'select';
  obrigatorio: boolean;
  /** Identidade travada (disabled) não é editável; endereço/telefones sim. */
  editavel: boolean;
  opcoes: OpcaoSernit[] | null;
};

export type PacienteEncontradoSernit = {
  encontrado: boolean;
  avisos: string[];
  campos: CampoPacienteSernit[];
  pacienteIdNosso: string | null;
  telefoneVerificadoNosso: string | null;
};

// ---------------------------------------------------------------- escrita no SERNIT

export type SessaoOperadorSernit = {
  autenticado: boolean;
  usuarioSernit: string | null;
  autenticadaEm: string | null;
  expiraEm: string | null;
};

/** Um evento lido ao vivo (conferência do FollowUP) — espelha SernitEventoDiretoDto do backend. */
export type EventoDiretoSernit = {
  data: string | null;
  evento: string | null;
  estadoAnterior: string | null;
  estadoAtual: string | null;
  centralRegulacao: string | null;
  unidadeExecutora: string | null;
  usuario: string | null;
  lotacaoEvento: string | null;
  ip: string | null;
  observacao: string | null;
};

export type FollowUpResultadoSernit = {
  idSernit: string;
  mensagemDoSer: string;
  evento: EventoDiretoSernit;
  eventosNovos: number;
};

/** `editavel` é falso nas situações terminais (Cancelada, Alta). No SERNIT, editar exige CPF no cadastro. */
export type ContatosSernit = {
  residencial: string | null;
  whatsApp: string | null;
  contato: string | null;
  editavel: boolean;
  motivoNaoEditavel: string | null;
};

/** Campo ausente = não mexer; string vazia = limpar. */
export type AlterarContatosSernit = Partial<{
  residencial: string;
  whatsapp: string;
  contato: string;
}>;
