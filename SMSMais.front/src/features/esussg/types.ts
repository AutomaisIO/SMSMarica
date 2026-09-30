/**
 * Espelho da fila e dos agendados que o município tem no **ESUS de São Gonçalo** (ADR-0063) — o
 * produto ESUS (saogoncalo.esusmais.com.br), NÃO o e-SUS do governo. Terceiro irmão do SER e do
 * SERNIT, em tabelas próprias `esussg_*`.
 *
 * A tela lê o NOSSO banco, não o ESUS: quem fala com o ESUS é o motor de varredura, em
 * background. A integração é só leitura — nada daqui escreve no ESUS.
 *
 * Espelha `EsusSgDtos.cs`, `EsusSgNotificacaoService.cs` e os enums de `Entities/EsusSg` (que
 * viajam como string).
 */

import type {
  AnaliseRegrasContagem,
  AnaliseRegrasDetalhe,
  AnaliseRegrasResumo,
  VereditoAnaliseRegras,
} from '@/shared/regulacao/analiseRegras/tipos';
import type { TipoEventoExterno } from '@/shared/regulacao/eventosExternos';

export const SITUACOES_ESUSSG = ['EmFila', 'Pendente', 'Agendada', 'SaiuDaFila'] as const;

export type SituacaoEsusSg = (typeof SITUACOES_ESUSSG)[number];

export const ROTULO_SITUACAO_ESUSSG: Record<SituacaoEsusSg, string> = {
  EmFila: 'Em fila',
  Pendente: 'Pendente',
  Agendada: 'Agendada',
  SaiuDaFila: 'Saiu da fila',
};

/**
 * O que cada situação quer dizer. A situação é DERIVADA das duas telas que a conta do município
 * enxerga no ESUS ("Fila de Regulação" e "Pacientes Agendados pela Fila") — por isso "Saiu da
 * fila" é declarado, não adivinhado: o motivo (exclusão, cancelamento, transferência) não é visível.
 */
export const DICA_SITUACAO_ESUSSG: Record<SituacaoEsusSg, string> = {
  EmFila: 'Na Fila de Regulação do ESUS, sem pendência.',
  Pendente: 'Na Fila de Regulação do ESUS com pendência ativa.',
  Agendada: 'Saiu da fila com data marcada (Pacientes Agendados pela Fila).',
  SaiuDaFila:
    'Não consta mais na fila nem nos agendados do ESUS; a conta do município não vê o motivo.',
};

export const TIPOS_RECURSO_ESUSSG = ['Consulta', 'Exame'] as const;

export type TipoRecursoEsusSg = (typeof TIPOS_RECURSO_ESUSSG)[number];

export type ModoVarreduraEsusSg = 'CargaInicial' | 'Diaria' | 'SomenteFila';

export type FaseVarreduraEsusSg = 'Fila' | 'Agendados' | 'Finalizada';

export type StatusVarreduraEsusSg =
  | 'Pendente'
  | 'EmExecucao'
  | 'Concluida'
  | 'Parcial'
  | 'Erro'
  | 'Cancelada'
  | 'Interrompida';

export type TipoGatilhoEsusSg =
  | 'MudancaSituacao'
  | 'NovaSolicitacao'
  | 'MudancaAgendamento'
  | 'MudancaPrioridade';

export type SolicitacaoEsusSgLista = {
  id: string;
  /** Número do pedido no ESUS (`fil_id`) — o mesmo na fila e nos agendados. */
  idEsusSg: string;
  tipo: TipoRecursoEsusSg;
  /** Procedimento como o ESUS escreve ("TRATAMENTO DE RETINA (PPI)"). */
  recurso: string;
  /** Data do pedido médico (DateOnly). */
  dataSolicitacao: string | null;
  /** Entrada na fila do ESUS (DateOnly) — a régua da espera. */
  dataEntradaFila: string | null;
  diasNaFila: number | null;
  prioridade: string | null;
  /** Cor que o ESUS dá à prioridade, no formato dele (`0xd02224`). */
  prioridadeCor: string | null;
  pendencia: string | null;
  /** Posição regulada na fila do procedimento. Só enquanto na fila. */
  posicaoFila: number | null;
  pacienteNome: string;
  /** Id no nosso hub — null quando ainda não conciliado. */
  pacienteId: string | null;
  cpf: string | null;
  cns: string | null;
  dataNascimento: string | null;
  unidadeExecutora: string | null;
  /** DateOnly. */
  dataAgendada: string | null;
  /** Como o ESUS formata ("06/10/2026 13:15:00") — hora local de Brasília. */
  dataHoraAgendadaTexto: string | null;
  notificacaoResposta: string | null;
  situacao: SituacaoEsusSg;
  situacaoAnterior: SituacaoEsusSg | null;
  situacaoMudouEm: string | null;
  sincronizadoEm: string;
  eventosCount: number;
  analise?: AnaliseRegrasResumo | null;
};

export type EventoEsusSg = {
  id: string;
  dataEvento: string;
  /** Rótulo do marco ("Inclusão na fila", "Agendamento", "Saiu da fila"…). */
  evento: string;
  tipoEvento: TipoEventoExterno;
  estadoAnterior: string | null;
  estadoAtual: string | null;
  unidadeExecutora: string | null;
  usuario: string | null;
  lotacaoEvento: string | null;
  observacao: string | null;
};

