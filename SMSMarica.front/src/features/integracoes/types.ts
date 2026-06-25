// Credenciais de provedores de login OAuth (store genérico cifrado).
export type IntegracaoCredencial = {
  provedor: string;
  rotulo: string;
  clientIdDefinido: boolean;
  clientSecretDefinido: boolean;
  redirectUri: string | null;
  parametrosJson: string | null;
  ativo: boolean;
};

export type AtualizarCredencialPayload = {
  clientId?: string;
  clientSecret?: string;
  redirectUri?: string | null;
  parametrosJson?: string | null;
  ativo: boolean;
};

// Resultado do teste de conexão do DigitalOcean Spaces (S3).
// Sempre vem em HTTP 200 — o sucesso/falha do teste está em `ok`.
export type EtapaTesteSpaces = 'credencial' | 'conexao' | 'escrita' | 'leitura' | 'exclusao' | 'ok';

export type TesteSpacesResultado = {
  ok: boolean;
  etapa: EtapaTesteSpaces;
  mensagem: string;
  bucket: string | null;
  endpoint: string | null;
};

// Google Maps (TFD) — chave cifrada.
export type TfdGoogle = { baseUrl: string; chaveConfigurada: boolean; ativo: boolean };
export type AtualizarTfdGoogle = { baseUrl: string; apiKey?: string; ativo: boolean };

// WhatsApp / Meta (TFD) — tokens cifrados.
export type TfdWhatsApp = {
  baseUrl: string;
  phoneNumberId: string | null;
  wabaId: string | null;
  tokenConfigurado: boolean;
  verifyTokenConfigurado: boolean;
  appSecretConfigurado: boolean;
  ativo: boolean;
};

export type AtualizarTfdWhatsApp = {
  baseUrl: string;
  token?: string;
  phoneNumberId?: string | null;
  wabaId?: string | null;
  verifyToken?: string;
  appSecret?: string;
  ativo: boolean;
};
