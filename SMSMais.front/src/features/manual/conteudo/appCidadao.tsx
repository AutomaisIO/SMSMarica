import { Smartphone } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Passos } from '@/features/manual/components/Passos';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Sistema → App do cidadão (ADR-0078, 22/10/2026).
 *
 * Conferido no código: `features/app-cidadao/pages/AppCidadaoPage` (dois canais, aviso de conta do
 * e-SUS ausente, Salvar, histórico) contra `ConfiguracaoAppCidadaoService` (só acesso global, trilha
 * de auditoria `ConfiguracaoAppCidadao`, padrão tudo desligado) e a entrada em `PacienteAuthService`
 * (CPF + nascimento, 5 erros em 30 min = 30 min de bloqueio, verificado sozinho na lista, código só para
 * número da lista, até 3 códigos por entrada) e `TelefonesEsusNaEntrada` (conta da plataforma, uma por
 * vez, sem forçar, fora da janela da madrugada, pausa de 30 min após login recusado, 20 min de guarda).
 */
export const artigoAppCidadao: Artigo = {
  slug: 'app-cidadao',
  titulo: 'App do cidadão — entrada',
  resumo:
    'Como o paciente entra no PWA e no app nativo (CPF, data de nascimento e um dos celulares do cadastro) e quando o app procura outros números no e-SUS — configurado por canal, só por admin global.',
  grupo: 'sistema',
  icone: Smartphone,
  rota: '/app/app-cidadao',
  publico: 'Administradores com acesso global',
  atualizadoEm: '2026-10-22',
  palavrasChave: [
    'app do cidadão',
    'aplicativo',
    'PWA',
    'app nativo',
    'entrada',
    'login',
    'entrar no app',
    'CPF',
    'data de nascimento',
    'código',
    'WhatsApp',
    'número do cadastro',
    'nenhum desses é meu',
    'e-SUS',
    'e-SUS PEC',
    'consultar o e-SUS',
    'procure o posto',
    'bloqueado',
    'muitas tentativas',
    'nº da solicitação',
    'acesso global',
    'histórico de ajustes',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve esta tela',
      busca: 'configuração app do cidadão PWA nativo canal entrada e-SUS acesso global',
      conteudo: (
        <>
          <P>
            Aqui se decide como o paciente entra no aplicativo do cidadão. São dois canais, configurados em
            separado: o <strong>PWA</strong>, que é o app aberto pelo navegador (inclusive com o atalho na tela
            inicial do celular), e o <strong>app nativo</strong>, instalado pela loja.
          </P>
          <P>
            Hoje a configuração de cada canal é uma só: se, quando nenhum número do cadastro é do paciente, o app
            procura outros números no e-SUS. Ela nasce desligada nos dois.
          </P>
        </>
      ),
    },
    {
      id: 'como-o-paciente-entra',
      titulo: 'Como o paciente entra',
      busca:
        'entrar CPF data de nascimento escolher número final WhatsApp código nenhum desses é meu posto verificado bloqueio nº da solicitação',
      conteudo: (
        <>
          <Passos
            itens={[
              { titulo: 'Digita o CPF e a data de nascimento.', detalhe: 'Nada mais: o nº da solicitação não é mais pedido.' },
              {
                titulo: 'Escolhe para qual celular vai o código.',
                detalhe:
                  'O app mostra só o final de cada número do cadastro (“WhatsApp final 1234”). Quem já tem o WhatsApp confirmado vê só esse.',
              },
              {
                titulo: 'Se nenhum é dele, toca em “Nenhum desses é meu”.',
                detalhe:
                  'Com a consulta ao e-SUS ligada no canal, o app procura outros números lá. Sem nenhum número, a orientação é procurar o posto.',
              },
              { titulo: 'Recebe o código no WhatsApp escolhido e entra.', detalhe: 'Esse número passa a ser o WhatsApp confirmado dele.' },
            ]}
          />
          <Callout tipo="regra" titulo="O código nunca vai para um número digitado">
            O paciente não digita número nenhum: o código só vai para um celular que já estava no cadastro (ou no
            e-SUS). É o que impede alguém que saiba o CPF e a data de nascimento de outra pessoa de pôr o próprio
            celular no lugar e ver o prontuário dela. Quem já tem o WhatsApp confirmado só troca de número no posto.
          </Callout>
          <Lista>
            <Item>
              CPF e data de nascimento são conferidos antes de qualquer número aparecer: contra a ficha e, se a ficha
              não tem a data (ou não existe), na Receita. Errou, a mensagem é sempre a mesma, para não ajudar quem tenta
              adivinhar.
            </Item>
            <Item>
              <strong>5 erros em 30 minutos</strong> no mesmo CPF bloqueiam a entrada desse CPF por 30 minutos.
            </Item>
            <Item>Cada entrada pode pedir até 3 códigos; depois, é preciso começar de novo.</Item>
            <Item>
              Sem cadastro, o paciente só entra se o e-SUS (ligado no canal) tiver um celular dele; o cadastro nasce
              quando ele confirma o código, com o nome da Receita.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'consulta-esus',
      titulo: 'A consulta ao e-SUS',
      busca: 'e-SUS PEC consulta conta da plataforma fontes de dados uma por vez derrubar madrugada indisponível segundos',
      conteudo: (
        <>
          <P>
            Ligada num canal, a busca usa a <strong>conta da plataforma</strong> no e-SUS PEC, a mesma de Sistema →
            Fontes de dados. Se ela não estiver gravada ou estiver desligada lá, esta tela avisa e o app não oferece a
            busca.
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Uma por vez', descricao: 'O e-SUS só aceita uma sessão por conta; os pedidos entram na fila e esperam alguns segundos.' },
              {
                termo: 'Sem derrubar ninguém',
                descricao: 'Se a conta já estiver em uso, o app diz “tente de novo em instantes” em vez de forçar a entrada.',
              },
              {
                termo: 'Madrugada',
                descricao: 'Na janela da rotina da madrugada (a que corrige telefones pelo e-SUS), a busca não é feita.',
              },
              {
                termo: 'Senha recusada',
                descricao:
                  'Se o e-SUS recusar a senha da conta, a busca para por 30 minutos — tentar de novo com senha errada bloquearia a conta lá.',
              },
              {
                termo: 'Mesma pessoa',
                descricao:
                  'Só vale o cadastro do e-SUS com a mesma data de nascimento, sem óbito e ativo. Só celulares entram na lista.',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'historico',
      titulo: 'Histórico de ajustes',
      busca: 'histórico ajustes quem mudou quando auditoria',
      conteudo: (
        <P>
          Cada <BotaoRef>Salvar</BotaoRef> que muda alguma coisa fica registrado: quando, quem e o que mudou (por
          exemplo, “App nativo: consulta ao e-SUS desligada → ligada”). O mesmo registro aparece em Sistema →
          Auditoria. Salvar sem mudar nada não cria registro.
        </P>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão acesso global administrador',
      conteudo: (
        <P>
          A tela é só de quem tem <strong>acesso global</strong> no cadastro de usuário. Para os demais ela nem aparece
          no menu, e o servidor recusa a leitura e a gravação mesmo pelo endereço direto.
        </P>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'paciente não recebe código número errado bloqueado nenhum número posto',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'O paciente diz que nenhum número da lista é dele.',
              descricao:
                'Se o e-SUS estiver ligado no canal, ele pode tocar em “Nenhum desses é meu” e procurar lá. Sem número em lugar nenhum, a recepção atualiza o WhatsApp dele no cadastro.',
            },
            {
              termo: 'O paciente diz que está “bloqueado”.',
              descricao: 'Errou a data de nascimento 5 vezes: espera 30 minutos e tenta de novo, com a data certa.',
            },
            {
              termo: 'O número confirmado dele mudou.',
              descricao: 'A troca do WhatsApp confirmado é no posto, com documento com foto.',
            },
          ]}
        />
      ),
    },
  ],
};
