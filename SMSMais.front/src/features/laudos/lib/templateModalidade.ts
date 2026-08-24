import type { LaudoTemplateListItem } from '@/features/laudo-templates/types';

/**
 * Escolha automática do template de checklist para um laudo novo.
 *
 * O casamento é por CATEGORIA do template (texto livre digitado no cadastro),
 * comparada com os termos esperados da modalidade DICOM do estudo. Não existe
 * coluna de modalidade no template — quando existir, esta função vira uma
 * consulta direta.
 *
 * Regra de ouro: na dúvida, NÃO carrega. Abrir o laudo vazio e deixar a médica
 * escolher no seletor é sempre melhor do que abrir o laudo de outro exame.
 */

/** Termos aceitos na categoria do template, por modalidade DICOM. */
const TERMOS_POR_MODALIDADE: Record<string, string[]> = {
  MG: ['mamografia', 'mama'],
  // Densitometria óssea. O código DICOM varia por fabricante; 'OT' (Other) é
  // genérico demais para casar automaticamente e fica de fora de propósito.
  BMD: ['densitometria', 'dmo'],
  DXA: ['densitometria', 'dmo'],
};

/** Minúsculas e sem acento, para o casamento não depender de como foi digitado. */
function normalizar(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim();
}

/**
 * Template de checklist da modalidade, ou null quando não há candidato único.
 * Sem modalidade no estudo, cai em mamografia — que é o fluxo histórico (o
 * laudo estruturado nasceu na mamografia e é a maioria absoluta do volume).
 */
export function templateDaModalidade(
  templates: LaudoTemplateListItem[],
  modalidade: string | undefined,
): LaudoTemplateListItem | null {
  const chave = (modalidade ?? 'MG').toUpperCase();
  const termos = TERMOS_POR_MODALIDADE[chave];
  if (!termos) return null;

  const candidatos = templates.filter(
    (t) => t.temChecklist && termos.some((termo) => normalizar(t.categoria).includes(termo)),
  );

  // Zero candidatos → texto livre. Mais de um → ambíguo, a médica decide.
  return candidatos.length === 1 ? candidatos[0] : null;
}
