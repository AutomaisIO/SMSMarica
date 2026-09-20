import { BookOpen, Folder, MessageCircle, Settings2, Stethoscope } from 'lucide-react';
import { artigoConfirmacoes } from '@/features/manual/conteudo/confirmacoes';
import { artigoOuvidoria } from '@/features/manual/conteudo/ouvidoria';
import type { Artigo, GrupoManual } from '@/features/manual/tipos';

/**
 * Catálogo do manual.
 *
 * O manual vive no código, e não num banco ou num Word, por uma razão só: assim ele muda no mesmo
 * commit que muda a tela. Documentação que mora longe do código descola — e manual descolado é
 * pior que manual nenhum, porque ensina errado com ar de autoridade.
 *
 * Para acrescentar um artigo: crie `conteudo/<slug>.tsx` exportando um `Artigo` e registre aqui.
 */

/** Grupos do índice. Espelham as seções do menu, que é como a pessoa pensa o sistema. */
export const GRUPOS: GrupoManual[] = [
  {
    id: 'atendimento',
    titulo: 'Atendimento',
    descricao: 'Falar com o cidadão: confirmações, conversas, mensagens e robô.',
    icone: MessageCircle,
  },
  {
    id: 'cadastros',
    titulo: 'Cadastros',
    descricao: 'Pacientes, profissionais, unidades e o que sustenta o resto.',
    icone: Folder,
  },
  {
    id: 'assistencial',
    titulo: 'Assistencial',
    descricao: 'Exames, laudos, imagem e o cuidado propriamente dito.',
    icone: Stethoscope,
  },
  {
    id: 'sistema',
    titulo: 'Sistema',
    descricao: 'Perfis, permissões, identidade da instituição e configuração.',
    icone: Settings2,
  },
];

export const ARTIGOS: Artigo[] = [artigoConfirmacoes, artigoOuvidoria];

/** Ícone de fallback quando um grupo ainda não foi declarado. */
export const ICONE_MANUAL = BookOpen;

export function artigoPorSlug(slug: string | undefined): Artigo | undefined {
  return ARTIGOS.find((a) => a.slug === slug);
}

/**
 * Artigo que documenta uma rota. Casa pelo prefixo mais longo, para que
 * `/app/confirmacoes/qualquer-coisa` continue achando o artigo de Confirmações.
 */
export function artigoDaRota(rota: string): Artigo | undefined {
  let melhor: Artigo | undefined;
  for (const a of ARTIGOS) {
    if (!a.rota) continue;
    if (rota === a.rota || rota.startsWith(`${a.rota}/`)) {
      if (!melhor || a.rota.length > (melhor.rota?.length ?? 0)) melhor = a;
    }
  }
  return melhor;
}

export function grupoPorId(id: string): GrupoManual | undefined {
  return GRUPOS.find((g) => g.id === id);
}

/** Artigos de um grupo, em ordem alfabética — o índice não tem hierarquia própria. */
export function artigosDoGrupo(grupoId: string): Artigo[] {
  return ARTIGOS.filter((a) => a.grupo === grupoId).sort((a, b) => a.titulo.localeCompare(b.titulo, 'pt-BR'));
}

/** Os mais novos primeiro — alimenta "atualizados recentemente" no índice. */
export function artigosRecentes(limite = 4): Artigo[] {
  return [...ARTIGOS].sort((a, b) => b.atualizadoEm.localeCompare(a.atualizadoEm)).slice(0, limite);
}
