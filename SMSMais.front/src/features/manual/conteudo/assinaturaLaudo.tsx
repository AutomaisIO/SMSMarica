import { FileText } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo da assinatura e liberação do laudo (ADR-0015, ADR-0049, ADR-0061).
 *
 * Conferido no código: `features/laudos` (LaudoEditorPage, ModalPosicionarCarimbo,
 * ModalConferenciaAssinatura, StatusBadgeLaudo), `features/medicos/assinatura`
 * (AssinaturaMedicoSecao) e no backend (`LaudoAssinaturaService`, `LaudosService`
 * — elegibilidade —, `LaudoVerificacaoService`, `PublicoController`).
 */
export const artigoAssinaturaLaudo: Artigo = {
  slug: 'assinatura-laudo',
  titulo: 'Assinatura e liberação do laudo',
  resumo:
    'Como o laudo finalizado vira documento oficial: os três jeitos de assinar, a conferência antes de liberar e o QR Code que prova a autenticidade.',
  grupo: 'assistencial',
  icone: FileText,
  rota: '/app/laudos',
  publico: 'Médicos que laudam e o administrador que cadastra os médicos',
  atualizadoEm: '2026-09-24',
  palavrasChave: [
    'assinar',
    'assinatura',
    'assinatura digital',
    'ICP-Brasil',
    'certificado',
    'VIDaaS',
    'nuvem',
    'aplicativo',
    'Automais Assinador',
    'agente',
    'sem certificado',
    'carimbo',
    'carimbado',
    'rubrica',
    'conferir',
    'aprovar',
    'rejeitar',
    'QR Code',
    'verificação',
    'autenticidade',
    'validar laudo',
    'baixar laudo',
    'rubrica não cadastrada',
    'assinador não encontrado',
    'exame sem pedido associado',
    'laudo junto com as imagens',
    'imprimir laudo',
    'solicitações',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Finalizar não é liberar',
      busca: 'finalizado assinado oficial liberado paciente aviso whatsapp',
      conteudo: (
        <>
          <P>
            Um laudo <SeloRef cor="info">Finalizado</SeloRef> está pronto no texto, mas ainda não é
            o documento oficial. O PDF que sai dele traz a tarja “documento sem assinatura
            digital” e não tem o nome do médico.
          </P>
          <P>
            O laudo vira oficial quando o médico <strong>assina</strong> (ou carimba, se não tem
            certificado) e depois <strong>confere e aprova</strong> o documento pronto. Só nesse
            momento o paciente é avisado e o PDF oficial passa a ser o que todo mundo baixa: o
            painel, o aplicativo do cidadão e o QR Code.
          </P>
          <Callout tipo="regra" titulo="Quem assina é o autor">
            Só o médico que escreveu o laudo pode assiná-lo. Para corrigir um laudo já liberado,
            use <BotaoRef variante="outline">Nova versão</BotaoRef>: o documento antigo continua
            existindo e a página do QR Code passa a avisar que ele foi substituído.
          </Callout>
        </>
      ),
    },
    {
      id: 'tres-modos',
      titulo: 'Os três jeitos de assinar',
      busca:
        'modo desktop computador nuvem vidaas celular aplicativo sem certificado carimbo administrador cadastro médico',
      conteudo: (
        <>
          <P>
            Cada médico assina de um jeito, conforme o certificado que tem. Quem escolhe é o
            administrador, no cadastro do médico, na aba <strong>Médico</strong>, em “Como este
            médico assina o laudo”. Sem escolha, vale login e senha, sem certificado.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Assinador no computador',
                descricao:
                  'O certificado ICP-Brasil está na máquina (VIDaaS Connect, token ou arquivo A1). Ao assinar, o painel chama o Automais Assinador, que precisa estar instalado uma vez por computador. O botão “Baixar Assinador” fica no laudo.',
              },
              {
                termo: 'VIDaaS em nuvem',
                descricao:
                  'O certificado é o VIDaaS em nuvem. Ao assinar, abre uma aba de autorização e o médico aprova no aplicativo VIDaaS do celular. Não precisa instalar nada no computador. O administrador da instituição precisa ter configurado a integração.',
              },
              {
                termo: 'Login e senha (sem certificado) — o padrão',
                descricao:
                  'Para o médico que não tem certificado digital: assina com o próprio acesso ao sistema. O laudo sai com a rubrica, nome e CRM e com o QR Code, mas SEM assinatura digital. O rodapé do PDF e a página do QR Code dizem isso claramente.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="Carimbo não é assinatura digital">
            Pela Resolução CFM 2.299/2021, documento médico eletrônico tem validade jurídica plena
            quando assinado com certificado ICP-Brasil. O laudo carimbado é oficial para a
            instituição, mas não tem essa validade — por isso ele aparece como{' '}
            <SeloRef cor="alerta">Carimbado</SeloRef>, e nunca como Assinado.
          </Callout>
        </>
      ),
    },
    {
      id: 'passo-a-passo',
      titulo: 'Passo a passo',
      busca: 'posicionar carimbo arrastar redimensionar página assinar botão',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: 'Abra o laudo finalizado',
                detalhe:
                  'O botão de assinar só aparece para o autor, com o laudo finalizado e o exame associado a um pedido.',
              },
              {
                titulo: 'Clique em “Assinar e liberar”',
                detalhe:
                  'É o mesmo botão nos três modos. O que muda é o que acontece depois do clique.',
              },
              {
                titulo: 'Posicione o carimbo',
                detalhe:
                  'O laudo aparece como vai sair. Arraste e redimensione o carimbo para um espaço em branco. Se ele cobrir texto, a tela avisa.',
              },
              {
                titulo: 'Autorize',
                detalhe:
                  'No computador, confirme no VIDaaS Connect. Na nuvem, aprove no aplicativo do celular. Sem certificado, não há nada a autorizar.',
              },
              {
                titulo: 'Confira e aprove',
                detalhe:
                  'O documento pronto abre para conferência. Aprovar libera o laudo e avisa o paciente.',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'conferencia',
      titulo: 'Conferir antes de liberar',
      busca: 'conferência aprovar rejeitar assinar de novo documento pronto aviso paciente',
      conteudo: (
        <>
          <P>
            Depois de assinado (ou carimbado), o laudo ainda não foi liberado. A tela mostra o PDF
            exatamente como ficou, e você decide:
          </P>
          <Lista>
            <Item>
              <BotaoRef variante="primaria">Aprovar e liberar</BotaoRef> — o laudo vira oficial e o
              paciente recebe o aviso de laudo pronto.
            </Item>
            <Item>
              <BotaoRef variante="outline">Rejeitar (assinar de novo)</BotaoRef> — o documento é
              descartado da liberação, fica guardado para auditoria, e você pode assinar de novo,
              por exemplo com o carimbo em outro lugar.
            </Item>
          </Lista>
          <P>
            Se fechar a janela sem decidir, o botão “Conferir e aprovar assinatura” fica no topo do
            laudo até você voltar.
          </P>
        </>
      ),
    },
    {
      id: 'onde-aparece',
      titulo: 'Onde o laudo aparece — e por que separado das imagens',
      busca:
        'solicitações ver laudo imagens exame pdf separado imprimir junto baixar exame completo capa',
      conteudo: (
        <>
          <P>
            Laudo e imagens são documentos diferentes. O PDF do exame, aberto pelo ícone de
            download em <strong>Solicitações</strong>, traz a capa e as imagens — o laudo não vem
            junto. Quem precisar dos dois imprime um e depois o outro.
          </P>
          <P>
            O laudo só aparece <strong>depois de liberado</strong>: assinado (ou carimbado, para o
            médico sem certificado) e aprovado na conferência. Antes disso, o ícone de laudo na
            lista de Solicitações fica cinza, e no detalhe do pedido o sistema avisa que ainda não
            há laudo liberado.
          </P>
          <Callout tipo="regra" titulo="Por que não junto">
            Juntar o laudo às imagens fazia circular uma cópia sem validade: o PDF montado levava
            a versão sem assinatura mesmo depois de o médico assinar. Separado, o único laudo que
            sai é o oficial.
          </Callout>
        </>
      ),
    },
    {
      id: 'qr-code',
      titulo: 'O QR Code de verificação',
      busca: 'qr code verificar autenticidade válido baixar pdf página pública substituído',
      conteudo: (
        <>
          <P>
            Todo laudo oficial traz no rodapé um QR Code e o endereço de conferência. Quem lê o
            código — o paciente, outro médico, um hospital — abre uma página que diz se o laudo é
            válido e oferece o botão para baixar o PDF oficial.
          </P>
          <Sub>O que a página mostra</Sub>
          <Lista>
            <Item>
              <strong>Laudo válido</strong>, com paciente, exame, médico e data. Se foi assinado
              digitalmente, mostra também o titular do certificado.
            </Item>
            <Item>
              <strong>Laudo válido — emitido com carimbo</strong>, quando o médico não tem
              certificado.
            </Item>
            <Item>
              <strong>Laudo ainda não liberado</strong>, se alguém imprimiu antes da aprovação.
            </Item>
            <Item>Um aviso de <strong>versão mais recente</strong>, se o laudo foi retificado.</Item>
          </Lista>
          <Callout tipo="lgpd" titulo="Quem tem o QR tem o laudo">
            A página mostra o nome do paciente e libera o PDF para quem tiver o código, do mesmo
            jeito que o papel mostra para quem o tiver na mão. Oriente o paciente a não publicar
            foto do laudo com o QR Code visível.
          </Callout>
        </>
      ),
    },
    {
      id: 'casos',
      titulo: 'Quando o botão não aparece ou algo falha',
      busca:
        'rubrica não cadastrada exame sem pedido associado assinatura em nuvem não configurada assinador não encontrado tentar de novo expirou',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Rubrica não cadastrada',
              descricao:
                'Sem a imagem da rubrica não há carimbo, em nenhum dos três modos. Peça ao administrador para enviar a imagem no cadastro do médico.',
            },
            {
              termo: 'Exame sem pedido associado',
              descricao:
                'Laudo de exame que não está ligado a um pedido não pode ser assinado, porque não há paciente confirmado. Associe o exame ao pedido primeiro.',
            },
            {
              termo: 'Assinatura em nuvem não configurada',
              descricao:
                'O médico está no modo VIDaaS em nuvem, mas a integração não foi ligada nesta instituição. O administrador precisa configurá-la ou trocar o modo do médico.',
            },
            {
              termo: 'Assinador não encontrado',
              descricao:
                'No modo computador, o painel esperou o Automais Assinador e ele não respondeu. Instale pelo botão “Baixar Assinador” e tente de novo.',
            },
            {
              termo: '“Tentar de novo” depois de esperar',
              descricao:
                'A autorização tem prazo: poucos minutos no computador, dez minutos na nuvem. Passou do prazo, o painel oferece começar outra vez.',
            },
          ]}
        />
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão módulo laudos médicos edição administrador',
      conteudo: (
        <Lista>
          <Item>
            <strong>Assinar, conferir e aprovar</strong>: o médico autor do laudo, com permissão de
            edição em Laudos.
          </Item>
          <Item>
            <strong>Escolher o modo e cadastrar a rubrica</strong>: quem tem permissão de edição em
            Médicos, normalmente o administrador. O médico não troca o próprio modo.
          </Item>
          <Item>
            <strong>Conferir pelo QR Code</strong>: qualquer pessoa com o código, sem login.
          </Item>
        </Lista>
      ),
    },
  ],
};
