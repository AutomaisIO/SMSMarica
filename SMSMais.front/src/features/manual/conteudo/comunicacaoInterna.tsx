import { MessagesSquare } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Passos } from '@/features/manual/components/Passos';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import { SELOS, SeloVisual } from '@/features/comunicacao-interna/lib/selos';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Comunicação interna (ADR-0079): o chat entre unidades e entre pessoas, no balão laranja.
 *
 * Conferido no código: `features/comunicacao-interna` (BalaoInterno, PainelInterno,
 * NovaConversaInterna, ThreadInterna, ComposerInterno, SeletorEmoji, AnexoInterno, useInternoHub,
 * selos.tsx) e o ADR-0079 (sessão única por par, participação derivada da unidade padrão — ADR-0081,
 * nada se apaga, anexo até 25 MB, presença = painel aberto, módulo ComunicacaoInterna).
 */
export const artigoComunicacaoInterna: Artigo = {
  slug: 'comunicacao-interna',
  titulo: 'Comunicação interna: conversar com outras unidades',
  resumo:
    'O chat do balão laranja: falar com todos de outra unidade ou com uma pessoa, ver quem está online, mandar arquivo, áudio, emoji e selo, e achar depois tudo o que foi combinado.',
  grupo: 'atendimento',
  icone: MessagesSquare,
  publico: 'Toda a equipe das unidades que usa o painel',
  atualizadoEm: '2026-10-23',
  palavrasChave: [
    'comunicação interna',
    'chat interno',
    'chat entre unidades',
    'conversar com outra unidade',
    'mensagem para unidade',
    'mensagem direta',
    'unidade padrão',
    'lotação',
    'não apareço na unidade',
    'balão laranja',
    'online',
    'quem está online',
    'presença',
    'anexo',
    'arquivo',
    'áudio',
    'gravar áudio',
    'microfone',
    'emoji',
    'selo',
    'figurinha',
    'urgente',
    'som',
    'silenciar',
    'notificação do Windows',
    'abrir sozinho',
    'janela separada',
    'buscar mensagem',
    'histórico',
    'digitando',
    'lida',
    'visto',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'chat interno unidades equipe whatsapp pessoal telefone registro balão laranja',
      conteudo: (
        <>
          <P>
            É o chat da equipe <strong>dentro do painel</strong>: a UPA fala com o hospital, a
            regulação com a policlínica, a recepção com o transporte. O que antes ia pelo WhatsApp
            pessoal ou pelo telefone fica registrado aqui, com nome, unidade e hora, e pode ser
            encontrado depois pela busca.
          </P>
          <P>
            Ele mora no <strong>balão laranja</strong>, no canto de baixo da tela, logo acima do balão
            da Central de Atendimento (o chat com os pacientes). Os dois não se misturam: o laranja é
            só entre colegas.
          </P>
        </>
      ),
    },
    {
      id: 'conversas',
      titulo: 'Os dois tipos de conversa',
      busca: 'todos da unidade pessoa direta equipe mesma conversa sessão participantes unidade padrão',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Todos da unidade',
                descricao:
                  'Conversa entre duas unidades. Participam todos que têm qualquer uma das duas como unidade padrão — quem está de plantão lê e responde, e quem chega no turno seguinte vê o que já foi combinado.',
              },
              {
                termo: 'Uma pessoa',
                descricao: 'Conversa só entre você e essa pessoa.',
              },
            ]}
          />
          <Lista>
            <Item>
              A conversa é <strong>uma só por par</strong>: escrever de novo para a mesma unidade (ou
              para a mesma pessoa) cai no mesmo histórico, não importa quem começou.
            </Item>
            <Item>
              Escolher a <strong>sua própria unidade</strong> em “Todos” abre o canal da sua equipe.
            </Item>
            <Item>
              Quem passa a ter a unidade como padrão passa a ver as conversas dela; quem troca de
              unidade padrão deixa de ver. Veja abaixo em qual unidade cada pessoa aparece.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'unidade-padrao',
      titulo: 'Em qual unidade cada pessoa aparece',
      busca:
        'unidade padrão lotação vínculo várias unidades aparece onde regulação técnico não apareço cadastro do usuário',
      conteudo: (
        <>
          <P>
            No chat, cada pessoa é de <strong>uma unidade só</strong>: a sua{' '}
            <strong>unidade padrão</strong> — onde ela está lotada. É ela que aparece na lista de
            pessoas, é por ela que a pessoa participa de “Todos da unidade” e é dela que saem as
            conversas que a pessoa abre.
          </P>
          <P>
            Muita gente é vinculada a várias unidades para <strong>enxergar os pedidos</strong> delas
            (quem trabalha na regulação, por exemplo, costuma ver todas). Esses vínculos continuam
            valendo para os dados, mas <strong>não contam no chat</strong> — senão a mesma pessoa
            apareceria na equipe de todas as unidades e receberia a conversa de todas elas.
          </P>
          <Callout tipo="dica" titulo="Não me acho na unidade certa">
            A unidade padrão é obrigatória e é definida no cadastro do usuário, na aba{' '}
            <strong>Unidades</strong>. Quem administra os usuários consegue listar, na tela de
            Usuários, quem ainda está <strong>sem unidade padrão</strong>. Depois de trocar, a pessoa
            recarrega a página (F5) para as mensagens novas chegarem na hora.
          </Callout>
        </>
      ),
    },
    {
      id: 'comecar',
      titulo: 'Começar uma conversa',
      busca: 'nova conversa escolher unidade pessoa online ponto verde de qual unidade falo',
      conteudo: (
        <>
          <Passos
            itens={[
              { titulo: 'Clique no balão laranja.' },
              { titulo: <>Clique em <BotaoRef>Nova</BotaoRef>.</> },
              {
                titulo: 'Escolha a unidade de destino.',
                detalhe: 'A lista mostra quantas pessoas cada unidade tem e quantas estão online agora.',
              },
              {
                titulo: 'Ao lado aparecem as pessoas da unidade: clique em uma, ou em “Todos da unidade”.',
                detalhe:
                  'O ponto verde quer dizer que a pessoa está com o painel aberto agora; o cinza, que não está.',
              },
            ]}
          />
          <P>
            Você fala sempre pela sua <strong>unidade padrão</strong>, não importa a unidade escolhida
            no topo do painel. Quem ainda não tem unidade padrão não consegue abrir “Todos da
            unidade” — a conversa com uma pessoa continua valendo; peça ao administrador para definir
            a sua.
          </P>
        </>
      ),
    },
    {
      id: 'escrever',
      titulo: 'Escrever, anexar, gravar áudio',
      busca:
        'enter shift enter quebrar linha anexar arquivo clipe colar print ctrl+v 25 MB áudio microfone gravar descartar',
      conteudo: (
        <>
          <Lista>
            <Item>
              <strong>Enter</strong> envia; <strong>Shift+Enter</strong> quebra a linha.
            </Item>
            <Item>
              O <strong>clipe</strong> anexa arquivos — vários de uma vez viram várias mensagens, e o
              texto que você escreveu vai junto do primeiro. Um print colado com <strong>Ctrl+V</strong>{' '}
              também vira anexo. Cada arquivo pode ter até <strong>25 MB</strong>.
            </Item>
            <Item>
              Com a caixa vazia, o botão da direita é o <strong>microfone</strong>: clique para gravar,
              clique na seta para enviar ou na lixeira para descartar. Na primeira vez o navegador
              pede licença para usar o microfone.
            </Item>
            <Item>
              Foto, áudio e PDF abrem na própria conversa. Outros tipos de arquivo aparecem com o
              botão <BotaoRef variante="outline">Baixar</BotaoRef>.
            </Item>
          </Lista>
          <Callout tipo="regra" titulo="Nada se apaga">
            Mensagem enviada não é editada nem excluída. É o que faz deste chat um registro: o que
            foi combinado entre as unidades continua lá para quem precisar conferir.
          </Callout>
        </>
      ),
    },
    {
      id: 'emojis-selos',
      titulo: 'Emojis e selos',
      busca: 'emoji carinha selo figurinha recebido ciente em análise aguardando urgente resolvido me liga paciente a caminho vaga',
      conteudo: (
        <>
          <P>
            A carinha ao lado do clipe abre os emojis — os mais usados, separados por tema, e uma aba
            de <strong>Trabalho e saúde</strong>. A última aba tem os <strong>selos</strong>: respostas
            de um clique que todo mundo reconhece de longe. O selo é enviado na hora:
          </P>
          <div className="my-3 flex flex-wrap gap-2">
            {SELOS.map((s) => (
              <SeloVisual key={s.chave} selo={s} />
            ))}
          </div>
          <P>Um selo também é achado pela busca, pelo nome dele (ex.: “urgente”).</P>
        </>
      ),
    },
    {
      id: 'avisos',
      titulo: 'Quando chega mensagem',
      busca:
        'aviso som plim abrir sozinho pula notificação do windows permissão título da aba contador silenciar conversa',
      conteudo: (
        <>
          <P>Mensagem de colega chama a atenção de quatro jeitos — todos ajustáveis no cabeçalho laranja:</P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'O painel pula',
                descricao:
                  'Com “abrir sozinho” ligado (o raio), o painel abre sozinho já na conversa que chegou, sem tirar o cursor do campo em que você está digitando. Desligado, só o balão balança.',
              },
              {
                termo: 'Som',
                descricao:
                  'Um “plim” de duas notas, diferente do bip da Central de Atendimento — dá para saber de ouvido se é colega ou paciente. O alto-falante liga e desliga.',
              },
              {
                termo: 'Notificação do Windows',
                descricao:
                  'Quando o painel está escondido atrás de outra janela. Na primeira vez, clique no sino com “+” para o navegador pedir a licença.',
              },
              {
                termo: 'Contador',
                descricao: 'O número no balão laranja e no título da aba (✉) são as mensagens ainda não lidas.',
              },
            ]}
          />
          <P>
            O sino no topo de cada conversa <strong>silencia aquela conversa</strong>: ela para de tocar,
            de abrir sozinha e de contar no balão, mas continua recebendo tudo. Som, “abrir sozinho” e
            silêncio ficam salvos no seu usuário — valem em qualquer computador.
          </P>
          <Callout tipo="atencao" titulo="O navegador não deixa trazer a janela para frente">
            Nenhum site consegue, sozinho, puxar o navegador para a frente de outro programa. Por isso a
            notificação do Windows é importante: libere-a para não perder mensagem com o painel atrás de
            outra janela.
          </Callout>
        </>
      ),
    },
    {
      id: 'ler',
      titulo: 'Online, digitando e lida',
      busca: 'online offline ponto verde digitando lida visto dois tracinhos azul participantes',
      conteudo: (
        <>
          <Lista>
            <Item>
              <strong>Online</strong> é quem está com o painel aberto agora, em qualquer tela. Na conversa
              de unidade, clique na linha de participantes para ver quem está.
            </Item>
            <Item>“Fulano está digitando…” aparece embaixo do nome da conversa.</Item>
            <Item>
              Na conversa com uma pessoa, os <strong>dois tracinhos azuis</strong> nas suas mensagens
              querem dizer que ela já leu.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'buscar',
      titulo: 'Achar o que foi combinado',
      busca: 'buscar busca pesquisar histórico mensagem antiga acento nome do arquivo',
      conteudo: (
        <>
          <P>
            A caixa <strong>Buscar nas conversas</strong>, no topo da lista, procura em todas as conversas
            de que você participa — no texto das mensagens e no nome dos arquivos — sem ligar para acento
            ou maiúscula. Clique num resultado para abrir a conversa já na mensagem, destacada.
          </P>
        </>
      ),
    },
    {
      id: 'janela',
      titulo: 'Em janela separada',
      busca: 'janela separada abrir em janela segundo monitor minimizar',
      conteudo: (
        <P>
          O botão de janela no cabeçalho abre a Comunicação interna numa janela própria do navegador
          (bom para deixar num segundo monitor). Com ela aberta, é ela quem toca o som e mostra os
          avisos; o painel do canto pode ficar fechado.
        </P>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo comunicação interna consulta inclusão ler enviar privacidade lgpd',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Ver o balão e ler as conversas de que participa.' },
              { termo: 'Inclusão', descricao: 'Enviar mensagem, arquivo, áudio e selo.' },
            ]}
          />
          <P>
            As duas vêm do módulo <strong>Comunicação interna</strong>, na tela de Perfis. Ninguém lê
            conversa de que não participa — nem o administrador.
          </P>
          <Callout tipo="lgpd" titulo="Dado de paciente só o necessário">
            A conversa fica guardada e pode ser lida por toda a equipe das duas unidades. Escreva do
            paciente só o que o colega precisa para resolver o caso.
          </Callout>
        </>
      ),
    },
  ],
};