export type SolicitacaoEsusSgDetalhe = {
  resumo: SolicitacaoEsusSgLista;
  /** Código interno do ESUS — NÃO é SIGTAP (o mesmo valor aparece em procedimentos diferentes). */
  codigoInterno: string | null;
  subprocedimentos: string | null;
  profissionalSolicitante: string | null;
  unidadeSolicitante: string | null;
  /** Quem incluiu o pedido na fila do ESUS (servidor do município). */
  usuarioInclusao: string | null;
  regulador: string | null;
  ordemEntrada: number | null;
  sexo: string | null;
  nomeMae: string | null;
  telefone: string | null;
  celular: string | null;
  municipioPaciente: string | null;
  bairro: string | null;
  cnesExecutora: string | null;
  setor: string | null;
  local: string | null;
  /** Operador de São Gonçalo que marcou. */
  usuarioAgendamento: string | null;
  agendamentoCadastradoEm: string | null;
  dataSaidaFila: string | null;
  comprovanteImpresso: boolean | null;
  agendadoTfd: boolean | null;
  notificacaoTipo: string | null;
  notificacaoEntrega: string | null;
  vistoNaFilaEm: string | null;
  vistoNosAgendadosEm: string | null;
  eventos: EventoEsusSg[];
  analise?: AnaliseRegrasDetalhe | null;
};

export type BuscaEsusSgFiltro = {
  situacao?: SituacaoEsusSg;
  tipo?: TipoRecursoEsusSg;
  /** Nome, CPF, CNS ou número do pedido no ESUS. */
  termo?: string;
  recurso?: string;
  prioridade?: string;
  veredito?: VereditoAnaliseRegras;
  entradaInicio?: string;
  entradaFim?: string;
  agendadaInicio?: string;
  agendadaFim?: string;
  mudouDesde?: string;
  pagina?: number;
  tamanho?: number;
};

export type BuscaEsusSgResultado = {
  itens: SolicitacaoEsusSgLista[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type ContagemTextoEsusSg = { texto: string; quantidade: number };

export type ResumoEsusSg = {
  porSituacao: { situacao: SituacaoEsusSg; quantidade: number }[];
  porVeredito: AnaliseRegrasContagem[];
  porRecurso: ContagemTextoEsusSg[];
  agendadosProximos30Dias: number;
};

export type ExecucaoEsusSg = {
  id: string;
  modo: ModoVarreduraEsusSg;
  disparo: 'Manual' | 'Agendado';
  status: StatusVarreduraEsusSg;
  janelaInicio: string;
  janelaFim: string;
  requisicoes: number;
  naFila: number;
  agendadosLidos: number;
  solicitacoesNovas: number;
  solicitacoesAtualizadas: number;
  mudancasSituacao: number;
  saidasDaFila: number;
  eventosNovos: number;
  gatilhosGerados: number;
  /** Meses de agendados cuja contagem não fechou (lido ≠ declarado). > 0 = cobertura incompleta. */
  mesesIncompletos: number;
  mensagemErro: string | null;
  iniciadoEm: string;
  finalizadoEm: string | null;
  duracaoSegundos: number | null;
  criadoPorNome: string | null;
  fase: FaseVarreduraEsusSg;
  /** Mês de agendados em leitura (DateOnly, dia 1). */
  cursorMes: string | null;
  retomadas: number;
  retomadaEm: string | null;
  ultimoSinalEm: string | null;
};

export type StatusMotorEsusSg = {
  credencialConfigurada: boolean;
  usuario: string | null;
  cliente: string;
  varreduraEmAndamento: boolean;
  totalSolicitacoes: number;
  totalEventos: number;
  gatilhosPendentes: number;
  recursosNoCatalogo: number;
  ultimaExecucao: ExecucaoEsusSg | null;
};

export type DispararVarreduraEsusSgPayload = { modo: ModoVarreduraEsusSg };

export type CredencialEsusSg = { usuario: string; senha: string; cliente?: string | null };

/** Disparo diário do motor: ligado/desligado e a que horas (Brasília). */
export type VarreduraAutomaticaEsusSg = {
  ativo: boolean;
  /** `HH:mm`. */
  horaLocal: string;
};

export type CatalogoSyncEsusSgResultado = {
  lidos: number;
  novos: number;
  alterados: number;
  inativados: number;
  mudou?: boolean;
};

// ---------------------------------------------------------------- notificações

export type EventoResumoEsusSg = { tipo: TipoEventoExterno; evento: string; dataEvento: string };

export type NotificacaoEsusSg = {
  id: string;
  solicitacaoId: string;
  idEsusSg: string;
  tipo: TipoGatilhoEsusSg;
  situacaoAnterior: SituacaoEsusSg | null;
  situacaoAtual: SituacaoEsusSg | null;
  criadoEm: string;
  tipoRecurso: TipoRecursoEsusSg;
  pacienteNome: string;
  pacienteId: string | null;
  recurso: string;
  prioridade: string | null;
  dataEntradaFila: string | null;
  dataAgendada: string | null;
  dataHoraAgendadaTexto: string | null;
  unidadeExecutora: string | null;
  payloadJson: string | null;
  ultimoEvento: EventoResumoEsusSg | null;
  /** Quem incluiu o pedido na fila do ESUS (chave normalizada). */
  tecnico: string | null;
};

export type NotificacoesEsusSgPagina = {
  itens: NotificacaoEsusSg[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type NotificacaoEsusSgContador = {
  tipo: TipoRecursoEsusSg;
  situacao: SituacaoEsusSg;
  quantidade: number;
};

export type NotificacoesEsusSgResumo = {
  total: number;
  contadores: NotificacaoEsusSgContador[];
};

export type NotificacoesEsusSgFiltro = {
  tipo?: TipoRecursoEsusSg;
  situacao?: SituacaoEsusSg;
  tipoGatilho?: TipoGatilhoEsusSg;
  /** Só pedidos incluídos por estes técnicos (chaves). Vazio = todos. */
  tecnicos?: string[];
  pagina?: number;
  tamanho?: number;
};
