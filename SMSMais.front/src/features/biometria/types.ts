/**
 * Login do motorista no tablet do veículo pelo rosto (ADR-0073). O rosto é comparado no tablet; o
 * painel só vê a situação do cadastro, a foto de referência, as conferências e gera o código de
 * liberação da central. Enums chegam como string (JsonStringEnumConverter global no backend).
 */

/** Em que momento o tablet conferiu o motorista. */
export type MomentoVerificacaoFacial = 'Login' | 'InicioTranslado' | 'PrimeiroAcesso';

/** Como o motorista foi conferido: pelo rosto ou pelo código que a central gerou. */
export type MeioVerificacaoMotorista = 'Rosto' | 'CodigoCentral';

/** Situação do cadastro do rosto do motorista (`GET motoristas/{id}/biometria`). */
export type BiometriaMotorista = {
  cadastrada: boolean;
  cadastradaEm: string | null;
  /** Veículo do tablet onde o motorista fez o primeiro acesso. */
  veiculoPlaca: string | null;
  /** Versão do termo de ciência aceito no primeiro acesso. */
  termoVersao: string | null;
};

/** JPEG em base64 puro (sem o prefixo `data:`). */
export type FotoBiometria = { fotoBase64: string };

/** Código de uso único da central (6 números, 15 min) para um motorista entrar num veículo. */
export type LiberacaoGerada = {
  codigo: string;
  expiraEm: string;
  motoristaNome: string;
  veiculoPlaca: string;
};

/** Uma conferência registrada pelo tablet, aprovada ou não. */
export type VerificacaoFacial = {
  id: string;
  veiculoId: string;
  veiculoPlaca: string;
  /** Nulo quando o rosto não bateu com ninguém escalado. */
  motoristaId: string | null;
  motoristaNome: string | null;
  momento: MomentoVerificacaoFacial;
  meio: MeioVerificacaoMotorista;
  aprovada: boolean;
  /** Semelhança com a foto de referência (0 a 1). Nula na entrada pelo código da central. */
  nota: number | null;
  /** Nota mínima usada pelo tablet para aprovar. */
  limiar: number | null;
  provaDeVida: boolean;
  latitude: number | null;
  longitude: number | null;
  /** Relógio do tablet. */
  capturadaEm: string;
  recebidaEm: string;
  temSelfie: boolean;
  /** Quem gerou o código, quando a entrada foi pelo código da central. */
  liberadaPorNome: string | null;
};

/** Filtros de `GET rastreamento/verificacoes-faciais`. Sem `dia`, o servidor devolve os últimos 7 dias. */
export type FiltroVerificacoesFaciais = {
  veiculoId?: string;
  motoristaId?: string;
  /** `aaaa-mm-dd`, no fuso de Brasília. */
  dia?: string;
};
