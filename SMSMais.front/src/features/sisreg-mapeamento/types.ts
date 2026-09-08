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
  profissionais: SisregProfissional[];
};

/** Resultado do "habilitar tudo" da unidade — a tela usa para dizer o custo resultante. */
export type AlternarTudoDaUnidade = {
  profissionaisAfetados: number;
  procedimentosAfetados: number;
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
  /** Motor do passado ligado para esta unidade. */
  historicoAtivo: boolean;
  /** Até onde para trás a importação está coberta (ISO date). Null = só o que a diária trouxe.
   * É isto que responde "o histórico já foi feito?" — um contador de execuções não saberia dizer
   * o que ficou faltando. */
  historicoCobertoDe: string | null;
  /** Preenchido quando o motor chegou ao início real da unidade (fatias seguidas vazias). */
  historicoConcluidoEm: string | null;
  /** Pede a exportação sem escolher profissional nem procedimento: o SISREG devolve a agenda da
   * unidade inteira numa requisição só. Ligado, `requisicoesEstimadas` vem 1 (o back calcula) e o
   * mapeamento deixa de recortar o que é consultado. */
};

export type SalvarVarreduraAgendaPayload = {
  ativo: boolean;
  /** "HH:mm" — hora de Brasília. */
  horaLocal: string;
  diasAFrente: number;
  /** Omitido mantém o valor atual — dá para salvar só a agenda sem mexer no gatilho. */
  enviarConfirmacao?: boolean;
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
  /** Última prova de vida do processo. Null nas execuções anteriores ao batimento. */
  ultimoSinalEm: string | null;
  /**
   * Diz "Rodando" no banco mas não responde há minutos — quase sempre o serviço reiniciou
   * (deploy) por baixo dela. A tela mostra isso, e não "Rodando": foi anunciar como viva uma
   * execução já morta que custou 65 minutos de dúvida em 08/09/2026.
   */
  semSinal: boolean;
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
  /** Início da corrida — a tela mostra "há X" ao lado do contador. */
  iniciadoEm: string;
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

/** Liga/desliga a importação do passado de uma unidade. */
export type AlternarHistoricoPayload = {
  ativo: boolean;
  /** Recomeça do zero: zera cobertura e contagem de fatias vazias. */
  reiniciar?: boolean;
};
