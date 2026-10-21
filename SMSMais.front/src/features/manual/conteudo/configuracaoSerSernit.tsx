import { Settings2 } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo, SecaoArtigo } from '@/features/manual/tipos';

/**
 * Artigos da Configuração do SER e da Configuração do SERNIT (21/10/2026). As duas abas são a
 * mesma tela com o nome trocado, então as seções saem de uma função só; o SER tem, além dela, as
 * abas de consulta direta, catálogo e solicitações.
 *
 * Conferido no código em 21/10/2026: `features/ser/pages/RegulacaoConfiguracaoPage`,
 * `features/ser/components/AbaSerConfiguracao` e `AbaSerConsultaDireta`,
 * `features/sernit/pages/RegulacaoSernitConfiguracaoPage`, `features/sernit/components/AbaSernitConfiguracao`,
 * `features/regulacao/components/configuracao/AvisosPacienteSecao`; no servidor, `SerConfiguracaoController`,
 * `SernitConfiguracaoController`, `SerSincronizacaoService` (status das rodadas), `VarreduraSerRunner`
 * (retomada na subida), `AgendamentoExternoService` e `LembreteAgendamentoExternoService`.
 */

type Sistema = 'SER' | 'SERNIT';

function secoesDoMotor(sistema: Sistema): SecaoArtigo[] {
  return [
    {
      id: 'estado',
      titulo: 'Os quatro números do topo',
      busca: 'solicitações espelhadas eventos de histórico gatilhos na fila credencial ok falta estado do motor',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Solicitações espelhadas',
              descricao: `Quantos pedidos do ${sistema} a plataforma já copiou, em todas as situações (em fila, pendente, agendada, chegada, cancelada, alta).`,
            },
            {
              termo: 'Eventos de histórico',
              descricao: 'As linhas do histórico de cada pedido (agendar, devolver, follow-up, cancelar…) já lidas.',
            },
            {
              termo: 'Gatilhos na fila',
              descricao: 'Mudanças de situação que ainda esperam alguém olhar em Notificações.',
            },
            {
              termo: `Credencial do ${sistema}`,
              descricao: '“OK” quando há usuário e senha salvos; “Falta” quando o motor ainda não tem com que entrar.',
            },
          ]}
        />
      ),
    },
    {
      id: 'motor',
      titulo: 'Rodar o motor à mão',
      busca: 'motor de atualização modo da rodada carga inicial diária só a grade histórico rodar agora segundo plano 15 minutos 1 hora',
      conteudo: (
        <>
          <P>
            O motor entra no {sistema} com a credencial salva, lê a grade de cada situação e o histórico dos
            pedidos, e grava a cópia na plataforma. Ele <strong>só lê</strong>: nada é escrito no {sistema}.
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Carga inicial', descricao: 'Tudo, com o histórico de todos os pedidos. Para a primeira vez, ou para refazer a base.' },
              {
                termo: 'Diária',
                descricao:
                  'Compara a grade com o que já temos e relê o histórico de quem mudou de situação e de toda a fila. É o modo da rodada automática.',
              },
              { termo: 'Só a grade', descricao: 'Atualiza as situações sem ler histórico — a rodada mais rápida.' },
            ]}
          />
          <P>
            Escolha o modo e clique em <BotaoRef>Rodar agora</BotaoRef>. A rodada vai para a fila e roda em segundo
            plano, de cerca de 15 minutos (só a grade) a cerca de 1 hora (com o histórico de toda a fila). O botão
            fica desligado enquanto outra rodada está em andamento ou sem credencial.
          </P>
          <Callout tipo="atencao" titulo="A sessão do motor é a do usuário da credencial">
            Enquanto o motor roda, quem estiver logado no {sistema} com o mesmo usuário pode cair. Por isso a
            rodada automática é de madrugada.
          </Callout>
        </>
      ),
    },
    {
      id: 'catalogo',
      titulo: `Copiar o catálogo do ${sistema}`,
      busca: 'catálogo copiar recursos campos cid lista nova solicitação offline uma vez por dia nome renumera',
      conteudo: (
        <>
          <P>
            A cópia do catálogo (recursos, campos de cada formulário e a lista de CID) é o que deixa a tela de nova
            solicitação funcionar sem consultar o {sistema} a cada clique. Ela se atualiza sozinha uma vez por dia (de
            madrugada, ou na primeira solicitação aberta no dia). O botão{' '}
            <BotaoRef variante="outline">Copiar catálogo do {sistema}</BotaoRef> é para não esperar.
          </P>
          <P>
            Na primeira vez leva uns 10 minutos (uma ida ao {sistema} por recurso); depois, só o que for novo. O
            recurso é reconhecido pelo <strong>nome</strong>: quando o {sistema} renumera a lista, nada troca de
            lugar.
          </P>
        </>
      ),
    },
    {
      id: 'rodada-diaria',
      titulo: 'A rodada diária automática',
      busca: 'rodada diária automática horário brasília 02:30 salvar horário ligar desligar',
      conteudo: (
        <P>
          Marque <strong>Rodar a varredura diária automaticamente</strong> e escolha o horário de Brasília (o padrão é
          02:30); a hora nova vale ao clicar em <BotaoRef variante="outline">Salvar horário</BotaoRef>. A automática é
          sempre no modo Diária. A carga inicial continua sendo disparo manual.
        </P>
      ),
    },
    {
      id: 'anexos',
      titulo: 'Anexos no envio',
      busca: 'anexos tamanho máximo por arquivo mb limite comprimir compressão foto pdf 150 dpi 2 arquivos 4,5 mb salvar limite',
      conteudo: (
        <>
          <P>
            No bloco <strong>Anexos no envio ao {sistema}</strong> fica o tamanho máximo por arquivo, em MB, como o
            Windows mostra. Clique em <BotaoRef variante="outline">Salvar limite</BotaoRef> para gravar. Sem nada
            configurado vale o padrão de 4,5 MB: a tela do {sistema} declara 5 MB, mas nunca se mediu como o servidor
            de lá conta.
          </P>
          <Lista>
            <Item>O {sistema} aceita no máximo 2 arquivos por solicitação; com mais, eles vão juntos num PDF.</Item>
            <Item>
              Arquivo acima do limite é comprimido no envio: a foto é reduzida, e o PDF tem as imagens regravadas em
              resolução menor, nunca abaixo de 150 dpi. Só se nem assim couber, o envio é recusado.
            </Item>
            <Item>O original continua guardado na plataforma.</Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'avisos-paciente',
      titulo: 'Avisos ao paciente: lembrete e cancelamento',
      busca:
        'avisos ao paciente lembrete dias antes aviso de cancelamento cancelado devolvido para a fila voltou para a fila desmarcado whatsapp carência 30 minutos pendência ligar vale daqui para frente',
      conteudo: (
        <>
          <P>
            O aviso de que o pedido foi <em>agendado</em> se liga em Mensageria → Regras e vale para SER, SERNIT e ESUS
            juntos — o bloco mostra se ele está ligado. Aqui ficam as duas mensagens que são só do {sistema}, e as duas
            nascem <strong>desligadas</strong>:
          </P>
          <Sub>Enviar lembrete antes do agendamento</Sub>
          <Lista>
            <Item>Sai os dias antes que você escolher (de 1 a 30).</Item>
            <Item>
              Quem já <strong>confirmou</strong> recebe o lembrete com o dia e a hora. Quem <strong>não
              respondeu</strong> recebe a primeira mensagem de novo — o que falta a essa pessoa é entrar na conversa.
            </Item>
            <Item>
              Quem avisou que <strong>não vai</strong> não recebe nada, e quem recebeu o aviso há menos de 7 dias
              também não.
            </Item>
          </Lista>
          <Sub>Avisar o paciente quando o agendamento for cancelado</Sub>
          <Passos
            itens={[
              {
                titulo: `O ${sistema} mostra o pedido cancelado ou devolvido para a fila, com a data ainda pela frente`,
                detalhe:
                  'A leitura do motor traz a mudança. Pendência não conta: às vezes o pedido volta com o mesmo dia.',
              },
              {
                titulo: 'O aviso espera 30 minutos',
                detalhe:
                  'Se nesse tempo o pedido voltar a Agendada com o mesmo dia, o aviso não sai. Se já tinha saído, o paciente recebe o aviso do agendamento de novo.',
              },
              {
                titulo: 'Sai a mensagem de cancelamento do SISREG',
                detalhe:
                  'Anônima para número não confirmado (“sua consulta foi cancelada”). Para o número confirmado, com o procedimento e a data. Quando o pedido voltou para a fila, a mensagem diz que o agendamento foi desmarcado e o pedido voltou para a fila da regulação.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="Ligar vale daqui para frente">
            O que foi desmarcado antes de ligar o aviso não é avisado. A mensagem respeita o horário de envio de
            Mensageria → Regras.
          </Callout>
          <P>
            Remarcação não depende destas chaves: a data nova sempre gera um aviso novo — inclusive para o pedido que
            o paciente não conseguiu fazer e a regulação devolveu para a fila e agendou de novo.
          </P>
        </>
      ),
    },
    {
      id: 'credencial',
      titulo: 'Credencial',
      busca: 'credencial usuário senha testar salvar cifrada write-only módulo ambulatório',
      conteudo: (
        <P>
          Preencha usuário e senha. <BotaoRef variante="outline">Testar</BotaoRef> só autentica e confere se o
          módulo Ambulatório abre, sem gravar nada. <BotaoRef>Salvar</BotaoRef> testa primeiro e só então guarda a
          credencial, cifrada: credencial que não funciona faria o motor falhar de madrugada, sem ninguém por perto. A
          senha não volta para a tela depois de salva. Trocar a credencial não mexe no horário da rodada diária nem
          no limite de anexo.
        </P>
      ),
    },
    {
      id: 'rodadas',
      titulo: 'Últimas rodadas',
      busca:
        'últimas rodadas status concluída parcial erro cancelada interrompida retomada fatias truncadas cobertura progresso sinal duração',
      conteudo: (
        <>
          <P>
            A tabela mostra as últimas 20 rodadas: quantos pedidos foram vistos, quantos eram novos e quantos mudaram de
            situação; quanto histórico foi lido (com os FollowUP, as tentativas de contato com o paciente, em roxo); a
            cobertura; o progresso e a duração.
          </P>
          <ListaDefinicoes
            itens={[
              { termo: <SeloRef cor="sucesso">Concluida</SeloRef>, descricao: 'Leu tudo.' },
              {
                termo: <SeloRef cor="alerta">Parcial</SeloRef>,
                descricao:
                  'Alguma fatia veio cortada (a coluna Cobertura diz quantas): o que foi lido vale; o que faltou entra na próxima rodada.',
              },
              { termo: <SeloRef cor="erro">Erro</SeloRef>, descricao: 'A rodada parou com um erro (credencial recusada, por exemplo).' },
              {
                termo: <SeloRef cor="violeta">Interrompida</SeloRef>,
                descricao:
                  'O serviço reiniciou no meio (um deploy, por exemplo). Ela retoma sozinha de onde parou — a coluna Progresso mostra a fase e quantas vezes foi retomada.',
              },
              {
                termo: <SeloRef cor="alerta">Cancelada</SeloRef>,
                descricao: 'A rodada foi encerrada no meio e não volta; a próxima rodada lê tudo de novo.',
              },
            ]}
          />
          <P>
            Numa rodada em andamento, “sinal há …” em verde quer dizer que ela está andando; em vermelho (mais de 2
            minutos sem gravar nada), vá olhar.
          </P>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão módulo regulação configuração consulta edição',
      conteudo: (
        <P>
          A tela exige o módulo <strong>Regulação — Configuração</strong>. Para salvar os avisos ao paciente é preciso
          a mesma permissão com <strong>edição</strong>.
        </P>
      ),
    },
  ];
}

const palavrasComuns = [
  'configuração',
  'credencial',
  'motor',
  'varredura',
  'rodar agora',
  'carga inicial',
  'diária',
  'rodada automática',
  'horário',
  'catálogo',
  'últimas rodadas',
  'parcial',
  'interrompida',
  'cancelada',
  'anexos',
  'tamanho máximo',
  'avisos ao paciente',
  'lembrete',
  'dias antes',
  'aviso de cancelamento',
  'voltou para a fila',
  'devolvido para a fila',
];

export const artigoSerConfiguracao: Artigo = {
  slug: 'ser-configuracao',
  titulo: 'Configuração do SER',
  resumo:
    'O motor que copia os pedidos do SER (Estado), a credencial, a rodada diária, o catálogo, o limite de anexo e os avisos ao paciente — lembrete e cancelamento.',
  grupo: 'regulacao',
  icone: Settings2,
  rota: '/app/regulacao/configuracao',
  publico: 'Quem cuida da integração com o SER e decide o que o paciente recebe',
  atualizadoEm: '2026-10-21',
  palavrasChave: ['SER', 'SES-RJ', 'Estado', 'consulta direta', 'catálogo de procedimentos', ...palavrasComuns],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O que é esta tela',
      busca: 'configuração da regulação abas ser consulta direta catálogo de procedimentos solicitações',
      conteudo: (
        <>
          <P>
            A Configuração da Regulação tem quatro abas. Este artigo é sobre a primeira, <strong>SER</strong>: o motor
            que mantém na plataforma a cópia dos pedidos do SER, e o que o paciente recebe sobre esses agendamentos.
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'SER', descricao: 'Estado do motor, rodada manual e diária, catálogo, anexos, avisos ao paciente, credencial e últimas rodadas.' },
              {
                termo: 'SER — consulta direta',
                descricao:
                  'Consulta o SER ao vivo, com os filtros da tela de lá, e mostra o resultado cru. Nada é gravado: é o ensaio que valida o motor em segundos. Cuidado: derruba a sessão de quem estiver logado no SER com a mesma credencial.',
              },
              {
                termo: 'Catálogo de procedimentos',
                descricao: 'A curadoria do catálogo canônico da regulação (o mesmo procedimento no SISREG, no SER e no SERNIT).',
              },
              {
                termo: 'Solicitações',
                descricao:
                  'As regras do fluxo de solicitação da regulação: prazos, busca, regras de elegibilidade, anexos e as regras de follow-up do SER e do SERNIT.',
              },
            ]}
          />
        </>
      ),
    },
    ...secoesDoMotor('SER'),
  ],
};

export const artigoSernitConfiguracao: Artigo = {
  slug: 'sernit-configuracao',
  titulo: 'Configuração do SERNIT',
  resumo:
    'O motor que copia os pedidos do SERNIT (Niterói), a credencial, a rodada diária, o catálogo, o limite de anexo e os avisos ao paciente — lembrete e cancelamento.',
  grupo: 'regulacao',
  icone: Settings2,
  rota: '/app/regulacao/sernit/configuracao',
  publico: 'Quem cuida da integração com o SERNIT e decide o que o paciente recebe',
  atualizadoEm: '2026-10-21',
  palavrasChave: ['SERNIT', 'Niterói', ...palavrasComuns],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O que é esta tela',
      busca: 'configuração sernit niterói motor credencial',
      conteudo: (
        <P>
          A tela do SERNIT (a regulação de Niterói) mantém na plataforma a cópia dos pedidos do município no SERNIT e
          decide o que o paciente recebe sobre esses agendamentos. É a mesma tela do SER, com o próprio motor, a
          própria credencial e as próprias chaves de aviso.
        </P>
      ),
    },
    ...secoesDoMotor('SERNIT'),
  ],
};
