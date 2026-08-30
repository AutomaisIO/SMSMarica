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
  /** SIGTAP confirmado no de-para (só dígitos). Null = pendente. */
  // O SIGTAP não vem aqui: este código é só o FILTRO da varredura. O procedimento de verdade é
  // resolvido na importação, a partir do que cada agendamento informa.
  /** Id no catálogo global de procedimentos do SISREG. */
  deParaId: string | null;
  /** Importar este procedimento NESTA unidade avisa o paciente por WhatsApp. Decisão da unidade. */
  enviarConfirmacao: boolean;
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

/** Resultado do "habilitar tudo" da unidade — a tela usa para dizer o custo resultante. */
export type AlternarTudoDaUnidade = {
  profissionaisAfetados: number;
  procedimentosAfetados: number;
  /** Pares habilitados = requisições por varredura, quando NÃO há recorte de unidade inteira. */
  combinacoesHabilitadas: number;
  /** A unidade puxa a agenda inteira numa requisição? Muda a leitura do custo acima. */
  recorteUnidadeInteira: boolean;
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

/** Um procedimento do SISREG e o estado do seu de-para para o SIGTAP. Catálogo global. */
export type ProcedimentoSigtapDePara = {
  id: string;
  /** O `pa` do SISREG (7 dígitos). */
  codigo: string;
  nome: string;
  grupo: boolean;
  codigoSigtap: string | null;
  confirmado: boolean;
  sugeridoSigtapId: string | null;
  sugeridoCodigo: string | null;
  sugeridoNome: string | null;
  sugeridoScore: number | null;
  confirmadoEm: string | null;
};

/** Agenda do motor diário da unidade + custo estimado da próxima varredura. */
export type VarreduraAgenda = {
  unidadeId: string;
  unidadeNome: string;
  ativo: boolean;
  /** "HH:mm:ss" em hora de Brasília. */
  horaLocal: string;
  diasAFrente: number;
  proximoRunEm: string | null;
  pausadoAte: string | null;
  ultimaExecucaoEm: string | null;
  falhasConsecutivas: number;
  /** Pares habilitados — o que será varrido. O SIGTAP é resolvido depois, na importação. */
  combinacoesProntas: number;
  requisicoesEstimadas: number;
  tetoPorExecucao: number;
  /** Faixa (Brasília) em que o SISREG bloqueia a exportação: a varredura não roda entre
   * `corteEntradaLocal` e `bloqueioFimLocal`. Fora disso, qualquer hora. "HH:mm:ss". */
  bloqueioInicioLocal: string;
  bloqueioFimLocal: string;
  /** Hora a partir da qual já não se pode iniciar (bloqueio − margem de 30 min). "HH:mm:ss". */
  corteEntradaLocal: string;
  /** Gatilho mestre da unidade: importar solicitação avisa o paciente por WhatsApp? Vale para
   * toda importação — varredura e upload de arquivo. */
  enviarConfirmacao: boolean;
  /** Pede a exportação sem escolher profissional nem procedimento: o SISREG devolve a agenda da
   * unidade inteira numa requisição só. Ligado, `requisicoesEstimadas` vem 1 (o back calcula) e o
   * mapeamento deixa de recortar o que é consultado. */
  recorteUnidadeInteira: boolean;
};

export type SalvarVarreduraAgendaPayload = {
  ativo: boolean;
  /** "HH:mm" — hora de Brasília. */
  horaLocal: string;
  diasAFrente: number;
  /** Omitido mantém o valor atual — dá para salvar só a agenda sem mexer no gatilho. */
  enviarConfirmacao?: boolean;
  /** Omitido mantém o valor atual, igual ao gatilho de WhatsApp. */
  recorteUnidadeInteira?: boolean;
};

export type StatusVarredura =
  | 'Pendente'
  | 'EmExecucao'
  | 'Concluida'
  /** Cobertura incompleta declarada: parou no CAPTCHA ou no teto, preservando o que entrou. */
  | 'Parcial'
  | 'Erro'
  | 'Cancelada';

export type VarreduraExecucao = {
  id: string;
  unidadeId: string;
  unidadeNome: string;
  disparo: 'Manual' | 'Agendado';
  status: StatusVarredura;
  janelaInicio: string;
  janelaFim: string;
  combinacoesTotal: number;
  combinacoesFeitas: number;
  requisicoes: number;
  registrosEncontrados: number;
  validos: number;
  invalidos: number;
  jaExistiam: number;
  mensagemErro: string | null;
  iniciadoEm: string;
  finalizadoEm: string | null;
  duracaoSegundos: number | null;
  criadoPorNome: string | null;
};

/** Detalhe por profissional × procedimento de UMA execução (modal do histórico). */
export type VarreduraExecucaoItem = {
  id: string;
  profissionalNome: string;
  procedimentoCodigo: string;
  procedimentoNome: string;
  requisicoes: number;
  registrosEncontrados: number;
  validos: number;
  invalidos: number;
  jaExistiam: number;
  observacao: string | null;
};

export type StatusVarreduraVivo = {
  execucaoId: string;
  unidadeId: string;
  unidadeNome: string;
  emExecucao: boolean;
  combinacoesTotal: number;
  combinacoesFeitas: number;
  requisicoes: number;
  registrosEncontrados: number;
  validos: number;
  invalidos: number;
  profissionalAtual: string | null;
  procedimentoAtual: string | null;
};

export type VarreduraAceita = { execucaoId: string; mensagem: string };

/** Import PONTUAL de um procedimento (botão "Importar" da árvore de mapeamento). */
export type ImportarAgendaPontualPayload = {
  cpf: string;
  codigoProcedimento: string;
  /** "yyyy-MM-dd" — Brasília. */
  dataInicio: string;
  dataFim: string;
};

/** Resumo do que a importação pontual fez — vira o modal de resultado. */
export type ImportacaoAgendaPontualResultado = {
  inicio: string;
  fim: string;
  /** Requisições gastas no SISREG (o recurso escasso). */
  requisicoes: number;
  totalEncontrados: number;
  importados: number;
  jaExistiam: number;
  /** Sem SIGTAP mapeado — vira pendência; a varredura noturna do expo completa depois. */
  pendencias: number;
  mensagem: string;
};
