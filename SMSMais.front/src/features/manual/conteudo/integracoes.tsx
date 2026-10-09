import { Settings2 } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Passos } from '@/features/manual/components/Passos';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Integrações → Credenciais. Nasceu em 09/10/2026 com o Firebase (notificações do app do
 * cidadão); os demais cards ainda não têm seção própria.
 *
 * Conferido no código: `features/integracoes/pages/IntegracoesPage` (seções e selo dos cards),
 * `components/FirebaseCard` (JSON da conta de serviço lido no navegador, projectId em
 * parametrosJson, Testar só com a credencial gravada, Ativo, Limpar) e
 * `components/DigitalOceanSpacesCard` (permissões Edição/Exclusão de IntegracoesConfig), contra o
 * contrato do servidor (`POST /integracoes/credenciais/fcm/testar`, validação do JSON ao gravar).
 *
 * O Testar (`PushCidadaoService.TestarCredencialAsync`) obtém o token de acesso E faz um envio de
 * validação (`ClienteFcm.ValidarEnvioAsync`, validate_only para um tópico — não entrega nada). As
 * mensagens de falha são as de `InterpretadorRespostaFcm.InterpretarValidacao` e a da credencial que
 * não decifra (`MensagemCredencialIlegivel`). O que ele não alcança: chave da Apple e app gerado com
 * outro projeto (`SENDER_ID_MISMATCH`), que só aparecem no envio a um aparelho.
 */
