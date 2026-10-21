/**
 * Fonte de dados (ADR-0077): de onde a plataforma lê dado. Vem de `GET /fontes-dados`.
 * `origem = 'base'` é cadastrável/removível (chave = id); `origem = 'sistema'` é fixa (SISREG, SER,
 * SERNIT, ESUS SG, e-SUS PEC, API-SISREG — chave = nome do sistema). Nunca traz segredo.
 */
export type CategoriaFonte = 'banco' | 'prontuario-web' | 'regulacao' | 'proprio';

export type FonteDados = {
  chave: string;
  origem: 'base' | 'sistema';
  nome: string;
  /** Nome do enum no servidor (ex.: `Salux`, `SqlServer`, `KlinikosWeb`, `SerWeb`, `SisregApi`). */
  tipo: string;
  categoria: CategoriaFonte;
  slug?: string | null;
  dialeto?: string | null;
  ambiente?: string | null;
  ativo: boolean;
  configurada: boolean;
  host?: string | null;
  porta?: number | null;
  servico?: string | null;
  /** Só onde sempre foi exibido (bases e API-SISREG). Login de operador é write-only. */
  usuario?: string | null;
  baseUrl?: string | null;
  urlPadrao?: string | null;
  loginDefinido: boolean;
  senhaDefinida: boolean;
  tokenDefinido: boolean;
  viaAgente: boolean;
  agenteConectado: boolean;
  tokenAgenteDefinido: boolean;
  familia?: string | null;
  /** Parâmetros de CONEXÃO não secretos. O uso fica no recurso que usa a fonte. */
  parametros: Record<string, string | null>;
  testavel: boolean;
  motivoSemTeste?: string | null;
  /** Trocar o login exige usuário e senha juntos (o sistema testa antes de gravar). */
  loginJunto: boolean;
  usadaPor: string[];
};

export type CriarFonteDadosPayload = {
  nome: string;
  slug?: string;
  tipo: string;
  dialeto?: string;
  ambiente: string;
  host?: string;
  porta?: number;
  servico?: string;
  usuario?: string;
  senha?: string;
  baseUrl?: string;
  familia?: string;
  parametros?: Record<string, string | null>;
};

/** Nas bases, campo ausente mantém e texto vazio limpa. Segredo vazio mantém. */
export type AtualizarFonteDadosPayload = {
  nome?: string;
  slug?: string;
  ambiente?: string;
  host?: string;
  porta?: number;
  servico?: string;
  usuario?: string;
  senha?: string;
  token?: string;
  baseUrl?: string;
  familia?: string;
  ativo: boolean;
  parametros?: Record<string, string | null>;
};

export type TestarFonteDadosPayload = { usuario?: string; senha?: string; cliente?: string };

export type ResultadoTesteFonte = { sucesso: boolean; mensagem?: string | null };

/** Token do agente recém-gerado — mostrado UMA vez. */
export type TokenAgente = { slug: string; token: string; wssUrl: string };

// Endereço do SISREG, mostrado na fonte "SISREG III" (GET /integracoes/sisreg/endereco — a rota
// manteve o prefixo antigo). O SISREG troca de IP sem aviso e a produção só o alcança pelo túnel —
// ver docs/sisreg-egress.md. Instantes em UTC (ISO com 'Z').
export type SituacaoEnderecoSisreg =
  | 'NaoVerificado'
  | 'NoTunel'
  | 'SaidaDireta'
  | 'ForaDoTunel'
  | 'SemDns'
  | 'RotaDesconhecida';

export type EnderecoSisregIp = {
  ip: string;
  interfaceRota: string | null;
  noTunel: boolean | null;
  desdeEm: string | null;
};

export type EnderecoSisregPeriodo = {
  ip: string;
  desdeEm: string;
  ultimaVezVistoEm: string;
  ateEm: string | null;
  interfaceRota: string | null;
};

export type EnderecoSisreg = {
  host: string;
  situacao: SituacaoEnderecoSisreg;
  verificadoEm: string | null;
  tunelEsperado: string | null;
  erro: string | null;
  atuais: EnderecoSisregIp[];
  historico: EnderecoSisregPeriodo[];
};
