import { BookOpen, Bus, ClipboardCheck, Folder, LifeBuoy, MessageCircle, Settings2, Stethoscope } from 'lucide-react';
import { artigoAgenteIa } from '@/features/manual/conteudo/agenteIa';
import { artigoAnaliseRegrasEspelho } from '@/features/manual/conteudo/analiseRegrasEspelho';
import { artigoAnamnese } from '@/features/manual/conteudo/anamnese';
import { artigoAssinaturaLaudo } from '@/features/manual/conteudo/assinaturaLaudo';
import { artigoAtendimentosTransporte } from '@/features/manual/conteudo/atendimentosTransporte';
import { artigoAvisosCelular } from '@/features/manual/conteudo/avisosCelular';
import { artigoAvisosConexao } from '@/features/manual/conteudo/avisosConexao';
import { artigoConfirmacoes } from '@/features/manual/conteudo/confirmacoes';
import { artigoEsusSaoGoncalo } from '@/features/manual/conteudo/esusSaoGoncalo';
import { artigoExtensaoChrome } from '@/features/manual/conteudo/extensaoChrome';
import { artigoExtensaoChromeGerenciar } from '@/features/manual/conteudo/extensaoChromeGerenciar';
import { artigoFluxoAtendimentoWhatsApp } from '@/features/manual/conteudo/fluxoAtendimentoWhatsApp';
import { artigoIndicadoresRegulacao } from '@/features/manual/conteudo/indicadoresRegulacao';
import { artigoMensageria } from '@/features/manual/conteudo/mensageria';
import { artigoMotoristas } from '@/features/manual/conteudo/motoristas';
import { artigoOuvidoria } from '@/features/manual/conteudo/ouvidoria';
import { artigoPacientes } from '@/features/manual/conteudo/pacientes';
import { artigoRascunhosSerSernit } from '@/features/manual/conteudo/rascunhosSerSernit';
import { artigoRegulacaoGestaoFila } from '@/features/manual/conteudo/regulacaoGestaoFila';
import { artigoRegulacaoSolicitacoes } from '@/features/manual/conteudo/regulacaoSolicitacoes';
import { artigoSerMedicos } from '@/features/manual/conteudo/serMedicos';
import { artigoSernitMedicos } from '@/features/manual/conteudo/sernitMedicos';
import { artigoSisregMedicos } from '@/features/manual/conteudo/sisregMedicos';
import { artigoSolicitacoes } from '@/features/manual/conteudo/solicitacoes';
import { artigoTickets } from '@/features/manual/conteudo/tickets';
import { artigoTicketsGestao } from '@/features/manual/conteudo/ticketsGestao';
import { artigoTiposTratamento } from '@/features/manual/conteudo/tiposTratamento';
import { artigoUnidadesAtendimento } from '@/features/manual/conteudo/unidadesAtendimento';
import { artigoVeiculos } from '@/features/manual/conteudo/veiculos';
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
    id: 'regulacao',
    titulo: 'Regulação',
    descricao:
      'Solicitações, as filas espelhadas dos sistemas de regulação (SER, SERNIT, ESUS São Gonçalo), as regras de elegibilidade e os indicadores mensais de cada sistema.',
    icone: ClipboardCheck,
  },
  {
    id: 'transporte',
    titulo: 'Transporte de Pacientes',
    descricao: 'Atendimentos, destinos, tipos de tratamento, frota e rotas da van.',
    icone: Bus,
  },
  {
    id: 'suporte',
    titulo: 'Suporte',
    descricao: 'Pedir ajuda à equipe que mantém o sistema e, para a equipe, triar e responder os tickets.',
    icone: LifeBuoy,
  },
  {
    id: 'sistema',
    titulo: 'Sistema',
    descricao: 'Perfis, permissões, identidade da instituição e configuração.',
    icone: Settings2,
  },
];

export const ARTIGOS: Artigo[] = [
  artigoAgenteIa,
  artigoAnaliseRegrasEspelho,
  artigoAnamnese,
  artigoAssinaturaLaudo,
  artigoAtendimentosTransporte,
  artigoAvisosCelular,
  artigoAvisosConexao,
  artigoConfirmacoes,
  artigoEsusSaoGoncalo,
  artigoExtensaoChrome,
  artigoExtensaoChromeGerenciar,
  artigoFluxoAtendimentoWhatsApp,
  artigoIndicadoresRegulacao,
  artigoMensageria,
  artigoMotoristas,
  artigoOuvidoria,
  artigoPacientes,
  artigoRascunhosSerSernit,
  artigoRegulacaoGestaoFila,
  artigoRegulacaoSolicitacoes,
  artigoSerMedicos,
  artigoSernitMedicos,
  artigoSisregMedicos,
  artigoSolicitacoes,
  artigoTickets,
  artigoTicketsGestao,
  artigoTiposTratamento,
  artigoUnidadesAtendimento,
  artigoVeiculos,
];

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
