// Cliente HTTP tipado pro backend FastAPI.

export type Paciente = {
  cd_paciente: string;
  nm_paciente: string;
  dt_nascimento?: string | null;
  sexo?: string | null;
  cpf_paciente?: string | null;
  cns?: string | null;
  in_ativo?: string | null;
  dt_obito?: string | null;
};

export type CadastroPaciente = Paciente & {
  sc_apelido?: string | null;
  rg_paciente?: string | null;
  estado_civil?: string | null;
  nm_mae?: string | null;
  nm_pai?: string | null;
  nm_conjuge?: string | null;
  nm_logradouro?: string | null;
  nr_logradouro?: string | null;
  compl_logradouro?: string | null;
  bairro?: string | null;
  cep?: string | null;
  cd_uf?: string | null;
  cd_cidade?: string | null;
  nm_cidade?: string | null;
  nr_ddd_fone?: string | null;
  nr_fone?: string | null;
  nr_fone_compl?: string | null;
  email?: string | null;
  profissao?: string | null;
  religiao?: string | null;
  peso?: string | null;
  altura?: string | null;
  dt_cadastro?: string | null;
  cd_pront_sgh?: string | null;
  cd_pront_cem?: string | null;
};

export type Atendimento = {
  dt_entrada: string;
  dt_ano: string;
  nr_doc: string;
  nm_medico?: string | null;
  dt_alta?: string | null;
  ds_clinica?: string | null;
  cd_cid?: string | null;
  id_tipo: "F" | "B";
  cd_hospital: string;
};

export type FiaHeader = {
  cd_hospital: string;
  dt_ano_fia: string;
  nr_fia: string;
  cd_paciente_efetivo: string;
  nm_paciente?: string | null;
  dt_baixa?: string | null;
  dt_alta?: string | null;
  dt_previsao_alta?: string | null;
  nm_medico_entrada?: string | null;
  nm_medico_alta?: string | null;
  cd_cid?: string | null;
  id_internacao?: string | null;
  nm_responsavel?: string | null;
  nr_dias_internacao?: string | null;
  leito_atual?: string | null;
};

export type BaaHeader = {
  cd_hospital: string;
  dt_ano_baa: string;
  nr_baa: string;
  cd_paciente_efetivo: string;
  nm_paciente?: string | null;
  dt_atendimento?: string | null;
  dt_chegada?: string | null;
  dt_saida?: string | null;
  nm_medico?: string | null;
  cd_cid?: string | null;
  ds_especialidade?: string | null;
  in_emergencia?: string | null;
  id_destino?: string | null;
  ds_classif_risco?: string | null;
  in_baa_atendido?: string | null;
  ds_setor?: string | null;
  nro_senha?: string | null;
};

export type EdocResumo = {
  id_movimento: string;
  cd_modelo: string;
  ds_modelo: string;
  grupo?: string | null;
  categoria?: string | null;
  dt_inclusao: string;
  nm_funcionario?: string | null;
  in_status?: string | null;
};

export type EdocItem = {
  cd_item: string;
  ds_item?: string | null;
  tipo?: string | null;
  seq_docto?: string | null;
  ds_resposta?: string | null;
};

async function jget<T>(url: string): Promise<T> {
  const r = await fetch(url);
  if (!r.ok) {
    const txt = await r.text();
    throw new Error(`${r.status}: ${txt}`);
  }
  return r.json();
}

export const api = {
  buscarPaciente: (nome: string, cpf = "") =>
    jget<{ resultados: Paciente[] }>(
      `/api/pacientes/buscar?nome=${encodeURIComponent(nome)}&cpf=${encodeURIComponent(cpf)}`,
    ),

  cadastro: (cd: number) => jget<CadastroPaciente>(`/api/pacientes/${cd}`),

  alergias: (cd: number) =>
    jget<{ alergias: Array<{ ds_alergia: string; ds_material?: string | null; ds_descritivo?: string | null }> }>(
      `/api/pacientes/${cd}/alergias`,
    ),

  historico: (cd: number) =>
    jget<{ atendimentos: Atendimento[] }>(`/api/pacientes/${cd}/historico`),

  fia: (nr: number, ano: number) => jget<FiaHeader>(`/api/fia/${nr}/${ano}`),
  baa: (nr: number, ano: number) => jget<BaaHeader>(`/api/baa/${nr}/${ano}`),

  edocsFia: (nr: number, ano: number) =>
    jget<{ edocs: EdocResumo[] }>(`/api/fia/${nr}/${ano}/edocs`),
  edocsBaa: (nr: number, ano: number) =>
    jget<{ edocs: EdocResumo[] }>(`/api/baa/${nr}/${ano}/edocs`),

  sinaisVitais: (nr: number, ano: number) =>
    jget<{
      aferições: Array<{
        dthr_visita: string; peso?: string | null; altura?: string | null;
        temp?: string | null; pa_sist?: string | null; pa_diast?: string | null;
        fc?: string | null; fr?: string | null; spo2?: string | null; glicemia?: string | null;
      }>;
    }>(`/api/fia/${nr}/${ano}/sinais-vitais`),

  edocMovimento: (cd_hospital: number, ano: number, id: number) =>
    jget<{ itens: EdocItem[] }>(`/api/edocs/movimento/${cd_hospital}/${ano}/${id}`),
};
