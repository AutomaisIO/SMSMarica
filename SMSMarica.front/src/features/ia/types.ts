// Contrato da API do módulo de Inteligência (IA): configuração de provedor, bases de dados
// consultáveis, conhecimento (RAG) e governança/melhorias. (A antiga tela de "perguntar" foi
// substituída pelo menu "Consulta Inteligente" — feature consulta-inteligente.)

export type Ambiente = 'PRODUCAO' | 'TREINAMENTO';

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
  /** Slug curto e estável (multi-base): prefixa identifiers internos e a proveniência na importação. */
  slug?: string | null;
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
  /** Base alcançada por agente proxy (WSS reverso) — ADR-0023. */
  viaAgente: boolean;
  /** Se o agente proxy está conectado agora (só faz sentido com viaAgente). */
  agenteConectado: boolean;
  /** Se já existe token de agente gerado. */
  tokenDefinido: boolean;
};

/** Payload de criação/edição de base. Senha só vai quando o usuário a digita. */
export type SalvarFonteConfigPayload = {
  nome: string;
  slug?: string;
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
  /** Cria a base como proxy via agente (sem host/senha; credenciais no .env do destino). */
  viaAgente?: boolean;
};

/** Documento de conhecimento (.md) de uma base — repositório orientado ao banco. */
export type DocumentoConhecimento = {
  id: string;
  caminho: string;
  versao: number;
  tamanho: number;
  chunks: number;
  /** Veio de arquivo do repositório (só-leitura na tela). */
  doRepo: boolean;
  atualizadoEm: string;
};

export type DocumentoConhecimentoDetalhe = {
  id: string;
  caminho: string;
  conteudo: string;
  versao: number;
  doRepo: boolean;
  atualizadoEm: string;
};

export type ExtracaoModeloResultado = {
  totalTabelas: number;
  totalViews: number;
  documentadas: number;
  totalFks: number;
  documentosGerados: number;
  aviso?: string | null;
};

/** Resultado do backfill de embeddings (RAG) de uma base. */
export type EmbeddingsBackfillResultado = {
  totalChunks: number;
  jaTinham: number;
  gerados: number;
  restantes: number;
  aviso?: string | null;
};

/** Token de agente recém-gerado (mostrado uma vez). */
export type TokenAgenteGerado = {
  slug: string;
  token: string;
  wssUrl: string;
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
