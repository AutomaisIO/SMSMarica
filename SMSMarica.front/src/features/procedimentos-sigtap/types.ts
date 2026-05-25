export type ProcedimentoSigtap = {
  id: string;
  codigo: string;
  nome: string;
  grupo: string;
  subgrupo: string;
  forma: string;
  descricao: string;
  ativo: boolean;
  competenciaInicio: string;
  competenciaFim: string | null;
};
