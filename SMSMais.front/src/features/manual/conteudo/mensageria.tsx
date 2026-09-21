import { BellRing } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import { Passos } from '@/features/manual/components/Passos';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo da Mensageria — a tela de quem OLHA o conjunto dos envios, não a de quem atende caso a
 * caso (essa é Confirmações, e a distinção abre o artigo de propósito: é a dúvida nº 1 de quem
 * chega aqui e tenta "resolver" um paciente).
 *
 * A ordem segue a dúvida de quem aprende: o que é esta tela e o que NÃO é → como se lê o painel
 * do dia → onde se olha um envio específico → o que o paciente respondeu → como se dispara um
 * lote → as regras que governam tudo isso → quanto custa → quem pode o quê.
 */
export const artigoMensageria: Artigo = {
  slug: 'mensageria',
  titulo: 'Mensageria',
  resumo:
    'A gestão dos envios de WhatsApp ao paciente: o que saiu, chegou, falhou e por quê; o disparo em lote; e as regras que governam todo o automático.',
  grupo: 'atendimento',
  icone: BellRing,
  rota: '/app/mensageria',
  publico: 'Quem responde pelo canal: coordenação, regulação e quem configura o envio automático',
  atualizadoEm: '2026-09-21',
  palavrasChave: [
    'mensageria',
    'whatsapp',
    'envios',
    'entrega',
    'falha',
    'reenviar',
    'disparar lote',
    'lote',
    'resumo diário',
    'respostas dos pacientes',
    'regras e parâmetros',
    'testar modelo',
    'template',
    'modelo aprovado',
    'tarifas meta',
    'custo',
    'utility',
    'marketing',
    'authentication',
    'janela de envio',
    'lembrete',
    'conciliação',
    'cadência',
    'janela de leitura',
    'fechamento do dia anterior',
    'cancelamentos do sisreg',
    'taxa de entrega',
    'taxa de leitura',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve esta tela (e para que não serve)',
      busca: 'o que é mensageria diferença confirmações gestor atendente para que serve',
      conteudo: (
        <div className="space-y-4">
          <P>
            A Mensageria é a tela de quem olha o <strong>conjunto</strong>: quantas mensagens saíram hoje, quantas
            chegaram, quantas falharam e por quê, o que os pacientes responderam, e quais regras governam todo o
            automático. É a tela de quem responde pelo canal.
          </P>
          <P>
            Ela <strong>não</strong> é a tela de trabalhar paciente por paciente. Ligar para alguém, confirmar,
            mandar para pendente, marcar contato errado, cancelar agendamento — tudo isso é do menu{' '}
            <strong>Confirmações</strong>, que tem as filas e a posse da ficha. Aqui não existe fila, não existe
            "assumir" e não se dá desfecho.
          </P>
          <Callout tipo="dica" titulo="A regra para não se perder entre as duas telas">
            Se a pergunta começa com <em>"esta pessoa…"</em>, é Confirmações. Se começa com <em>"quantas…"</em>,{' '}
            <em>"por que tantas…"</em> ou <em>"a partir de amanhã, todas…"</em>, é Mensageria.
          </Callout>
          <Sub>As seis abas</Sub>
          <ListaDefinicoes
            itens={[
              {
                termo: <AbaRef>Resumo diário</AbaRef>,
                descricao:
                  'O painel do período: entraram, saíram, chegaram, falharam, responderam — com taxas e a quebra por dia, por erro, por finalidade e por unidade. Só leitura.',
              },
              {
                termo: <AbaRef>Envios</AbaRef>,
                descricao:
                  'Uma linha por mensagem. É onde se procura um envio específico (por nº SISREG, accession ou telefone) e se abre a linha do tempo dele — até o erro que a Meta devolveu.',
              },
              {
                termo: <AbaRef>Respostas dos pacientes</AbaRef>,
                descricao:
                  'Quem respondeu, o que respondeu, por qual caminho e com que motivo. Abre filtrada em quem não vai — que é a resposta que exige ação.',
              },
              {
                termo: <AbaRef>Disparar lote</AbaRef>,
                descricao:
                  'O aviso nasce na importação; quem já estava agendado antes não recebeu. Esta aba cobre esse estoque, com prévia antes de disparar.',
              },
              {
                termo: <AbaRef>Regras e parâmetros</AbaRef>,
                descricao:
                  'O horário em que o automático pode falar, a vazão, o lembrete, a leitura dos cancelamentos feitos no SISREG, quais unidades avisam e as tarifas da Meta.',
              },
              {
                termo: <AbaRef>Testar modelo</AbaRef>,
                descricao:
                  'Manda um modelo aprovado para o seu próprio celular, do jeito que o paciente receberia. Não cria comunicação nem mexe em agendamento.',
              },
            ]}
          />
          <P>
            A aba escolhida fica no endereço (<code>?aba=envios</code>), então dá para guardar o link de uma aba
            específica ou mandá-lo para alguém.
          </P>
        </div>
      ),
    },
    {
      id: 'resumo-diario',
      titulo: 'Resumo diário: como ler os números sem se enganar',
      busca: 'resumo diário painel entraram saíram taxa de entrega leitura resposta coorte falhas por erro',
      conteudo: (
        <div className="space-y-4">
          <P>
            O período padrão são os últimos 30 dias, e dá para filtrar por finalidade (confirmação de agendamento,
            exame liberado, laudo pronto) e por unidade executante.
          </P>
          <Callout tipo="atencao" titulo="A confusão que todo mundo comete aqui">
            <strong>Entraram</strong> e <strong>Saíram no dia</strong> não são a mesma conta e quase nunca batem.
            "Entraram" conta as mensagens <em>criadas</em> naquele dia — é a turma do dia. "Saíram no dia" conta as
            que <em>foram enviadas</em> naquele dia, não importa de que turma vieram. Uma mensagem criada no sábado
            à noite sai na segunda de manhã: ela entra no sábado e sai na segunda. Comparar as duas colunas lado a
            lado como se fossem a mesma coisa é o erro mais comum de leitura desta tela.
          </Callout>
          <Sub>O que cada número está dizendo</Sub>
          <ListaDefinicoes
            itens={[
              { termo: 'Enviadas / Entregues / Lidas', descricao: 'O funil da entrega: saiu daqui, chegou no aparelho, a pessoa abriu.' },
              {
                termo: 'Retidas (identif./negado/sem celular)',
                descricao:
                  'Não falharam — estão seguradas de propósito: o número não é verificado e recebeu o desafio de identificação, ou alguém já disse que o número não é do paciente, ou o cadastro não tem celular. Retida alta é problema de cadastro, não de canal.',
              },
              {
                termo: 'Atendidas por pessoa',
                descricao:
                  'Uma atendente entrou no circuito pelo menu Confirmações antes de a mensagem sair, e o automático se calou. Isso é o sistema funcionando, não perda.',
              },
              { termo: 'Confirmaram / Não vão / Sem resposta', descricao: 'O desfecho do ponto de vista do paciente.' },
              {
                termo: 'Taxa de entrega / leitura / resposta',
                descricao:
                  'As três taxas do período. A de entrega caindo aponta para número ruim no cadastro; a de leitura caindo, para arte ou horário; a de resposta caindo, para o texto.',
              },
            ]}
          />
          <P>
            Ao lado, três listas dizem <strong>onde</strong> está o problema: <em>Falhas por erro (Meta)</em>,{' '}
            <em>Por finalidade</em> e <em>Por unidade executante</em>. É por onde se começa quando a taxa de entrega
            cai: quase sempre o erro se concentra num código só ou numa unidade só.
          </P>
        </div>
      ),
    },
    {
      id: 'envios',
      titulo: 'Envios: achar uma mensagem e entender o que houve com ela',
      busca: 'envios buscar sisreg accession telefone selo status detalhe linha do tempo reenviar erro meta magic link',
      conteudo: (
        <div className="space-y-4">
          <P>
            A busca aceita <strong>nº SISREG, accession ou telefone</strong>. Clicando no nome do paciente abre o
            detalhe: a linha do tempo do envio (criada, tentativas, enviada, entregue, lida, próxima tentativa), o
            conteúdo exato que foi mandado, o erro que a Meta devolveu quando houve, a resposta do paciente e o
            estado do link de acesso.
          </P>
          <Sub>Os selos da coluna Envio</Sub>
          <ListaDefinicoes
            itens={[
              { termo: <SeloRef cor="gray">Na fila</SeloRef>, descricao: 'Ainda não saiu. Fora da janela de horário, espera a janela abrir.' },
              { termo: <SeloRef cor="info">Enviada</SeloRef>, descricao: 'Saiu daqui e foi aceita pela Meta.' },
              { termo: <SeloRef cor="info">Entregue</SeloRef>, descricao: 'Chegou no aparelho do paciente.' },
              { termo: <SeloRef cor="sucesso">Lida</SeloRef>, descricao: 'A pessoa abriu. Leu e não respondeu é um bom motivo para ligar.' },
              {
                termo: <SeloRef cor="erro">Falha</SeloRef>,
                descricao: 'A entrega foi recusada. O motivo aparece na dica do selo e, inteiro, no detalhe, como "Erro de entrega (Meta)".',
              },
              { termo: <SeloRef cor="alerta">Sem celular válido</SeloRef>, descricao: 'O cadastro não tem um celular que sirva. Resolve-se no cadastro do paciente, não aqui.' },
              {
                termo: <SeloRef cor="alerta">Aguardando contato verificado</SeloRef>,
                descricao: 'Resultado e laudo só vão para contato verificado. Sai sozinha no dia em que verificarem o telefone.',
              },
              {
                termo: <SeloRef cor="alerta">Aguardando o paciente se identificar</SeloRef>,
                descricao:
                  'O número não é verificado, então em vez dos dados foi enviado o desafio (início do CPF, mês e ano de nascimento). Responde certo, recebe o conteúdo.',
              },
              {
                termo: <SeloRef cor="erro">Número inválido (não é do paciente)</SeloRef>,
                descricao: 'Quem atende disse que não conhece o paciente. Nada automático sai mais para esse número até alguém corrigir e verificar o cadastro.',
              },
              {
                termo: <SeloRef cor="gray">Atendida por pessoa</SeloRef>,
                descricao: 'Terminal: uma atendente assumiu a ficha em Confirmações antes de a mensagem sair.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="Reenviar não é o conserto de tudo">
            O <BotaoRef>Reenviar</BotaoRef> do detalhe manda a mesma mensagem de novo. Ele resolve falha momentânea
            de entrega — não resolve número errado, cadastro sem celular nem número marcado como inválido. Nesses
            três casos reenviar só gasta mensagem: o caminho é arrumar o contato do paciente.
          </Callout>
        </div>
      ),
    },
    {
      id: 'respostas',
      titulo: 'Respostas dos pacientes: quem disse que não vai',
      busca: 'respostas pacientes cancelaram confirmaram motivo canal link botões robô app presencial atendente',
      conteudo: (
        <div className="space-y-4">
          <P>
            A aba abre já filtrada em <strong>Não vão (cancelaram)</strong>, porque é a resposta que pede ação: cada
            linha é uma vaga que pode ser remanejada se alguém olhar a tempo. Dá para trocar para quem confirmou ou
            ver todas.
          </P>
          <P>
            A coluna <strong>Canal</strong> diz por onde a pessoa respondeu — link do WhatsApp, botões do WhatsApp,
            robô, app do cidadão, presencial na recepção ou uma atendente pelo menu Confirmações. O motivo aparece
            quando a pessoa escreveu algum; ninguém é obrigado a dar motivo.
          </P>
          <Callout tipo="regra" titulo="Responder aqui não mexe no SISREG">
            A resposta do paciente fica no SMSMais. Ela solta a vaga nas nossas filas e nos nossos números, mas
            nenhum agendamento é desmarcado no SISREG por causa dela. Desmarcar é ação de gente, pelo menu
            Confirmações, com o login do SISREG de quem clica.
          </Callout>
        </div>
      ),
    },
    {
      id: 'lote',
      titulo: 'Disparar lote: avisar quem já estava agendado',
      busca: 'disparar lote estoque prévia forçar ignorar chaves enviar agora reenviar já confirmou régua',
      conteudo: (
        <div className="space-y-4">
          <P>
            O aviso automático nasce na importação do SISREG. Isso quer dizer que ligar a chave de uma unidade hoje{' '}
            <strong>não avisa</strong> quem já estava agendado ontem — essa gente fica num estoque que ninguém
            alcança. O disparo em lote existe para cobrir exatamente esse estoque.
          </P>
          <Sub>A régua normal</Sub>
          <P>
            Sem mexer em nada, o lote usa a mesma régua do automático: só agendamento do SISREG, só de unidade e
            procedimento ligados, só data futura, só quem ainda não respondeu.
          </P>
          <Sub>As quatro opções que afrouxam a régua</Sub>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Ignorar chaves de unidade/procedimento',
                descricao:
                  'Avisa uma unidade com o automático desligado, sem precisar religá-lo. Exige escolher a unidade — justamente para não valer para a rede inteira sem querer. Número negado continua sem receber.',
              },
              {
                termo: 'Enviar agora (fora da janela)',
                descricao: 'Vale só para este lote. A janela de horário continua valendo para todo o resto.',
              },
              {
                termo: 'Reenviar para quem já recebeu',
                descricao: 'A comunicação é rearmada: links antigos são revogados, os recibos zeram e sai mensagem nova.',
              },
              {
                termo: 'Reenviar para quem já confirmou',
                descricao: 'A resposta anterior fica na trilha e a pessoa volta para "sem resposta" — ou seja, ela vai reconfirmar.',
              },
            ]}
          />
          <Passos
            itens={[
              { titulo: 'Escolha unidade e o intervalo da agenda', detalhe: 'O padrão cobre de amanhã a dez dias à frente.' },
              {
                titulo: 'Leia a prévia antes de qualquer clique',
                detalhe:
                  'Ela separa quem será avisado de quem é reenvio, de quem está com procedimento desligado, de quem já foi avisado e de quem está fora do SISREG. A tabela por dia mostra a distribuição.',
              },
              {
                titulo: 'Dispare e confirme',
                detalhe:
                  'O botão pede confirmação com o número na frente ("Sim, disparar 128"). É proposital: são mensagens reais para pacientes reais.',
              },
              {
                titulo: 'Acompanhe em Envios',
                detalhe: 'As mensagens entram na fila e saem no ritmo do worker; a tela estima em quantos minutos o lote escoa.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="Prévia zerada quase nunca é bug">
            Zero elegíveis normalmente significa que a régua está funcionando: ou a unidade está desligada, ou os
            procedimentos não estão marcados para avisar, ou todo mundo daquele intervalo já foi avisado. A própria
            prévia diz em qual dessas caixas a gente caiu.
          </Callout>
        </div>
      ),
    },
    {
      id: 'regras',
      titulo: 'Regras e parâmetros: o que governa o automático',
      busca: 'regras parâmetros janela de envio vazão lembrete dias antes conciliação cadência fechamento quem recebe aviso unidade',
      conteudo: (
        <div className="space-y-4">
          <Sub>Parâmetros de disparo</Sub>
          <P>
            O horário em que o automático pode falar e quantas mensagens saem por rodada (a rodada é a cada minuto).
            Fora do horário as mensagens <strong>empilham</strong> — não somem: a sincronização do SISREG pode rodar
            de madrugada sem ninguém receber mensagem de noite. A única exceção é a resposta a quem acabou de se
            identificar pelo WhatsApp, porque a pessoa está na conversa naquele instante.
          </P>

          <Sub>Lembrete antes do agendamento</Sub>
          <P>
            Uma segunda mensagem às vésperas, com um texto para quem já confirmou e outro para quem ainda não
            respondeu. Vale para a rede toda — não é por unidade.
          </P>
          <Callout tipo="regra" titulo="O lembrete se cala por uma semana depois da mensagem principal">
            Se a mensagem principal saiu há menos de sete dias, o lembrete não é enviado àquela pessoa. A regra
            nasceu de um caso concreto: um lote saiu num dia e o lembrete de dois dias alcançaria, dois dias depois,
            justamente quem tinha acabado de receber — duas mensagens quase iguais, e duas cobranças na conta da
            Meta.
          </Callout>

          <Sub>Cancelamentos feitos no SISREG</Sub>
          <P>
            Cancelamento feito pela unidade executante, pela solicitante ou pela regulação não passa pelas nossas
            telas. Sem alguém ir buscar, a vaga fica presa aqui e o paciente segue sendo lembrado de um agendamento
            que já não existe. São dois interruptores, separados de propósito:
          </P>
          <Lista>
            <Item>
              <strong>Trazer os cancelamentos do SISREG para a base</strong> — o motor de leitura. Só lê; nunca
              escreve no SISREG.
            </Item>
            <Item>
              <strong>Avisar o paciente do cancelamento</strong> — a mensagem. Ela diz que foi cancelado e nada
              mais: o motivo registrado no SISREG é informação interna e não vai para o paciente, nem pelo robô.
            </Item>
          </Lista>
          <P>
            Separados porque dá para conciliar a base por alguns dias, conferir os números, e só então começar a
            avisar. Ligar os dois no primeiro dia é apostar que o volume diário é o esperado.
          </P>
          <P>Quatro campos governam o ritmo dessa leitura:</P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'A cada (minutos)',
                descricao:
                  'De quanto em quanto tempo o motor relê o dia corrente. Apertar custa requisição no SISREG; afrouxar custa demora até a vaga aparecer livre aqui.',
              },
              { termo: 'Começa às / Para às', descricao: 'A janela do dia em que ele lê. O padrão cobre o expediente, que é quando a rede cancela.' },
              {
                termo: 'Fecha o dia anterior às',
                descricao:
                  'Uma passada que relê o dia anterior INTEIRO. É o conferidor: pega o que aconteceu fora da janela e qualquer leitura que tenha voltado incompleta — inclusive um domingo, na segunda de manhã.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="O fechamento precisa cair fora da janela de leitura">
            A tela recusa salvar se o fechamento cair dentro do horário de leitura, e o motivo é prático: o
            fechamento relê um dia inteiro, então ali dentro ele disputaria com a leitura do dia corrente o mesmo
            orçamento de requisições do SISREG — e quem perde é justamente a leitura que dá a vaga quase em tempo
            real. Pela mesma razão a janela não pode cobrir as 24 horas: o fechamento precisa de uma hora livre.
          </Callout>
          <Callout tipo="dica" titulo="Mudou aqui, vale já">
            Cadência, janela e hora do fechamento passam a valer sem reiniciar nada. Se o volume de requisições
            assustar no meio da tarde, dá para afrouxar na hora.
          </Callout>

          <Sub>Quem recebe o aviso</Sub>
          <P>
            A mensagem só sai quando <strong>as duas</strong> chaves estão ligadas: a da unidade executante, aqui
            nesta lista, e a do procedimento, que se escolhe na aba SISREG da própria unidade (o link{' '}
            <em>Escolher procedimentos</em> leva para lá). A coluna do meio mostra quantos procedimentos daquela
            unidade estão marcados — "3 de 40" é o aviso ligado com quase nada passando.
          </P>
        </div>
      ),
    },
    {
      id: 'tarifas-e-modelos',
      titulo: 'Tarifas da Meta e teste de modelo',
      busca: 'tarifas meta custo usd utility marketing authentication template categoria testar modelo arte cabeçalho automais zap',
      conteudo: (
        <div className="space-y-4">
          <Sub>Tarifas</Sub>
          <P>
            O preço em dólar por mensagem de modelo, por categoria de cobrança da Meta. Ele é cadastrado aqui, mas o
            número de custo aparece em <strong>Estatísticas</strong>, para quem tem o módulo de custos.
          </P>
          <Lista>
            <Item>Sem tarifa cadastrada, a estimativa fica zerada — não é erro de cálculo, é falta de preço.</Item>
            <Item>
              Texto de conversa e mensagem <em>utility</em> dentro de uma janela de 24 horas já aberta não custam.
            </Item>
            <Item>
              A categoria sai do nome do modelo quando não é declarada: nome com "auth" conta como{' '}
              <em>authentication</em>, o resto conta como <em>utility</em>. Para fugir disso, declare no campo de
              mapeamento, uma linha por modelo.
            </Item>
          </Lista>
          <Callout tipo="atencao" titulo="Estimativa não é fatura">
            O número que sai daqui é a nossa conta a partir das tarifas que alguém digitou. Ele serve para comparar
            meses e enxergar tendência — não para conferir a cobrança da Meta.
          </Callout>

          <Sub>Testar modelo</Sub>
          <P>
            Manda um modelo aprovado para o celular que você escolher, com as variáveis preenchidas como o sistema
            preencheria. Serve para conferir texto, arte e botões antes de ligar um envio de verdade. Não cria
            comunicação nem altera agendamento nenhum — mas a resposta de quem receber cai na Central de
            Atendimento, então prefira o seu próprio número.
          </P>
          <Callout tipo="atencao" titulo="Modelo com foto e sem arte definida é envio recusado">
            Quando o modelo tem imagem no cabeçalho, a arte vai em <strong>toda</strong> mensagem — a que aparece no
            modelo aprovado é só exemplo. Faltando a arte, o envio é recusado antes mesmo de chegar na Meta. A tela
            avisa quais modelos estão nessa situação. A arte não se troca aqui: publica-se no painel do Automais.Zap,
            em Artes, e escolhe-se em Templates.
          </Callout>
        </div>
      ),
    },
    {
      id: 'permissoes',
      titulo: 'Quem pode o quê',
      busca: 'permissão módulo mensageria notificações agendamento consulta edição não aparece botão',
      conteudo: (
        <div className="space-y-4">
          <P>
            A tela inteira pede <strong>Consulta</strong> no módulo <strong>Mensageria</strong> — quem não tem, não
            vê o item no menu. Com Consulta se vê tudo: resumo, envios, respostas e regras.
          </P>
          <P>
            <strong>Edição</strong> é o que libera agir: reenviar um envio, enviar teste de modelo, disparar lote,
            salvar parâmetros e tarifas. Sem ela, os campos ficam travados e os botões simplesmente não aparecem —
            botão que some é proposital, para ninguém clicar no que vai ser recusado.
          </P>
          <Callout tipo="dica" titulo="Uma assimetria que confunde">
            Disparar lote e salvar as regras também aceitam a permissão de edição de <strong>Confirmações</strong>.
            Reenviar um envio e testar modelo, não — esses dois exigem edição na própria Mensageria. Se os botões de
            lote aparecem para você mas o de reenviar não, é isso.
          </Callout>
        </div>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'faq dúvidas não saiu mensagem por que zerado empilhada fila número negado duplicada',
      conteudo: (
        <div className="space-y-4">
          <ListaDefinicoes
            itens={[
              {
                termo: '"A mensagem está Na fila há horas."',
                descricao:
                  'Quase sempre é a janela de horário: fora dela as mensagens esperam. Confira o horário em Regras e parâmetros e a hora atual.',
              },
              {
                termo: '"Liguei a unidade e ninguém recebeu."',
                descricao:
                  'Duas causas, nesta ordem: os procedimentos daquela unidade podem não estar marcados para avisar (o aviso pede as duas chaves), e quem já estava agendado antes não é alcançado pelo automático — esse estoque precisa do disparo em lote.',
              },
              {
                termo: '"Por que tanta gente Retida?"',
                descricao:
                  'Retida é cadastro, não canal: número não verificado, número marcado como inválido ou paciente sem celular. A aba Telefone comprometido, em Confirmações, é a lista de trabalho dessas pessoas.',
              },
              {
                termo: '"O paciente diz que cancelou, mas continua na agenda do SISREG."',
                descricao:
                  'Correto: a resposta do paciente vale aqui e solta a vaga nas nossas contas. Desmarcar no SISREG é ação de gente, pelo menu Confirmações.',
              },
              {
                termo: '"O custo está zerado em Estatísticas."',
                descricao: 'Falta cadastrar as tarifas na aba Regras e parâmetros. Sem preço, não há estimativa.',
              },
              {
                termo: '"Reenviei e falhou de novo, com o mesmo erro."',
                descricao:
                  'Erro que repete não é momentâneo. Leia o "Erro de entrega (Meta)" no detalhe: ele costuma dizer que o número não existe no WhatsApp — o que se resolve no cadastro do paciente.',
              },
            ]}
          />
        </div>
      ),
    },
  ],
};
