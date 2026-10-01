import { Puzzle } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo da tela "Extensão Chrome" (menu do perfil) e da página "Autorizar computador".
 *
 * Conferido no código: `features/extensao-navegador` (ExtensaoChromePage, AutorizarComputadorPage),
 * `app/layout/Header.tsx` (item do menu do perfil), backend `ExtensaoDistribuicaoController`
 * (`/extensao/situacao`, `/extensao/instalador`, `/extensao/dispositivos/ativacoes/*`) e
 * `ExtensaoDistribuicaoService` (código de uso único, validade de 1 hora no instalador e de 10
 * minutos no Configurar) e no programa do computador (`SMSMais.atualizador`: pasta, ícone, menu,
 * avisos, ciclo de 10 minutos).
 */
export const artigoExtensaoChrome: Artigo = {
  slug: 'extensao-chrome',
  titulo: 'Extensão Chrome: instalar neste computador',
  resumo:
    'Como pôr a extensão do Chrome no computador: baixar o instalador pelo painel, os passos no Chrome e o que fazer quando ela não aparece.',
  grupo: 'sistema',
  icone: Puzzle,
  rota: '/app/extensao',
  publico: 'Quem usa a extensão do Chrome no computador de trabalho',
  atualizadoEm: '2026-10-01',
  palavrasChave: [
    'extensão',
    'chrome',
    'instalar',
    'instalador',
    'atualizador',
    'atualização',
    'baixar',
    'modo do desenvolvedor',
    'carregar sem compactação',
    'autorizar computador',
    'configurar',
    'ícone',
    'bandeja',
    'relógio',
    'windows protegeu',
    'administrador',
    'F5',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'extensão chrome instalar computador atualizar sozinha programa atualizador',
      conteudo: (
        <>
          <P>
            A extensão do Chrome não vem de loja: ela é entregue por esta plataforma, e só a
            computador autorizado por alguém logado aqui. Esta tela é a porta de entrada — você baixa
            um programa pequeno, o <strong>atualizador</strong>, que instala a extensão e depois a
            mantém em dia sozinho.
          </P>
          <P>
            A tela fica no menu do seu nome (canto de cima, à direita), em{' '}
            <strong>Extensão Chrome</strong>, e é aberta a qualquer pessoa logada.
          </P>
        </>
      ),
    },
    {
      id: 'instalar',
      titulo: 'Instalar',
      busca:
        'baixar instalador executar windows protegeu mais informações executar assim mesmo administrador senha autoriza no seu nome uma hora um computador só',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    Clique em <BotaoRef>Baixar instalador</BotaoRef>
                  </>
                ),
                detalhe:
                  'O arquivo já sai autorizado no seu nome. Ele vale por 1 hora e para um computador só: para instalar em outro, entre no painel naquele computador e baixe de novo lá.',
              },
              {
                titulo: 'Execute o arquivo baixado',
                detalhe:
                  'Não pede senha de administrador. Se o Windows mostrar "O Windows protegeu o computador", clique em "Mais informações" e depois em "Executar assim mesmo".',
              },
              {
                titulo: 'Aguarde a janela de confirmação',
                detalhe:
                  'O programa se instala, liga este computador à plataforma e baixa a extensão. Um ícone "+" aparece perto do relógio (no Windows 11 ele pode ficar dentro da setinha de ícones ocultos).',
              },
              {
                titulo: 'Só na primeira vez: carregue a extensão no Chrome',
                detalhe:
                  'Abra chrome://extensions, ligue o "Modo do desenvolvedor" (canto de cima, à direita), clique em "Carregar sem compactação" e escolha a pasta que o programa mostrou. Ele abre a pasta para você.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo='O "Modo do desenvolvedor" precisa continuar ligado'>
            Se alguém desligar, o Chrome desativa a extensão na hora. O programa avisa pelo ícone, mas
            não consegue religar: é preciso voltar em chrome://extensions e ligar de novo.
          </Callout>
        </>
      ),
    },
    {
      id: 'depois',
      titulo: 'Depois de instalado',
      busca:
        'atualiza sozinha recarrega dez minutos aviso extensão atualizada F5 recarregar página ícone menu verificar agora abrir pasta registro sair',
      conteudo: (
        <>
          <P>
            Não há mais nada a fazer. A cada 10 minutos o programa pergunta à plataforma se há versão
            nova; havendo, baixa, troca os arquivos e a extensão se recarrega sozinha. Sai um aviso do
            Windows, <em>"Extensão atualizada"</em>.
          </P>
          <Callout tipo="dica" titulo="Depois do aviso, dê F5 nas páginas abertas">
            O Chrome não troca o código das páginas que já estavam abertas. Recarregue (F5) as abas
            em que você usa a extensão.
          </Callout>
          <P>O ícone "+" perto do relógio tem um menu (clique nele):</P>
          <ListaDefinicoes
            itens={[
              { termo: 'Verificar agora', descricao: 'Confere na hora se há versão nova, sem esperar os 10 minutos.' },
              { termo: 'Abrir a pasta da extensão', descricao: 'A pasta que o Chrome carrega.' },
              { termo: 'Abrir o registro', descricao: 'O histórico do que o programa fez — útil para o suporte.' },
              { termo: 'Configurar…', descricao: 'Liga o computador à plataforma de novo (veja "Autorizar um computador").' },
              { termo: 'Sair', descricao: 'Fecha o programa até o próximo logon. A extensão continua no Chrome, só deixa de se atualizar.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'autorizar',
      titulo: 'Autorizar um computador',
      busca:
        'autorizar computador pedido página configurar código venceu dez minutos conferir nome do computador revogado não reconhece',
      conteudo: (
        <>
          <P>
            Quando o instalador não veio daqui (ou passou de 1 hora), ou quando o computador foi
            revogado, usa-se o <strong>Configurar…</strong> do ícone "+". O programa pede o endereço
            da plataforma e abre no navegador uma página deste painel pedindo a autorização.
          </P>
          <Passos
            itens={[
              {
                titulo: 'Confira o nome do computador',
                detalhe: 'A página mostra qual computador está pedindo e até quando o pedido vale (10 minutos).',
              },
              {
                titulo: (
                  <>
                    Clique em <BotaoRef>Autorizar</BotaoRef>
                  </>
                ),
                detalhe:
                  'Autorize só se o pedido foi feito por você ou pela sua equipe, naquele computador. A página avisa quando o computador terminou de se ligar.',
              },
            ]}
          />
          <P>
            Se a página disser que o pedido venceu, volte ao computador e use o Configurar de novo —
            ele gera outro pedido.
          </P>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil qualquer usuário logado baixar autorizar administração módulo',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Qualquer pessoa logada',
              descricao: 'Baixar o instalador e autorizar um computador. O computador fica registrado no nome de quem autorizou.',
            },
            {
              termo: 'Quem tem o módulo "Extensão Chrome" no perfil',
              descricao:
                'Ver todos os computadores e as versões, publicar versão, pôr em produção e revogar — pela tela de Sistema → Extensão Chrome.',
            },
          ]}
        />
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca:
        'não aparece instalador não publicado chrome aberto sem a extensão desativada outro usuário do windows outro computador desinstalar',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'O botão de baixar está apagado',
              descricao: 'O instalador ainda não foi publicado nesta plataforma. Fale com quem administra o sistema.',
            },
            {
              termo: 'O ícone avisou "O Chrome está aberto sem a extensão"',
              descricao:
                'A extensão não está carregada neste Chrome. Refaça o último passo da instalação (Carregar sem compactação, escolhendo a pasta da extensão).',
            },
            {
              termo: 'Outra pessoa usa o mesmo computador com outro usuário do Windows',
              descricao:
                'O programa e a extensão ficam numa pasta comum, e a autorização é do computador. A outra pessoa só precisa executar o instalador uma vez (para ele abrir no logon dela) e carregar a extensão no Chrome dela.',
            },
            {
              termo: 'A página que eu estava usando parou de responder à extensão',
              descricao: 'A extensão foi atualizada com a página aberta. Dê F5.',
            },
          ]}
        />
      ),
    },
  ],
};
