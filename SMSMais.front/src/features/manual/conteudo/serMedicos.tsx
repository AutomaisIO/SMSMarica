import { Stethoscope } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Regulação → SER → Médicos (ADR-0065): o espelho dos profissionais do SER, à parte do
 * nosso cadastro de Médicos.
 *
 * Conferido no código em 01/10/2026: `features/ser/pages/SerMedicosPage`, `features/ser/api/
 * profissionaisApi`, `SerProfissionaisController`, `SerProfissionalService`,
 * `SerProfissionalLeitor`.
 */
export const artigoSerMedicos: Artigo = {
  slug: 'ser-medicos',
  titulo: 'Médicos do SER',
  resumo:
    'Os médicos como estão cadastrados no SER, num espelho à parte do nosso cadastro, e os pedidos de cadastro de médico feitos pelas unidades.',
  grupo: 'regulacao',
  icone: Stethoscope,
  rota: '/app/regulacao/ser/medicos',
  publico: 'Quem prepara as solicitações ao SER e cuida da lista de médicos solicitantes',
  atualizadoEm: '2026-10-08',
  palavrasChave: [
    'médicos do SER',
    'profissionais',
    'cadastro de profissionais',
    'médico solicitante',
    'lotação',
    'importar do SER',
    'ligar a médico nosso',
    'desligar',
    'saiu do SER',
    'duplicado',
    'CPF',
    'CRM',
    'documento',
    'espelho',
    'enviar para o SER',
    'pedidos de cadastro',
    'médico pendente',
    'adicionar médico',
    'autorizo cadastrar',
    'cadastro a conferir',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O que é esta tela',
      busca: 'médicos do ser espelho à parte cadastro médico solicitante lista lotação município',
      conteudo: (
        <>
          <P>
            O SER só aceita como <strong>médico solicitante</strong> quem está cadastrado lá, com
            lotação no município. Esta tela mostra os profissionais <strong>exatamente como o SER os
            tem</strong> (Cadastro → Profissionais), para conferir e ligar ao nosso cadastro. O campo
            “Médico solicitante” da solicitação usa a lista do próprio combo do SER, copiada em
            “Copiar catálogo do SER” (Configuração).
          </P>
          <Callout tipo="regra" titulo="À parte do nosso cadastro">
            O cadastro do SER é ruim: quase ninguém tem CPF, metade só tem o nome, há nomes abreviados e
            cadastros repetidos. Por isso ele fica separado — nada daqui muda o nosso cadastro de
            Médicos, e nada do nosso é copiado para cá.
          </Callout>
        </>
      ),
    },
    {
      id: 'importar',
      titulo: 'Importar do SER',
      busca: 'importar do ser leitura pesquisa minutos lendo o ser última leitura falhou saiu do ser',
      conteudo: (
        <>
          <P>
            <BotaoRef>Importar do SER</BotaoRef> lê a pesquisa de profissionais do SER inteira — leva um
            ou dois minutos, e o botão mostra “Lendo o SER…” enquanto isso. É só leitura: nada é
            gravado no SER.
          </P>
          <Lista>
            <Item>Quem é novo no SER entra na lista.</Item>
            <Item>
              Quem não aparece mais na pesquisa fica marcado <SeloRef>Saiu do SER</SeloRef> — não é
              apagado, e a ligação com o nosso médico continua.
            </Item>
            <Item>
              Se a leitura voltar muito menor do que a anterior (a sessão caiu, a tela mudou), nada é
              alterado e a tela avisa que a importação falhou.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'lista',
      titulo: 'A lista',
      busca: 'cartões no ser ativos com cpf ligados saíram buscar nome cpf documento filtro situação ligação repetido',
      conteudo: (
        <>
          <P>
            Os cartões do alto contam os médicos no SER, os ativos, os que têm CPF lá, os já ligados a
            um médico nosso e os que saíram do SER. A busca aceita nome (sem exigir acento), CPF ou
            documento.
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Nome no SER', descricao: 'Como o SER escreve. “2× no SER” = o SER tem esse cadastro repetido.' },
              { termo: 'CPF / Documento', descricao: 'O que o SER tem — em geral vazio. O documento costuma ser o CRM.' },
              {
                termo: 'Situação',
                descricao: (
                  <>
                    <SeloRef cor="sucesso">Ativo</SeloRef> ou <SeloRef>Inativo</SeloRef> no SER, como está
                    na lotação de lá.
                  </>
                ),
              },
              { termo: 'Médico nosso', descricao: 'O médico do nosso cadastro que alguém confirmou ser o mesmo.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'ligar',
      titulo: 'Ligar a um médico nosso',
      busca: 'ligar a médico nosso é este desligar mesmo médico confirmar homônimo cpf crm',
      conteudo: (
        <>
          <P>
            <BotaoRef>Ligar a médico nosso</BotaoRef> abre a busca no nosso cadastro já com o nome (ou o
            CPF) do SER. Confira CPF, CRM e especialidade e clique em <BotaoRef>É este</BotaoRef>. Errou?
            <BotaoRef>Desligar</BotaoRef> desfaz.
          </P>
          <Callout tipo="atencao" titulo="Quem liga é a pessoa">
            O sistema não liga sozinho: no SER há homônimos e nomes abreviados demais para casar pelo
            nome. Na dúvida, não ligue.
          </Callout>
        </>
      ),
    },
    {
      id: 'enviar',
      titulo: 'E o médico que não está no SER? — Pedidos de cadastro',
      busca: 'enviar para o ser cadastrar médico no ser pedidos de cadastro pendente adicionar médico cadastrei já existia recusar autorizo cadastrar enviar ao ser nomes parecidos cadastro a conferir não entrou',
      conteudo: (
        <>
          <P>
            Na Nova Solicitação, a unidade que não acha o médico usa “Incluir médico”: o pedido fica{' '}
            <strong>pendente</strong>, sem escrever no SER. Os pedidos aparecem no fim desta tela, em{' '}
            <strong>Pedidos de cadastro no SER</strong>, e também no detalhe de cada solicitação.
          </P>
          <P>
            O caminho mais curto é o <strong>Aceitar e enviar ao SER</strong> da Gestão de fila: a prévia mostra
            os nomes parecidos da lista do SER (“É este”) e, com a autorização de quem regula, a plataforma
            cadastra o médico no SER pelo “Adicionar Médico” de lá e confere se o nome entrou na lista. O
            cadastro de médicos do SER não tem editar nem apagar — por isso a autorização é a cada médico.
          </P>
          <P>
            Também dá para cadastrar pela tela de solicitação do SER, ícone <strong>Adicionar médico</strong> ao
            lado de “Médico responsável” (tem tipo e número de documento, onde vai o CRM), e resolver aqui:{' '}
            <BotaoRef>Cadastrei no SER</BotaoRef>, <BotaoRef>Já existia no SER</BotaoRef> (escolhe o cadastro de
            lá; as solicitações passam a usar esse nome) ou <BotaoRef>Recusar</BotaoRef>, com o motivo. Depois,
            “Copiar catálogo do SER” traz o nome para a lista do campo.
          </P>
          <P>
            <strong>Cadastro a conferir</strong> (filtro próprio): a plataforma gravou no SER e não conseguiu ver
            o nome na lista. Confira no SER e use <BotaoRef>Já existia no SER</BotaoRef> (entrou) ou{' '}
            <BotaoRef>Não entrou</BotaoRef> (volta a aguardar cadastro). Não tente de novo sem conferir.
          </P>
        </>
      ),
    },
  ],
};
