import { Puzzle } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo do gerenciador da extensão (Sistema → Extensão Chrome).
 *
 * Conferido no código: `features/extensao-navegador` (ExtensaoGerenciarPage, AbaComputadores,
 * AbaVersoes, AbaApiPublicacao, lib/formatar), backend `ExtensaoDistribuicaoController` e
 * `ExtensaoDistribuicaoService` (canais, regra "teste recebe a mais nova", recusas de publicação,
 * retirar, revogar, chave de publicação de uso restrito) e `PacoteDaExtensao` (o que faz um
 * pacote ser recusado).
 */
export const artigoExtensaoChromeGerenciar: Artigo = {
  slug: 'extensao-chrome-gerenciar',
  titulo: 'Extensão Chrome: computadores e versões',
  resumo:
    'O gerenciador da extensão: quem está com ela e em que versão, como publicar uma versão nova, conferir em teste e pôr em produção.',
  grupo: 'sistema',
  icone: Puzzle,
  rota: '/app/extensao/gerenciar',
  publico: 'Quem administra o sistema',
  atualizadoEm: '2026-10-01',
  palavrasChave: [
    'extensão',
    'chrome',
    'atualizador',
    'versão',
    'publicar',
    'produção',
    'teste',
    'canal',
    'promover',
    'retirar',
    'revogar',
    'computador',
    'inventário',
    'desatualizada',
    'sem contato',
    'modo desenvolvedor',
    'api',
    'chave de publicação',
    'pacote',
    'zip',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'gerenciador extensão atualizador hospedado plataforma computadores versões desenvolvimento publicado',
      conteudo: (
        <>
          <P>
            A extensão do Chrome e o programa que a mantém atualizada (o atualizador) ficam
            hospedados nesta plataforma. Esta tela é onde se vê <strong>quem está com a extensão</strong>{' '}
            e onde se decide <strong>o que chega aos computadores</strong>.
          </P>
          <P>
            O que está sendo desenvolvido não chega a computador nenhum. Só chega o que foi{' '}
            <strong>publicado aqui</strong> — e em duas etapas: primeiro para os computadores de
            teste, depois para todos.
          </P>
        </>
      ),
    },
    {
      id: 'canais',
      titulo: 'Teste e produção',
      busca: 'canal teste produção publicar conferir promover todos os computadores dez minutos',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Teste',
                descricao:
                  'Recebe a versão mais nova publicada, promovida ou não. São os computadores que você marcou como de teste.',
              },
              {
                termo: 'Produção',
                descricao: 'Recebe só o que foi posto em produção. Todo computador nasce aqui ao ser autorizado.',
              },
            ]}
          />
          <P>
            O canal é do computador e se troca nesta tela, sem encostar nele: vale na próxima vez que
            ele consultar a plataforma (até 10 minutos).
          </P>
          <Callout tipo="regra" titulo="Computador não volta de versão">
            Uma versão só é instalada se for maior que a que o computador já tem. Por isso não se
            publica versão igual ou menor que a última, e o conserto de uma versão ruim é sempre
            publicar uma maior.
          </Callout>
        </>
      ),
    },
    {
      id: 'computadores',
      titulo: 'Aba Computadores',
      busca:
        'computadores autorizados inventário versão extensão atualizador chrome último contato desatualizada sem contato passar para teste produção revogar',
      conteudo: (
        <>
          <P>
            Uma linha por computador autorizado, com o que ele mesmo informa a cada consulta (de 10
            em 10 minutos).
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Extensão e Atualizador',
                descricao: (
                  <>
                    A versão que o computador tem. <SeloRef cor="alerta">Desatualizada</SeloRef> aparece
                    quando ela está atrás da que o canal dele recebe.
                  </>
                ),
              },
              {
                termo: 'No Chrome',
                descricao: (
                  <>
                    Como a extensão está no navegador: carregada, Chrome fechado, ou um problema em
                    vermelho — <SeloRef cor="erro">Chrome aberto sem a extensão</SeloRef>,{' '}
                    <SeloRef cor="erro">Desativada no Chrome</SeloRef> ou{' '}
                    <SeloRef cor="erro">Modo desenvolvedor desligado</SeloRef>. Nesses casos a pessoa
                    também é avisada no próprio computador.
                  </>
                ),
              },
              {
                termo: 'Último contato',
                descricao: (
                  <>
                    Quando o computador falou com a plataforma pela última vez.{' '}
                    <SeloRef>Sem contato</SeloRef> = mais de 30 minutos (desligado, sem internet, ou o
                    programa foi fechado).
                  </>
                ),
              },
            ]}
          />
          <P>
            <BotaoRef variante="ghost">Passar para teste</BotaoRef> /{' '}
            <BotaoRef variante="ghost">Passar para produção</BotaoRef> troca o canal.{' '}
            <BotaoRef variante="ghost">Revogar</BotaoRef> faz o computador deixar de receber a
            extensão e as atualizações; a extensão que já está nele continua lá, e para voltar a
            receber é preciso autorizar de novo. Os revogados somem da lista (marque{' '}
            <em>Mostrar revogados</em> para vê-los).
          </P>
        </>
      ),
    },
    {
      id: 'versoes',
      titulo: 'Aba Versões: publicar e pôr em produção',
      busca:
        'versões publicar pacote zip executável manifest versão notas em teste em produção substituída não promovida retirada pôr em produção retirar recusado',
      conteudo: (
        <>
          <P>
            Duas listas — <strong>Extensão</strong> e <strong>Atualizador</strong> — com tudo o que já
            foi publicado. O selo diz onde cada versão está:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: <SeloRef cor="alerta">Em teste</SeloRef>, descricao: 'É a que os computadores de teste recebem agora; ainda não foi para todos.' },
              { termo: <SeloRef cor="sucesso">Em produção</SeloRef>, descricao: 'É a que todos os computadores recebem agora.' },
              { termo: <SeloRef>Substituída</SeloRef>, descricao: 'Já esteve em produção e há uma mais nova no lugar.' },
              { termo: <SeloRef>Não promovida</SeloRef>, descricao: 'Ficou só em teste e há uma mais nova.' },
              { termo: <SeloRef cor="erro">Retirada</SeloRef>, descricao: 'Deixou de ser entregue.' },
            ]}
          />
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    <BotaoRef>Publicar versão</BotaoRef> e escolha o que é
                  </>
                ),
                detalhe:
                  'Extensão: o pacote .zip — a versão é lida do manifest de dentro dele. Atualizador: o executável, informando a versão (a mesma do executável).',
              },
              {
                titulo: 'Confira num computador de teste',
                detalhe:
                  'A versão entra em teste. Um computador de teste a recebe em até 10 minutos (ou na hora, pelo "Verificar agora" do ícone dele).',
              },
              {
                titulo: (
                  <>
                    <BotaoRef variante="ghost">Pôr em produção</BotaoRef>
                  </>
                ),
                detalhe: 'Todos os computadores autorizados passam a receber. Não dá para desfazer nos que já receberam.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="O pacote é conferido ao publicar">
            A plataforma recusa o que os computadores recusariam: .zip sem manifest, manifest
            inválido, arquivo que o manifest manda carregar e não está no pacote, versão repetida ou
            menor que a última, e o instalador baixado do painel no lugar do executável do
            atualizador. O motivo aparece na própria janela de publicação.
          </Callout>
          <P>
            <BotaoRef variante="ghost">Retirar</BotaoRef> tira a versão de circulação. Quem já a
            recebeu continua com ela (computador não volta de versão); os demais passam a receber a
            anterior que ainda vale.
          </P>
        </>
      ),
    },
    {
      id: 'api',
      titulo: 'Aba Publicação por API',
      busca: 'publicação por api chave ligar desligar gerar outra chave X-Chave-Publicacao script último uso',
      conteudo: (
        <>
          <P>
            Publicar por esta tela é o caminho normal. A <AbaRef>Publicação por API</AbaRef> é uma
            opção para quem publica por script: ela fica <SeloRef>Desligada</SeloRef> até alguém gerar
            uma chave.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'O que a chave pode',
                descricao: 'Listar as versões, publicar (entra em teste) e pôr em produção. Nada mais do sistema — nem revogar computador, nem retirar versão.',
              },
              {
                termo: 'Ligar e gerar chave',
                descricao: 'A chave aparece uma única vez, na hora. Copie e guarde: depois só o começo dela fica à vista.',
              },
              {
                termo: 'Gerar outra chave',
                descricao: 'A anterior deixa de valer na hora. Só há uma chave ativa por vez.',
              },
              { termo: 'Desligar', descricao: 'A chave deixa de valer e só se publica pela tela.' },
            ]}
          />
          <P>
            O que entra pela API fica marcado como <em>pela API</em> na lista de versões, e a aba
            mostra quando a chave foi usada pela última vez.
          </P>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo extensão chrome consulta inclusão edição exclusão',
      conteudo: (
        <>
          <P>
            A tela pede o módulo <strong>Extensão Chrome</strong> no perfil. Baixar o instalador e
            autorizar o próprio computador não pedem módulo nenhum.
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Ver os computadores e as versões.' },
              { termo: 'Inclusão', descricao: 'Publicar versão (entra em teste).' },
              {
                termo: 'Edição',
                descricao: 'Pôr versão em produção, trocar o canal de um computador e ligar ou desligar a publicação por API.',
              },
              { termo: 'Exclusão', descricao: 'Retirar versão e revogar computador.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca:
        'publiquei e ninguém recebeu versão ruim instalador não aparece computador duplicado mesmo nome primeira publicação',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Publiquei e os computadores não receberam',
              descricao: 'Versão publicada vai só para o canal de teste. Para chegar a todos, ponha em produção.',
            },
            {
              termo: 'Ninguém consegue baixar o instalador',
              descricao:
                'O instalador é o atualizador que está em produção. Publique o atualizador e ponha em produção — na primeira vez, logo em seguida.',
            },
            {
              termo: 'Uma versão ruim foi para produção',
              descricao:
                'Retire-a (para de ser entregue) e publique uma versão maior com o conserto. Quem já recebeu a ruim só sai dela com a versão maior.',
            },
            {
              termo: 'O mesmo computador aparece duas vezes',
              descricao:
                'Ele foi autorizado de novo (Configurar, ou instalador novo). A linha antiga fica sem contato; pode revogá-la.',
            },
          ]}
        />
      ),
    },
  ],
};
