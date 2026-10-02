import { FilePlus2 } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo das telas antigas "Nova solicitação (SER)" e "Nova solicitação (SERNIT)" — os rascunhos
 * guardados na nossa base. As duas telas funcionam igual; o "?" de cada uma aponta para cá pelo slug.
 *
 * Conferido no código em 02/10/2026: `features/ser/pages/SerNovaSolicitacaoPage`,
 * `features/sernit/pages/SernitNovaSolicitacaoPage`, `shared/regulacao/rascunhoLegado` (corte por
 * `regulacao_configuracao.rascunhos_legados_migrados_em`), `shared/acervo/AnexosRascunho`,
 * `SerRascunhoService`/`SernitRascunhoService` (10 MB por anexo, cópia no acervo pelo CNS).
 */
export const artigoRascunhosSerSernit: Artigo = {
  slug: 'rascunhos-ser-sernit',
  titulo: 'Nova solicitação do SER e do SERNIT (telas antigas)',
  resumo:
    'As telas antigas de rascunho de pedido ao SER e ao SERNIT: o que mostram, por que viraram histórico e onde se abre pedido novo hoje.',
  grupo: 'regulacao',
  icone: FilePlus2,
  rota: '/app/regulacao/nova-solicitacao',
  publico: 'Quem montava pedidos ao SER/SERNIT pela tela antiga e quem precisa consultar um rascunho antigo',
  atualizadoEm: '2026-10-02',
  palavrasChave: [
    'nova solicitação SER',
    'nova solicitação SERNIT',
    'rascunho',
    'rascunhos',
    'tela antiga',
    'histórico',
    'somente leitura',
    'virou histórico',
    'pronto para envio',
    'marcar pronto',
    'enviar ao SER',
    'recurso',
    'ambulatório estadual',
    'catálogo',
    'buscar no SER',
    'buscar no SERNIT',
    'CNS',
    'classificação de risco',
    'hipótese',
    'CID',
    'anexo',
    'anexar do cadastro',
    'visualizar anexo',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve — e por que virou histórico',
      busca: 'tela antiga histórico somente leitura migrado regulação solicitações substituída onde abrir pedido novo',
      conteudo: (
        <>
          <P>
            Antes de existir <strong>Regulação → Solicitações</strong>, o pedido ao SER e ao SERNIT era
            montado aqui, um por sistema, e ficava guardado na nossa base como <strong>rascunho</strong>
            até o envio ser ligado.
          </P>
          <P>
            Quando os rascunhos são migrados para Regulação → Solicitações, estas telas passam a mostrar
            o aviso <SeloRef cor="info">Esta tela virou histórico</SeloRef> e ficam{' '}
            <strong>só para consulta</strong>: nenhum campo se edita, nada se anexa e o botão{' '}
            <BotaoRef variante="outline">Novo</BotaoRef> fica desabilitado. O botão{' '}
            <BotaoRef>Nova solicitação</BotaoRef> do aviso leva ao caminho novo.
          </P>
          <Callout tipo="regra" titulo="Pedido novo é em Regulação → Solicitações">
            Lá o mesmo pedido vale para o SER e para o SERNIT, passa pelas regras do procedimento, tem
            anexos por documento exigido e entra na fila da pré-regulação. Aqui era um formulário por
            sistema, sem fila — mantê-lo vivo seria ter dois lugares fazendo a mesma coisa.
          </Callout>
        </>
      ),
    },
    {
      id: 'lendo-a-tela',
      titulo: 'Como ler a tela',
      busca: 'lista de rascunhos lateral status rascunho pronto enviado falhou número no SER anexos',
      conteudo: (
        <>
          <P>
            À direita fica a lista <strong>Rascunhos</strong> — paciente (ou CNS), recurso, quantidade de
            anexos e quando foi mexido pela última vez. Clicar abre o rascunho no formulário. O selo diz
            em que ponto ele estava:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Rascunho', descricao: 'Guardado, mas ainda incompleto ou não conferido.' },
              {
                termo: 'Pronto',
                descricao:
                  'Completo — todos os campos obrigatórios do recurso preenchidos — e esperando a autorização de envio.',
              },
              {
                termo: 'Enviado',
                descricao: 'Foi ao sistema de regulação; aparece o número gerado lá. Não se edita mais.',
              },
              { termo: 'Falhou', descricao: 'A tentativa de envio voltou com erro; a mensagem aparece ao lado.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'formulario',
      titulo: 'O formulário (enquanto a tela está em uso)',
      busca:
        'ambulatório estadual tipo consulta exame recurso catálogo campos do recurso CNS CPF buscar no SER cadastro do paciente classificação de risco médico responsável hipótese CID salvar rascunho marcar pronto enviar desligado',
      conteudo: (
        <>
          <P>Numa instância em que a migração ainda não aconteceu, a tela é montada assim:</P>
          <Lista>
            <Item>
              <strong>O que está sendo pedido</strong> — no SER, primeiro “É ambulatório estadual?”: a
              resposta muda a lista de recursos e o formulário de cada um. Depois o tipo e o{' '}
              <strong>recurso</strong>. Os recursos vêm do catálogo copiado do sistema; sem a cópia, a tela
              avisa e manda rodar em Regulação → Configuração.
            </Item>
            <Item>
              <strong>Paciente</strong> — CNS ou CPF e <BotaoRef variante="outline">Buscar no SER</BotaoRef>{' '}
              (ou no SERNIT): o cadastro de lá preenche os campos. Nome, CPF, CNS, nascimento, sexo, mãe e
              raça voltam travados, porque o sistema não aceita correção deles pelo pedido.
            </Item>
            <Item>
              <strong>Classificação</strong> — classificação de risco, médico responsável e hipótese (CID).
            </Item>
            <Item>
              <strong>Campos do recurso</strong> — mudam conforme o recurso escolhido.
            </Item>
          </Lista>
          <P>
            <BotaoRef>Salvar rascunho</BotaoRef> guarda como está;{' '}
            <BotaoRef variante="outline">Marcar pronto para envio</BotaoRef> recusa se faltar campo
            obrigatório. O <BotaoRef variante="outline">Enviar</BotaoRef> nunca foi ligado nesta tela.
          </P>
        </>
      ),
    },
    {
      id: 'anexos',
      titulo: 'Anexos',
      busca:
        'anexo anexar arquivo nome do documento descrição visualizar abrir anexar do cadastro exames anexados CNS 10 MB no SER',
      conteudo: (
        <>
          <P>
            Clique no nome do anexo para abri-lo no visualizador do sistema — vale também na tela que
            virou histórico. Anexo que já subiu para o sistema mostra <SeloRef cor="sucesso">no SER</SeloRef>{' '}
            (ou no SERNIT) e não se remove.
          </P>
          <P>Enquanto a tela está em uso:</P>
          <Lista>
            <Item>
              <BotaoRef variante="outline">Anexar arquivo</BotaoRef> pede o <strong>nome do documento</strong>{' '}
              e uma descrição; até 10 MB por arquivo. Se o CNS é de um paciente cadastrado, o arquivo também
              fica no cadastro dele (aba Exames anexados).
            </Item>
            <Item>
              <BotaoRef variante="outline">Anexar do cadastro</BotaoRef> lista o que o paciente daquele CNS
              já tem guardado — documentos, laudos assinados, imagens de exames — e anexa sem novo upload. O
              rascunho é salvo antes, para o CNS digitado valer.
            </Item>
          </Lista>
          <Callout tipo="dica" titulo="“Nada no cadastro”?">
            A lista do cadastro é achada pelo CNS. Se veio vazia, confira se o CNS está completo (15
            dígitos) e se é de um paciente cadastrado.
          </Callout>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'não consigo editar campos travados onde está o rascunho que eu fiz pedido migrado',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Não consigo editar nada nesta tela.',
              descricao:
                'Ela virou histórico (o aviso azul no topo). Abra o pedido em Regulação → Solicitações — os rascunhos antigos foram copiados para lá na migração.',
            },
            {
              termo: 'Onde foi parar o rascunho que eu tinha feito?',
              descricao:
                'Continua listado aqui, para consulta, e foi copiado para Regulação → Solicitações, onde segue o caminho normal.',
            },
            {
              termo: 'Por que o nome do paciente veio travado?',
              descricao:
                'O sistema de regulação não aceita alterar identidade pelo pedido. Correção de cadastro é feita no próprio sistema (ou no CADSUS).',
            },
          ]}
        />
      ),
    },
  ],
};
