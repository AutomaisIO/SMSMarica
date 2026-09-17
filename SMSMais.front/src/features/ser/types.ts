/**
 * Fila do SER (Sistema Estadual de Regulação, SES-RJ) espelhada na nossa base — ADR-0042.
 *
 * A tela lê o NOSSO banco, não o SER: quem fala com o SER é o motor de varredura, em background.
 * Os campos espelham as colunas da grade do SER de propósito, para o operador reconhecer a tela.
 */

import type { CategoriaFollowUp } from '@/shared/regulacao/categoriasFollowUp';
import type { EventoResumoExterno, TipoEventoExterno } from '@/shared/regulacao/eventosExternos';

/** Situações do SER. A ordem aqui é a da fila (do que espera para o que terminou). */
export const SITUACOES_SER = [
  'EmFila',
  'Pendente',
  'Agendada',
  'ChegadaNaoConfirmada',
  'ChegadaConfirmada',
  'Cancelada',
  'Alta',
] as const;

export type SituacaoSer = (typeof SITUACOES_SER)[number];

export const ROTULO_SITUACAO: Record<SituacaoSer, string> = {
  EmFila: 'Em fila',
  Pendente: 'Pendente',
  Agendada: 'Agendada',
  ChegadaNaoConfirmada: 'Chegada não confirmada',
  ChegadaConfirmada: 'Chegada confirmada',
  Cancelada: 'Cancelada',
  Alta: 'Alta',
};

export type TipoRecursoSer = 'Consulta' | 'Exame';

export type ModoVarreduraSer = 'CargaInicial' | 'Diaria' | 'SomenteGrade';

export type StatusVarreduraSer =
  | 'Pendente'
  | 'EmExecucao'
  | 'Concluida'
  /** Terminou, mas alguma fatia estourou o teto do SER — há registros NÃO lidos. */
  | 'Parcial'
  | 'Erro'
  | 'Cancelada'
  /** Parada por queda/deploy do serviço e RETOMÁVEL — o runner continua do ponteiro. */
  | 'Interrompida';

/** Em que ponto a rodada está. Grade = espelho da fila; Historico = trilha de eventos. */
export type FaseVarreduraSer = 'Grade' | 'Historico' | 'Finalizada';

export type SolicitacaoSerLista = {
  id: string;
  /** "ID Solicitação" do SER — é a chave que o operador usa para falar com a central. */
  idSer: string;
  tipo: TipoRecursoSer;
  recurso: string;
  dataSolicitacao: string | null;
  pacienteNome: string;
  /** Id no nosso hub FHIR — null quando ainda não conciliado. Libera o resumo e o WhatsApp. */
  pacienteId: string | null;
  idadeTexto: string | null;
  cpf: string | null;
  cns: string | null;
  cid: string | null;
  solicitanteNome: string | null;
  municipioSolicitante: string | null;
  agendadoParaTexto: string | null;
  situacao: SituacaoSer;
  situacaoAnterior: SituacaoSer | null;
  situacaoMudouEm: string | null;
  sincronizadoEm: string;
  historicoLidoEm: string | null;
  eventosCount: number;
  /** Solicitações em Alta não têm histórico no SER — o menu não oferece o item. */
  historicoIndisponivel: boolean;
  diasNaFila: number | null;
};

