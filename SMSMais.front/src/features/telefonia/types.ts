/** Softphone de um usuário como o painel vê (sem segredo). */
export type SoftphoneUsuario = {
  habilitado: boolean;
  ramal: string | null;
  nomeExibicao: string | null;
  ativo: boolean;
};

export type DefinirSoftphonePayload = {
  ramal: string;
  nomeExibicao: string;
  ativo: boolean;
};

export type RamaisLivres = {
  inicio: number | null;
  fim: number | null;
  sugestoes: string[];
};

export type IceServer = { urls: string; username?: string | null; credential?: string | null };

/** Credencial SIP do próprio usuário. Contém a senha: nunca guardar em storage. */
export type CredencialSoftphone = {
  ramal: string;
  usuarioSip: string;
  senha: string;
  dominio: string;
  uri: string;
  wssUrl: string;
  nomeExibicao: string | null;
  iceServers: IceServer[];
};
