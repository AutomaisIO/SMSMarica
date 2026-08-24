/**
 * Origem de um registro clínico do hub FHIR: **qual sistema** o gerou e **em qual unidade**
 * o atendimento aconteceu.
 *
 * As duas coisas são independentes e nenhuma deriva da outra. A base `salux-hcml` atende
 * três unidades (HMCML, UPA Inoã e Santa Rita), então ler a unidade de dentro do
 * `meta.source` erra em centenas de milhares de atendimentos. E, por causa do cutover
 * Salux→Klinikos de abr/2025, a mesma unidade tem atendimentos dos dois sistemas — a UPA
 * Inoã, por exemplo, tem os antigos no Salux e os novos no Klinikos.
 */

/** Sistema de origem, extraído do `meta.source` (`https://…/source/<sistema>[/<base>]`). */
const SISTEMAS: Record<string, string> = {
  salux: 'Salux',
  klinikos: 'Klinikos',
  esus: 'e-SUS APS',
  pacs: 'PACS',
  smsmarica: 'SMS Maricá',
  'ser-sesrj': 'SER (SES-RJ)',
};

/**
 * Nome curto por CNES. O nome oficial não cabe numa etiqueta ("PRONTO ATENDIMENTO 24H DO
 * POSTO DE SAÚDE SANTA RITA"), e o CNES é a chave estável — não muda quando o cadastro é
 * reescrito por um import.
 */
const UNIDADES_POR_CNES: Record<string, string> = {
  '2266733': 'HMCML',
  '7164440': 'UPA Inoã',
  '2266792': 'Santa Rita',
};

/** "salux" a partir de "https://smsmarica.saude.marica/source/salux/salux-hcml". */
function chaveSistema(fonte: string): string {
  const depois = fonte.split('/source/')[1];
  return (depois ?? '').split('/')[0].toLowerCase();
}

/** Nome do sistema de origem ("Salux"), ou `null` se a fonte for desconhecida/ausente. */
export function nomeSistemaOrigem(fonte?: string | null): string | null {
  if (!fonte) return null;
  const chave = chaveSistema(fonte);
  return SISTEMAS[chave] ?? (chave ? chave.toUpperCase() : null);
}

/**
 * Base de origem ("salux-hcml", "upa24h-marica-sqlserver") quando o `meta.source` a carrega.
 * Só o Klinikos tem uma base por unidade; no Salux uma base serve as três, então a base
 * **não** substitui a unidade — serve para saber de qual instalação o registro veio.
 */
export function nomeBaseOrigem(fonte?: string | null): string | null {
  if (!fonte) return null;
  const depois = fonte.split('/source/')[1];
  return depois?.split('/')[1] ?? null;
}

/** Nome curto da unidade — pelo CNES quando conhecido, senão o nome que veio do hub. */
export function nomeUnidade(cnes?: string | null, nome?: string | null): string | null {
  if (cnes && UNIDADES_POR_CNES[cnes]) return UNIDADES_POR_CNES[cnes];
  return nome?.trim() || null;
}

/**
 * Etiqueta completa: "Salux - HMCML". Cai para só o sistema quando o atendimento não tem
 * `serviceProvider` (registros antigos do hub), e devolve `null` quando não há nem fonte —
 * aí não há nada honesto a exibir.
 */
export function rotuloOrigem(
  fonte?: string | null,
  unidadeCnes?: string | null,
  unidadeNome?: string | null,
): string | null {
  const sistema = nomeSistemaOrigem(fonte);
  const unidade = nomeUnidade(unidadeCnes, unidadeNome);
  if (sistema && unidade) return `${sistema} - ${unidade}`;
  return sistema ?? unidade;
}