export type EventoSer = {
  id: string;
  dataEvento: string;
  /** Verbo do SER: Solicitar, FollowUP, Pendenciar, Cancelar. */
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

export type SolicitacaoSerDetalhe = {
  resumo: SolicitacaoSerLista;
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
  eventos: EventoSer[];
};

export type BuscaSerFiltro = {
  situacao?: SituacaoSer;
  tipo?: TipoRecursoSer;
  /** Busca livre: nome, CPF, CNS, ID do SER e recurso. */
  termo?: string;
  dataSolicitacaoInicio?: string;
  dataSolicitacaoFim?: string;
  mudouDesde?: string;
  pagina?: number;
  tamanho?: number;
};

export type BuscaSerResultado = {
  itens: SolicitacaoSerLista[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type ResumoSituacaoSer = { situacao: SituacaoSer; quantidade: number };

export type ExecucaoSer = {
  id: string;
  modo: ModoVarreduraSer;
  disparo: 'Manual' | 'Agendado';
  status: StatusVarreduraSer;
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
  /** > 0 significa cobertura incompleta: existem registros que NÃO foram lidos. */
  fatiasTruncadas: number;
  mensagemErro: string | null;
  iniciadoEm: string;
  finalizadoEm: string | null;
  duracaoSegundos: number | null;
  criadoPorNome: string | null;
  /** Ponteiro de retomada: onde a rodada está e de onde ela continua se o serviço cair. */
  fase: FaseVarreduraSer;
  cursorSituacao: SituacaoSer | null;
  cursorData: string | null;
  cursorIdSer: string | null;
  historicosPendentes: number;
  retomadas: number;
  retomadaEm: string | null;
  /**
   * Sinal de vida: quando o motor gravou progresso pela última vez. É o que distingue
   * "trabalhando numa fatia grande" de "pendurada" — contador parado, sozinho, não diz qual.
   */
  ultimoSinalEm: string | null;
};

export type StatusMotorSer = {
  credencialConfigurada: boolean;
  varreduraEmAndamento: boolean;
  totalSolicitacoes: number;
  totalEventos: number;
  gatilhosPendentes: number;
  ultimaExecucao: ExecucaoSer | null;
};

export type DispararVarreduraPayload = {
  modo: ModoVarreduraSer;
  inicio?: string;
  fim?: string;
  situacoes?: SituacaoSer[];
};

// ---- Consulta DIRETA (tela de testes: vai ao SER ao vivo, nada é gravado) ----

export type ConsultaDiretaFiltro = {
  situacao: SituacaoSer;
  tipo?: TipoRecursoSer;
  dataSolicitacaoInicio?: string;
  dataSolicitacaoFim?: string;
  cpf?: string;
  nome?: string;
  cns?: string;
  idSolicitacao?: string;
  pagina?: number;
  /**
   * Consultar pela tela de Histórico (export de 500) em vez da tela de Solicitação (teto de 100).
   * É o caminho que a varredura usa de verdade — e o único em que a contagem significa algo.
   */
  porExport?: boolean;
  /**
   * No export, mandar o filtro de unidade solicitante. Existe para testar a suspeita de que esse
   * campo (autocomplete com hidden vazio) zera a consulta em silêncio.
   */
  filtrarPorSolicitante?: boolean;
};

/** Qual tela do SER respondeu. Muda o teto e o que a resposta prova. */
export type FonteConsultaSer = 'TelaSolicitacao' | 'ExportHistorico';

/** Linha crua do SER — tudo string, exatamente como o parser leu da grade. */
export type LinhaDiretaSer = {
  idSer: string;
  tipo: string | null;
  recurso: string | null;
  dataSolicitacao: string | null;
  paciente: string | null;
  idade: string | null;
  cpf: string | null;
  cns: string | null;
  cid: string | null;
  solicitante: string | null;
  municipioSolicitante: string | null;
  agendadoPara: string | null;
  situacao: string | null;
};

export type ConsultaDiretaResultado = {
  linhas: LinhaDiretaSer[];
  /** Páginas do datascroller. Sempre 0 no export: aquela tela não pagina. */
  paginas: number;
  /** Na tela de Solicitação, 5 páginas = corte de 100. No export, é o aviso do próprio SER. */
  bateuNoTeto: boolean;
  duracaoMs: number;
  fonte: FonteConsultaSer;
  /** O aviso de corte nas palavras do SER, ou null quando o lote veio inteiro. */
  avisoDoSer: string | null;
};

export type EventoDiretoSer = {
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

export type HistoricoDiretoResultado = {
  idSer: string;
  paciente: Record<string, string>;
  eventos: EventoDiretoSer[];
  duracaoMs: number;
};

// ---------------------------------------------------------------- notificações
// A fila de gatilhos do motor vista pela regulação: o que mudou no SER e ninguém olhou ainda.

export type TipoGatilhoSer =
  | 'MudancaSituacao'
  | 'NovoFollowUp'
  | 'NovaSolicitacao'
  | 'MudancaAgendamento';

export type NotificacaoSer = {
  id: string;
  /** Id interno da solicitação — o modal de detalhe busca por ele. */
  solicitacaoId: string;
  idSer: string;
  tipo: TipoGatilhoSer;
  situacaoAnterior: SituacaoSer | null;
  situacaoAtual: SituacaoSer | null;
  criadoEm: string;
  tipoRecurso: TipoRecursoSer | null;
  pacienteNome: string | null;
  /** Id no nosso hub FHIR — libera o resumo do paciente e o WhatsApp. Null se não conciliado. */
  pacienteId: string | null;
  recurso: string | null;
  dataSolicitacao: string | null;
  agendadoParaTexto: string | null;
  unidadeExecutora: string | null;
  /** FollowUP mais recente da solicitação (em qualquer notificação). Null se nunca houve. */
  ultimoFollowUp: FollowUpResumoSer | null;
  /** Último evento da trilha, de qualquer verbo. Null se o histórico ainda não foi lido. */
  ultimoEvento: EventoResumoExterno | null;
  /** Técnico regulador que incluiu a solicitação (chave normalizada). Null se o histórico não foi lido. */
  tecnico: string | null;
};

export type FollowUpResumoSer = {
  dataEvento: string;
  usuario: string | null;
  observacao: string | null;
  /** Categoria do classificador de FollowUP (FalhaContato, SemVaga…). Null enquanto o worker
   * não classificou; 'Outro' quando nenhuma regra casou. */
  categoria: CategoriaFollowUp | null;
};

export type NotificacoesPagina = {
  itens: NotificacaoSer[];
  total: number;
  pagina: number;
  tamanho: number;
};

/** Quantos movimentos por ler existem em cada (tipo de recurso, situação). */
export type NotificacaoContador = {
  tipo: TipoRecursoSer | null;
  situacao: SituacaoSer;
  quantidade: number;
};

export type NotificacoesResumo = {
  total: number;
  contadores: NotificacaoContador[];
};

export type NotificacoesFiltro = {
  tipo?: TipoRecursoSer;
  situacao?: SituacaoSer;
  tipoGatilho?: TipoGatilhoSer;
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
export type VarreduraAutomaticaSer = {
  ativo: boolean;
  /** `HH:mm`. */
  horaLocal: string;
};

// ---------------------------------------------------------------- nova solicitação
// O formulário de criação do SER, lido AO VIVO. Ver docs/ser-criar-solicitacao.md.

export type OpcaoSer = { valor: string; rotulo: string };

/**
 * Campo que o SER acrescenta conforme o Recurso escolhido — é o que faz oncologia pedir peso,
 * altura, IMC e datas de biópsia enquanto uma consulta comum pede só três textos.
 */
export type CampoDinamicoSer = {
  numero: string;
  /** Nome JSF do campo — é por ele que o valor viajaria no envio. */
  campo: string;
  rotulo: string;
  /** `text`, `textarea`, `select`, `radio` ou `checkbox`. */
  tipo: string;
  obrigatorio: boolean;
  opcoes: OpcaoSer[] | null;
};

export type FormularioNovaSer = {
  ambulatorioEstadual: OpcaoSer[];
  tipos: OpcaoSer[];
  classificacoesRisco: OpcaoSer[];
  medicos: OpcaoSer[];
  camposDinamicosPadrao: CampoDinamicoSer[];
};

// ---------------------------------------------------------------- catálogo + rascunhos
// Tudo local: o formulário vem do catálogo copiado, o pedido é guardado na nossa base.

export type StatusRascunhoSer = 'Rascunho' | 'Pronto' | 'Enviado' | 'Falhou';

export type CatalogoRecursoSer = {
  tipo: TipoRecursoSer;
  /**
   * Ramo de "É ambulatório estadual?" em que o recurso existe. Faz parte da IDENTIDADE: o mesmo
   * valor aparece nos dois ramos com formulários diferentes, e 31 consultas só existem no "Sim".
   */
  ambulatorioEstadual: boolean;
  valor: string;
  rotulo: string;
  /** false = os campos dinâmicos deste recurso ainda não foram copiados do SER. */
  camposLidos: boolean;
};

export type CatalogoFormularioSer = {
  ambulatorioEstadual: OpcaoSer[];
  classificacoesRisco: OpcaoSer[];
  medicos: OpcaoSer[];
  recursos: CatalogoRecursoSer[];
  /** Data do item MAIS ANTIGO: o catálogo só está tão atualizado quanto a parte mais velha. */
  sincronizadoEm: string | null;
  recursosSemCampos: number;
  /** Quantos CID o espelho tem, somando as listas. */
  cidsCopiados: number;
  /** Recursos que ainda não sabem qual lista de CID aceitam — esses caem no ao vivo. */
  recursosSemCid: number;
  /** A cópia está rodando agora, em segundo plano. */
  copiaEmAndamento: boolean;
  /** Motivo da última falha, quando houve. */
  ultimoErro: string | null;
};

/** Uma linha do autocomplete de CID da Hipótese, como o SER devolve. */
export type CidSer = {
  /** Código sem ponto, do jeito do SER: `A09`, `E119`. */
  codigo: string;
  descricao: string;
  /**
   * O que o SER escreve no campo ao clicar na sugestão — `(A09 ) Diarréia e gastroenterite…`.
   * É ISSO que o pedido leva de volta em `form0:procedimento`; guardar o código sozinho, ou só a
   * descrição, faz o SER gravar o pedido sem hipótese e responder "salva com sucesso".
   */
  texto: string;
};

export type SugestoesCidSer = {
  itens: CidSer[];
  /** O SER cortou no teto dele (500) — refine o termo, a lista não é toda a resposta. */
  truncado: boolean;
};

export type AnexoRascunhoSer = {
  id: string;
  midiaId: string;
  nomeArquivo: string;
  contentType: string | null;
  tamanho: number;
  /** Nulo = o arquivo ainda é só nosso; não subiu para o SER. */
  enviadoEm: string | null;
  criadoEm: string;
};

export type RascunhoSerLista = {
  id: string;
  status: StatusRascunhoSer;
  tipo: TipoRecursoSer | null;
  recursoRotulo: string | null;
  pacienteNome: string | null;
  cns: string | null;
  hipotese: string | null;
  idSerGerado: string | null;
  criadoPorNome: string | null;
  criadoEm: string;
  atualizadoEm: string | null;
  enviadoEm: string | null;
  anexos: number;
};

export type RascunhoSerDetalhe = {
  id: string;
  status: StatusRascunhoSer;
  tipo: TipoRecursoSer | null;
  /** Resposta a "É ambulatório estadual?" — decide os recursos e o formulário. */
  ambulatorioEstadual: boolean | null;
  recursoValor: string | null;
  recursoRotulo: string | null;
  cns: string | null;
  pacienteNome: string | null;
  hipotese: string | null;
  /** Valores com os nomes JSF do SER como chave — é o que seria postado. */
  campos: Record<string, string>;
  idSerGerado: string | null;
  mensagemErro: string | null;
  criadoPorNome: string | null;
  criadoEm: string;
  atualizadoEm: string | null;
  enviadoEm: string | null;
  anexos: AnexoRascunhoSer[];
};

export type RascunhoSerRequest = {
  tipo?: TipoRecursoSer;
  recursoValor?: string;
  recursoRotulo?: string;
  cns?: string;
  pacienteNome?: string;
  hipotese?: string;
  campos?: Record<string, string>;
};

export type CatalogoSyncResultado = {
  recursos: number;
  campos: number;
  listas: number;
  cids: number;
  falhas: number;
  duracaoSegundos: number;
};

/** Um campo do cadastro do paciente como o SER devolve na pesquisa por CNS/CPF. */
export type CampoPacienteSer = {
  /** Nome JSF. Dois dos telefones têm id POSICIONAL (j_idNNN) — nunca chumbar. */
  campo: string;
  rotulo: string;
  valor: string | null;
  tipo: 'text' | 'select';
  obrigatorio: boolean;
  /**
   * O SER trava a identidade (nome, CPF, CNS, nascimento, sexo, mãe, raça) com `disabled`, e
   * campo travado não é enviado pelo navegador: esses valores nem chegam ao Gravar. Editá-los
   * aqui seria oferecer uma digitação que o SER descarta.
   */
  editavel: boolean;
  opcoes: OpcaoSer[] | null;
};

export type PacienteEncontradoSer = {
  encontrado: boolean;
  /** Mensagens do SER — inclui o aviso de CNS definitivo × provisório. */
  avisos: string[];
  campos: CampoPacienteSer[];
  /** Id da mesma pessoa no NOSSO hub, quando o CPF do SER casa com alguém aqui. */
  pacienteIdNosso: string | null;
  /** Nosso telefone verificado por OTP. Vira sugestão ao lado do WhatsApp do SER. */
  telefoneVerificadoNosso: string | null;
};

// ---------------------------------------------------------------- escrita no SER

/**
 * Sessão de ESCRITA do operador no SER.
 *
 * A credencial cadastrada em Configuração é de SINCRONISMO e só lê. O SER assina cada evento com
 * o nome de quem fez, então escrever exige o login pessoal de quem está operando — senão toda
 * ação do município aparece no nome da mesma pessoa na trilha do Estado.
 */
export type SessaoOperadorSer = {
  autenticado: boolean;
  usuarioSer: string | null;
  autenticadaEm: string | null;
  expiraEm: string | null;
};


export type FollowUpResultadoSer = {
  idSer: string;
  /** O que o SER exibiu. NÃO é a prova — a prova é `evento`, achado na releitura. */
  mensagemDoSer: string;
  evento: EventoDiretoSer;
  eventosNovos: number;
};

/**
 * Os três telefones como o SER os tem agora.
 *
 * `editavel` é falso nas situações terminais (Cancelada, Alta): o SER mostra os números mas não
 * oferece o botão Editar no menu da linha.
 */
export type ContatosSer = {
  residencial: string | null;
  whatsApp: string | null;
  contato: string | null;
  editavel: boolean;
  motivoNaoEditavel: string | null;
};

/** Campo ausente = não mexer; string vazia = limpar. */
export type AlterarContatosSer = Partial<{
  residencial: string;
  whatsapp: string;
  contato: string;
}>;
