/**
 * Acervo do paciente — o que fica perene no cadastro ("Exames anexados"): documentos do acervo,
 * PDFs da anamnese, laudos ASSINADOS e o PDF das imagens dos exames, numa lista só.
 */
export type TipoItemAcervo = 'Documento' | 'AnexoExame' | 'Laudo' | 'ImagensExame';

export type SituacaoDocumentoPaciente = 'Pendente' | 'Aceito';

export type ItemAcervo = {
  /** `tipo:id` — o que se devolve ao backend para abrir ou anexar o item. */
  chave: string;
  tipo: TipoItemAcervo;
  id: string;
  titulo: string;
  descricao: string | null;
  mimeType: string;
  tamanhoBytes: number | null;
  paginas: number | null;
  data: string;
  /** Rótulo de onde veio ("Solicitação", "WhatsApp", "Enviado pelo paciente", "Laudo"…). */
  origem: string;
  situacao: SituacaoDocumentoPaciente | null;
  /** Só documentos do acervo propriamente dito têm título/descrição editáveis. */
  editavel: boolean;
};

export const ROTULO_TIPO_ACERVO: Record<TipoItemAcervo, string> = {
  Documento: 'Documento',
  AnexoExame: 'Anexo da anamnese',
  Laudo: 'Laudo assinado',
  ImagensExame: 'Imagens do exame',
};

/** Tipos que o acervo aceita no upload (o servidor confere de novo). */
export const TIPOS_ACEITOS_ACERVO = ['application/pdf', 'image/jpeg', 'image/png', 'image/webp', 'image/gif'];

export const LIMITE_MB_ACERVO = 25;