export const artigoIntegracoes: Artigo = {
  slug: 'integracoes',
  titulo: 'Integrações e credenciais',
  resumo:
    'As chaves dos serviços externos que a plataforma usa — login social, armazenamento, SISREG, e-SUS, mapas, WhatsApp, voz e o Firebase das notificações do app do cidadão —, gravadas cifradas e nunca exibidas de volta.',
  grupo: 'sistema',
  icone: Settings2,
  rota: '/app/integracoes',
  publico: 'Quem administra as integrações da instância (não quem atende o paciente)',
  atualizadoEm: '2026-10-09',
  palavrasChave: [
    'integrações',
    'integração',
    'credenciais',
    'credencial',
    'chave',
    'segredo',
    'cifrado',
    'configurado',
    'não configurado',
    'inativo',
    'firebase',
    'FCM',
    'Firebase Cloud Messaging',
    'notificação',
    'notificações',
    'push',
    'app do cidadão',
    'conta de serviço',
    'service account',
    'chave privada',
    'gerar nova chave privada',
    'JSON',
    'project_id',
    'projeto',
    'testar',
    'envio de validação',
    'validate_only',
    'credencial válida',
    'API desligada',
    'Firebase Cloud Messaging API (V1)',
    'Google Cloud',
    'APIs e serviços',
    'permissão de envio',
    'Firebase Cloud Messaging Admin',
    'Firebase Admin SDK',
    'projeto não encontrado',
    'outro projeto do Firebase',
    'credencial ilegível',
    'cifrada em outro ambiente',
    'ativo',
    'limpar',
    'APNs',
    'iPhone',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve esta tela',
      busca: 'credenciais chaves tokens provedores externos cifrado nunca exibido substituir selo configurado inativo não configurado card',
      conteudo: (
        <>
          <P>
            Guarda as chaves que a plataforma usa para falar com serviços de fora: os provedores de login
            do app do cidadão, o armazenamento dos exames digitalizados, a consulta ao SISREG, o e-SUS da
            atenção básica, os mapas e o WhatsApp do transporte, a transcrição de voz e o envio de
            notificações ao app do cidadão. Cada serviço é um card; clique no título para abrir.
          </P>
          <Callout tipo="regra" titulo="O que é segredo não volta para a tela">
            Senhas, chaves e arquivos de credencial são gravados cifrados e nunca são exibidos de novo —
            nem para quem gravou. O card diz só se o valor está definido. Para trocar, preencha o campo
            de novo; deixar em branco mantém o que está gravado.
          </Callout>
          <P>
            Na maioria dos cards, o selo no título resume a situação (o do WhatsApp do transporte diz
            Conectado ou Desligado):
          </P>
          <ListaDefinicoes
            itens={[
              { termo: <SeloRef cor="sucesso">Configurado</SeloRef>, descricao: 'Credencial gravada e ligada.' },
              {
                termo: <SeloRef>Inativo</SeloRef>,
                descricao: 'Credencial gravada, mas desligada pela chave de ativo do card — o serviço não é usado.',
              },
              { termo: <SeloRef>Não configurado</SeloRef>, descricao: 'Nada gravado ainda.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'firebase',
      titulo: 'Firebase — notificações do app do cidadão',
      busca:
        'firebase fcm firebase cloud messaging notificação push app do cidadão celular conta de serviço service account chave privada gerar nova chave privada json project_id projeto console configurações do projeto contas de serviço',
      conteudo: (
        <>
          <P>
            O Firebase é o serviço do Google que leva notificações ao celular, no Android e no iPhone. É
            por ele que sai a notificação enviada pela ficha do paciente (aba Histórico de Acesso). Sem
            este card configurado e ativo, a ficha avisa que o envio não foi configurado e nada sai.
          </P>
          <P>
            O que se grava aqui é o <strong>JSON da conta de serviço</strong> do projeto do Firebase em
            que o app do cidadão está registrado — é o que prova ao Google que o servidor pode mandar
            notificação em nome do app. Para pegar o arquivo:
          </P>
          <Passos
            itens={[
              { titulo: 'Abra o console do Firebase e entre no projeto do app do cidadão.' },
              {
                titulo: 'Vá em Configurações do projeto (a engrenagem ao lado de “Visão geral do projeto”).',
              },
              { titulo: 'Abra a aba Contas de serviço.' },
              {
                titulo: 'Clique em “Gerar nova chave privada” e confirme.',
                detalhe: 'O navegador baixa um arquivo .json.',
              },
              {
                titulo: 'Abra o arquivo num editor de texto, copie tudo e cole no card.',
                detalhe:
                  'Ao colar, o card confere o arquivo e mostra o projeto que leu dele. Se não for uma conta de serviço — por exemplo, o google-services.json do app —, ele diz o que falta e não deixa salvar.',
              },
              {
                titulo: <>Marque “Envio de notificações ativo” e clique em <BotaoRef>Salvar</BotaoRef>.</>,
                detalhe: 'O servidor confere o arquivo de novo antes de gravar.',
              },
              { titulo: <>Clique em <BotaoRef variante="outline">Testar</BotaoRef>.</> },
            ]}
          />
          <Callout tipo="atencao" titulo="O arquivo é uma senha">
            Quem tem esse JSON pode mandar notificação a todos os celulares com o app. Não envie por
            WhatsApp nem e-mail; depois de colar, apague o arquivo baixado. Se ele vazar, apague a chave
            no console do Google Cloud (Contas de serviço → Chaves), gere outra e cole aqui.
          </Callout>
        </>
      ),
    },
    {
      id: 'firebase-testar-ativo',
      titulo: 'Firebase: Testar, Ativo e Limpar',
      busca:
        'testar credencial válida autenticou envio de validação validate_only não entrega nada firebase ativo desligar envio limpar apagar credencial projeto gravado apns chave da apple iphone outro projeto do firebase app gerado com outro projeto testar deu certo e não chega',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: <BotaoRef variante="outline">Testar</BotaoRef>,
                descricao:
                  'Confere a credencial gravada pelo mesmo caminho de um envio de verdade, sem mandar notificação a ninguém: entra no Google com ela e faz um envio de validação ao Firebase, que confere o projeto, se a API de envio está ligada e se a conta tem permissão de enviar — e não entrega nada. Testa o que está GRAVADO: com um JSON novo colado e não salvo, o botão fica desligado até você salvar. E só testa com o envio ativo: desligado, a resposta é que a credencial está desativada.',
              },
              {
                termo: 'Envio de notificações ativo',
                descricao:
                  'Desmarcado, a credencial continua gravada, mas nada sai: a ficha do paciente mostra que o envio não está configurado. É o jeito de suspender as notificações sem perder a chave.',
              },
              {
                termo: <BotaoRef variante="outline">Limpar</BotaoRef>,
                descricao:
                  'Apaga a credencial e desliga o envio. Volta a funcionar quando alguém colar o JSON de novo, marcar “Envio de notificações ativo” e salvar.',
              },
              {
                termo: 'Projeto gravado',
                descricao:
                  'O identificador do projeto do Firebase, tirado do JSON. Não é segredo; aparece para você conferir se a credencial é do projeto certo.',
              },
            ]}
          />
          <Sub>O que o Testar não alcança</Sub>
          <P>
            O envio de validação vai a um destino de teste, não a um celular. Por isso duas configurações
            só aparecem quando a notificação vai a um aparelho de verdade — com elas erradas, o Testar
            continua dando certo:
          </P>
          <Lista>
            <Item>
              <strong>A chave da Apple (APNs).</strong> O Firebase só entrega ao iPhone se o projeto tiver
              a chave de notificações da Apple, carregada no console do Firebase em Configurações do
              projeto → Cloud Messaging. Ela não passa por esta tela. Se faltar, o envio ao iPhone volta
              com “O Firebase recusou a chave da Apple (APNs)”.
            </Item>
            <Item>
              <strong>O projeto do app.</strong> O app do cidadão é gerado ligado a um projeto do
              Firebase, e a chave gravada aqui precisa ser desse <strong>mesmo</strong> projeto. Se for de
              outro, cada aparelho volta com “Este aparelho está ligado a outro projeto do Firebase” — e
              continua na lista do paciente, porque o celular não tem culpa. Confira com quem gera o app
              se o projeto dele é o mesmo do Projeto gravado; se não for, cole aqui a chave do projeto do
              app.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'firebase-testar-respostas',
      titulo: 'Firebase: o que o Testar responde',
      busca:
        'resposta do testar credencial válida o servidor autenticou firebase aceita envios api desligada firebase cloud messaging api v1 ativar google cloud apis e serviços conta de serviço sem permissão de envio papel firebase cloud messaging admin firebase admin sdk iam não encontrou o projeto json do projeto certo não respondeu agora http 503 429 tente de novo recusou o envio de teste google recusou a credencial credencial ilegível não pode ser lida por este servidor cifrada em outro ambiente banco copiado limpar colar o json de novo colar por cima sem permissão de exclusão gerar nova chave privada',
      conteudo: (
        <>
          <P>
            A resposta aparece no próprio card: em verde quando deu certo, em vermelho com o motivo quando
            não deu. No lugar do X entra o identificador do projeto.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: '“Credencial válida: o servidor autenticou e o Firebase aceita envios do projeto X.”',
                descricao:
                  'Tudo certo do lado do servidor: a chave entra no Google, a API de envio está ligada e a conta pode enviar. Lembre do que o Testar não alcança (seção acima).',
              },
              {
                termo: '“A API Firebase Cloud Messaging (V1) está desligada no projeto X.”',
                descricao:
                  'A chave é boa, mas o serviço de envio está desligado no projeto. No console do Google Cloud do mesmo projeto, em APIs e serviços, ative a “Firebase Cloud Messaging API” e teste de novo — pode levar alguns minutos para valer.',
              },
              {
                termo: '“A conta de serviço não tem permissão de envio no projeto X.”',
                descricao:
                  'A chave entra no Google, mas a conta não pode mandar notificação. Quem administra o projeto no Google Cloud dá a ela o papel Firebase Cloud Messaging Admin (ou Firebase Admin SDK), em IAM e administrador. A conta criada pela aba Contas de serviço do Firebase normalmente já vem com esse papel.',
              },
              {
                termo: '“O Firebase não encontrou o projeto X.”',
                descricao:
                  'O JSON gravado não é de um projeto do Firebase que exista. Confira se é a conta de serviço do projeto certo e cole de novo.',
              },
              {
                termo: '“O Firebase não respondeu agora.”',
                descricao:
                  'Instabilidade passageira do lado do Google (às vezes com o código HTTP entre parênteses). Não é a credencial: tente de novo em alguns minutos.',
              },
              {
                termo: '“O Firebase recusou o envio de teste do projeto X (HTTP …).”',
                descricao:
                  'Uma recusa que o sistema não sabe explicar. É raro; o detalhe técnico fica registrado no servidor para quem dá suporte à plataforma.',
              },
              {
                termo: '“O Google recusou a credencial: …”',
                descricao:
                  'A chave não entra no Google — em geral foi apagada ou desativada no console. Gere uma nova chave privada (passos na seção Firebase, acima) e cole aqui.',
              },
              {
                termo: '“A credencial gravada não pode ser lida por este servidor.”',
                descricao: 'Ver o quadro abaixo.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="Credencial que este servidor não consegue ler">
            A credencial é gravada cifrada com uma chave que pertence a cada servidor. Quando o banco vem
            de outro ambiente (uma cópia, uma troca de servidor), a credencial continua lá, o selo continua
            dizendo Configurado, mas este servidor não consegue abri-la — e responde “A credencial gravada
            não pode ser lida por este servidor (foi cifrada em outro ambiente). Use Limpar e cole o JSON
            de novo.” O envio pela ficha do paciente é barrado com a mesma frase. Para resolver, clique em{' '}
            <BotaoRef variante="outline">Limpar</BotaoRef>, cole o JSON da conta de serviço, marque “Envio
            de notificações ativo”, salve e teste. O Limpar pede a permissão de Exclusão; sem ela, colar o
            JSON por cima e salvar também resolve — o novo substitui o que não abre. Se o arquivo já foi
            apagado, como recomendado, gere uma nova chave privada pelos passos da seção Firebase.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil integrações credenciais consulta edição exclusão salvar testar limpar',
      conteudo: (
        <>
          <P>
            A tela é governada pelo módulo <strong>Integrações (credenciais)</strong> do perfil:
          </P>
          <Lista>
            <Item>
              <strong>Consulta</strong> — abrir a tela e ver o que está configurado;
            </Item>
            <Item>
              <strong>Edição</strong> — gravar credenciais, ligar e desligar e usar o Testar;
            </Item>
            <Item>
              <strong>Exclusão</strong> — limpar uma credencial.
            </Item>
          </Lista>
          <P>
            Mandar notificação a um paciente é outra permissão, na ficha dele: o módulo{' '}
            <strong>Notificações no app do cidadão</strong>. Configurar o Firebase não dá esse direito.
          </P>
        </>
      ),
    },
  ],
};
