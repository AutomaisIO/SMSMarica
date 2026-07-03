import type { ReactNode } from 'react';

/**
 * Textos de ajuda dos campos da Solicitação de Exame, exibidos pelo "?" (AjudaCampo)
 * ao lado de cada rótulo. Fonte única — mantém a explicação da tela alinhada com a
 * documentação em `docs/modulos/solicitacoes-exame.md`.
 */
export const AJUDA_SOLICITACAO: Record<string, { titulo: string; conteudo: ReactNode }> = {
  dataSolicitacao: {
    titulo: 'Data da solicitação',
    conteudo: (
      <>
        <p>
          Data em que o exame foi <strong>pedido</strong> pelo profissional (a data do pedido/guia).
          É um dia de calendário, sem hora.
        </p>
        <p>
          Não confunda com a <strong>data de cadastro no sistema</strong> (quando o registro foi criado
          aqui) nem com a <strong>data agendada</strong> (quando o exame vai acontecer). Nas importações
          do SISREG, este campo vem preenchido automaticamente com a data da solicitação do TXT.
        </p>
      </>
    ),
  },
  dataAgendada: {
    titulo: 'Data/hora agendada',
    conteudo: (
      <>
        <p>
          Data e hora em que o exame está <strong>marcado para ser realizado</strong>. Opcional.
        </p>
        <p>
          É diferente da <strong>data da solicitação</strong> (quando foi pedido) e da data real de
          execução, que é lida do próprio exame (DICOM) quando ele chega ao PACS.
        </p>
      </>
    ),
  },
  unidadeSolicitante: {
    titulo: 'Unidade solicitante',
    conteudo: (
      <>
        <p>
          A unidade de saúde que <strong>pediu</strong> o exame (ex.: a USF de origem do paciente).
          Opcional.
        </p>
        <p>
          Não confunda com a <strong>unidade executora</strong>, que é onde o exame é realizado
          (ex.: o CDT). As duas podem ser diferentes.
        </p>
      </>
    ),
  },
  codigoSolicitacao: {
    titulo: 'Código de Solicitação',
    conteudo: (
      <>
        <p>
          Número da solicitação na <strong>regulação (SISREG)</strong>. Serve de chave para evitar
          importar o mesmo pedido duas vezes.
        </p>
        <p>
          Use um número a partir de <strong>9999</strong>, ou o sentinela <strong>0000</strong> para
          exames feitos em caráter <strong>emergencial fora do SUS</strong>.
        </p>
      </>
    ),
  },
  chaveConfirmacao: {
    titulo: 'Chave de Confirmação',
    conteudo: (
      <>
        <p>
          Chave que confirma a marcação na regulação. Segue a mesma régua do Código de Solicitação:
          número a partir de <strong>9999</strong> ou <strong>0000</strong> (emergência extra-SUS).
        </p>
      </>
    ),
  },
  prioridade: {
    titulo: 'Prioridade',
    conteudo: (
      <>
        <p>Grau de urgência clínica do exame:</p>
        <ul className="list-disc pl-5">
          <li><strong>Eletiva</strong> — sem urgência, fluxo normal.</li>
          <li><strong>Prioritária</strong> — deve ser priorizada na fila.</li>
          <li><strong>Urgente</strong> — precisa ser realizada o quanto antes.</li>
        </ul>
      </>
    ),
  },
  solicitante: {
    titulo: 'Solicitante',
    conteudo: (
      <>
        <p>
          Nome do profissional (ou unidade) que <strong>pediu</strong> o exame, como texto livre.
        </p>
        <p>
          Não cadastramos médico nem número de conselho (CRM/COREN) por aqui — apenas o nome, que
          aparece no laudo e na capa do exame.
        </p>
      </>
    ),
  },
};
