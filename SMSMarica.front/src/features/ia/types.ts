// Contrato da API do módulo de Inteligência (IA).
// Tela de perguntar + configuração de provedor e bases de dados consultáveis.

export type Ambiente = 'PRODUCAO' | 'TREINAMENTO';

/** Forma de renderização sugerida pelo backend para uma resposta. */
export type VisualizacaoIa =
  | 'numero'
  | 'lista'
  | 'tabela'
  | 'grafico_pizza'
  | 'grafico_barra'
  | 'grafico_linha';

export type StatusResposta = 'ok' | 'vazio' | 'erro';

/** Resposta de UMA fonte para a pergunta feita. */
export type RespostaIa = {
  fonteId: string;
  fonteNome: string;
  status: StatusResposta;
  resumo?: string;
  visualizacao?: VisualizacaoIa;
  titulo?: string;
  colunas: string[];
  dados: unknown[][];
  sql?: string;
  consultaId: string;
  erro?: string;
};

/** POST /ia/perguntar */
export type PerguntarPayload = {
  pergunta: string;
  fonteIds: string[];
};

export type PerguntarResposta = {
  respostas: RespostaIa[];
};

/** GET /ia/fontes — fontes disponíveis para consulta (ativas). */
export type FonteIa = {
  id: string;
  nome: string;
  tipo: string;
  ambiente: Ambiente;
};

// ── Configuração ────────────────────────────────────────────────────────────

/** GET/PUT /ia/configuracao */
export type ConfiguracaoIa = {
  provedor: string;
  modelo: string;
  provedorEmbeddings: string;
  modeloEmbeddings: string;
  /** Write-only: indica apenas se o token já está definido (nunca devolve o valor). */
  tokenDefinido: boolean;
  tokenEmbeddingsDefinido: boolean;
};

/** Payload do PUT — tokens só são enviados quando o usuário digita um novo. */
export type AtualizarConfiguracaoPayload = {
  provedor: string;
  modelo: string;
  provedorEmbeddings: string;
  modeloEmbeddings: string;
  /** Novo token de IA; omitido/undefined mantém o atual. */
  token?: string;
  /** Novo token de embeddings; omitido/undefined mantém o atual. */
  tokenEmbeddings?: string;
};

/** Item de base de dados consultável (CRUD em /ia/configuracao/fontes). */
export type FonteConfig = {
  id: string;
  nome: string;
  tipo: string;
  dialeto: string;
  ambiente: Ambiente;
  host?: string;
  porta?: number;
  servico?: string;
  usuario?: string;
  baseUrl?: string;
  /** Write-only: a senha nunca volta da API. */
  senhaDefinida: boolean;
  ativo: boolean;
};

/** Payload de criação/edição de base. Senha só vai quando o usuário a digita. */
export type SalvarFonteConfigPayload = {
  nome: string;
  tipo: string;
  dialeto: string;
  ambiente: Ambiente;
  host?: string;
  porta?: number;
  servico?: string;
  usuario?: string;
  baseUrl?: string;
  /** Nova senha; omitida mantém a atual. */
  senha?: string;
  ativo: boolean;
};

/** Resultado de POST /ia/configuracao/fontes/{id}/testar-conexao */
export type ResultadoTesteConexao = {
  sucesso: boolean;
  mensagem: string;
};

// ── Governança / Melhorias (aprendizado) ─────────────────────────────────────

/** GET /ia/aprendizados — instrução aprendida (manual ou auto-correção) de uma fonte. */
export type AprendizadoIa = {
  id: string;
  fonteId: string;
  fonteNome: string;
  tipo: string;
  origem: string;
  conteudo: string;
  ativo: boolean;
  criadoEm: string;
};

/** GET /ia/correcoes — entrada do histórico de correções automáticas de SQL. */
export type CorrecaoIa = {
  id: string;
  consultaId: string;
  aprendizadoId?: string | null;
  pergunta: string;
  erroOriginal: string;
  sqlAntes?: string | null;
  sqlDepois?: string | null;
  instrucaoGerada?: string | null;
  criadoEm: string;
  revisadoEm?: string | null;
  removidoEm?: string | null;
};
