import type { CategoriaFonte } from '@/features/fontes-dados/types';

/** Rótulo de cada tipo de fonte (o servidor manda o nome do enum). */
export const ROTULO_TIPO: Record<string, string> = {
  Salux: 'Salux (Oracle)',
  Mv: 'MV',
  Eco: 'ECO',
  Fhir: 'FHIR (API REST)',
  Postgres: 'PostgreSQL',
  SqlServer: 'SQL Server (via agente)',
  Regulacao: 'Regulação — banco do próprio sistema',
  Atendimento: 'Atendimento — banco do próprio sistema',
  KlinikosWeb: 'Klinikos Web',
  SisregWeb: 'SISREG III (login do sistema)',
  SisregApi: 'API do SISREG',
  SerWeb: 'SER (login do sistema)',
  SernitWeb: 'SERNIT (login do sistema)',
  EsusSgWeb: 'ESUS São Gonçalo (login do sistema)',
  EsusPecWeb: 'e-SUS APS PEC (login do sistema)',
};

export const rotuloTipo = (tipo: string) => ROTULO_TIPO[tipo] ?? tipo;

/** Ordem e título das seções da tela. */
export const CATEGORIAS: ReadonlyArray<{ id: CategoriaFonte; titulo: string; descricao: string }> = [
  {
    id: 'banco',
    titulo: 'Bancos e agentes',
    descricao: 'Bancos de prontuário lidos direto (Oracle, PostgreSQL) ou por um agente instalado no servidor da unidade.',
  },
  {
    id: 'prontuario-web',
    titulo: 'Prontuários pela web',
    descricao: 'Sistemas de prontuário lidos "como usuário", pelo login e pelos relatórios da tela.',
  },
  {
    id: 'regulacao',
    titulo: 'Regulação e cadastro',
    descricao: 'Sistemas externos lidos com o login de um operador: SISREG, SER, SERNIT, ESUS São Gonçalo e e-SUS PEC.',
  },
  {
    id: 'proprio',
    titulo: 'Banco do próprio sistema',
    descricao: 'Recortes só-leitura do banco desta plataforma, para a Consulta Inteligente.',
  },
];

/** Tipos que se cadastram pela tela. Base SQL Server nasce pelo agente. */
export const TIPOS_CADASTRAVEIS = ['Salux', 'Mv', 'Eco', 'Fhir', 'KlinikosWeb', 'Postgres', 'Regulacao', 'Atendimento'];

/** Versões do Klinikos com perfil no catálogo do servidor (KlinikosBuildCatalogo). Build novo → o
 *  motor pede para criar o perfil; aí entra aqui e no catálogo. */
export const KLINIKOS_BUILDS: ReadonlyArray<{ versao: string; rotulo: string }> = [
  { versao: 'K.2024.09.2.1', rotulo: 'Conde 2024 (K.2024.09.2.1)' },
  { versao: 'U.2025.06.1.26', rotulo: 'UPA/Santa Rita 2025 (U.2025.06.1.26)' },
];
