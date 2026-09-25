import { ClipboardList } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo da Anamnese de mamografia (questionário `mamografia` v2) e da geração da requisição no
 * SISCAN.
 *
 * A ordem segue a dúvida de quem chega: o que é e por que existe → quando e quem preenche → as
 * sete seções → a requisição do SISCAN (o que é, por que os dois números, como gerar) → os casos
 * chatos → quem pode o quê → dúvidas.
 *
 * Tudo aqui foi conferido em `features/anamnese` (tipos, página, SecaoSiscan, modais) e no
 * backend (`SiscanRequisicaoMapper`, `SiscanRequisicaoService`). O de-para com o SISCAN e as
 * medições que o sustentam estão em `Automais.SISCAN/docs/`.
 */
export const artigoAnamnese: Artigo = {
  slug: 'anamnese',
  titulo: 'Anamnese de mamografia',
  resumo:
    'O questionário que a paciente responde antes do exame — e de onde sai, com um clique, a requisição no SISCAN que a médica precisa para laudar.',
  grupo: 'assistencial',
  icone: ClipboardList,
  rota: '/app/anamnese',
  publico: 'Quem recebe a paciente para a mamografia e quem lauda',
  atualizadoEm: '2026-09-25',
  palavrasChave: [
    'anamnese',
    'mamografia',
    'questionário',
    'SISCAN',
    'requisição',
    'protocolo',
    'número do exame',
    'rastreamento',
    'diagnóstica',
    'nódulo',
    'radioterapia',
    'cirurgia de mama',
    'risco elevado',
    'responsável',
    'CNS',
    'prontuário',
    'CADSUS',
    'duplicidade',
    'vincular',
    'já tem requisição',
    'somente leitura',
    'travada',
    'enviada ao SISCAN',
    'requisição antiga',
    'lançada à mão',
    'pareamento',
    'anamnese travada sem eu ter gerado',
    'tipo de cirurgia',
    'mastectomia',
    'biópsia',
    'quadrantectomia',
    'data de atendimento',
    'desconectou',
    'sessão expirou',
    'reconectar',
    'GERENCIAR EXAME',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'anamnese questionário mamografia papel formulário por que existe laudo médica',
      conteudo: (
        <>
          <P>
            A anamnese é o questionário que a paciente responde <strong>antes</strong> da
            mamografia: o que ela sente, o que já fez, o que a família teve. É o mesmo formulário
            que existia em papel, agora dentro do sistema e preso ao pedido do exame.
          </P>
          <P>
            Ela serve a duas pessoas em momentos diferentes. Para quem <strong>lauda</strong>, é
            contexto — uma mama dolorida, uma cirurgia antiga, um histórico familiar mudam a
            leitura da imagem. Para o <strong>SISCAN</strong>, o sistema do Ministério da Saúde que
            acompanha o rastreamento de câncer, é a própria requisição: sem ela, o exame não existe
            lá, e sem existir lá a médica não consegue lançar o resultado.
          </P>
          <Callout tipo="regra" titulo="Uma anamnese por pedido">
            Cada pedido de exame tem uma anamnese só, e ela pode ser reaberta e corrigida enquanto
            o exame não tiver sido laudado.
          </Callout>
        </>
      ),
    },
    {
      id: 'quando-preencher',
      titulo: 'Quando preencher, e quem',
      busca: 'quem preenche enfermeira recepção editar somente leitura laudar janela',
      conteudo: (
        <>
          <P>
            Preenche quem recebe a paciente, com ela na frente — é o único momento em que dá para
            perguntar. A tela abre pela lista de <strong>Solicitações</strong>, no pedido do exame.
          </P>
          <P>
            Aberta de qualquer outro lugar — pela janela do <strong>Laudar</strong> ou pelo PACS —,
            a anamnese entra <strong>somente leitura</strong>. É de propósito: quem lauda precisa
            ler o que a paciente disse, não reescrever. Anexar documento continua liberado nesses
            casos, porque o médico precisa disso justamente enquanto lauda.
          </P>
        </>
      ),
    },
    {
      id: 'as-secoes',
      titulo: 'As sete seções',
      busca:
        'identificação avaliação clínica histórico queixas risco saúde reprodutiva diagrama marcação traço',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: '1 · Identificação do paciente',
                descricao:
                  'Vem do cadastro e não se digita. Se estiver errado, o lugar de corrigir é o cadastro da paciente.',
              },
              {
                termo: '2 · Avaliação clínica pelo profissional',
                descricao:
                  'O que VOCÊ apurou ao examinar: marcações e traços no diagrama das mamas, alterações palpáveis e o que mais precise ser dito.',
              },
              {
                termo: '3 · Histórico clínico',
                descricao:
                  'Dez perguntas de Sim/Não sobre o passado dela — mamografia e ultrassom anteriores, prótese, cirurgia, gestação, hormônios, tabagismo e histórico familiar. O "Sim" abre um campo de observação. Em "Já realizou cirurgia mamária?" (ou prótese) o "Sim" abre também a tabela de cirurgias: tipo, mama direita e esquerda, e o ano.',
              },
              {
                termo: '4 · Queixas referidas',
                descricao:
                  'O que ela sente, marcado por mama: dor, nódulo palpável, secreção, alteração na pele, vermelhidão, retração, edema.',
              },
              {
                termo: '5 · Avaliação de risco',
                descricao:
                  'Quatro critérios objetivos (familiar de 1º grau, câncer antes dos 50 na família, histórico pessoal, mutação genética), a classificação Baixo, Moderado ou Alto e, abaixo, a pergunta do SISCAN "Apresenta risco elevado para câncer de mama?" do jeito que ela é lá.',
              },
              {
                termo: '6 · Saúde reprodutiva',
                descricao:
                  'Anticoncepcional, se ainda menstrua (com a data da última menstruação) e número de filhos.',
              },
              {
                termo: '7 · Requisição do SISCAN',
                descricao:
                  'As demais perguntas que existem porque o SISCAN as exige — e que o formulário de papel não tinha. Ver a seção abaixo.',
              },
            ]}
          />
          <Callout tipo="dica" titulo="O diagrama não é enfeite">
            As marcações e os traços que você desenha sobre as mamas viajam com a anamnese e são o
            que a médica olha primeiro. Um traço de cicatriz de cirurgia diz o lado sem precisar de
            texto.
          </Callout>
        </>
      ),
    },
    {
      id: 'secao-siscan',
      titulo: 'Seção 7: o que o SISCAN exige a mais',
      busca:
        'mamas examinadas antes radioterapia plastrão ano última mamografia cirurgia tipo lado implante prótese tabela mastectomia biópsia risco elevado sim não não sabe quadro',
      conteudo: (
        <>
          <P>
            A requisição do SISCAN tem seis perguntas obrigatórias. Algumas já estavam no nosso
            questionário; as que o papel não perguntava entraram onde fazem sentido:
          </P>
          <Lista>
            <Item>
              <strong>Na seção 7:</strong> <em>Antes desta consulta, teve as mamas examinadas por um
              profissional de saúde?</em> (Sim, Nunca foram examinadas anteriormente, ou Não sabe);{' '}
              <em>Fez radioterapia na mama ou no plastrão?</em> (o “Sim” abre em qual mama, e a mama
              abre o ano de cada lado); e o <em>ano da última mamografia</em>, que só aparece
              quando a seção 3 disse que ela já fez.
            </Item>
            <Item>
              <strong>Na seção 3, as cirurgias:</strong> marcando Sim em “Já realizou cirurgia
              mamária?” (ou em prótese), abre logo abaixo a tabela com os tipos de cirurgia do
              SISCAN — biópsias, segmentectomia, mastectomia, reconstrução, implantes e os demais.
              Marque a mama (D ou E) e escreva o ano.
            </Item>
            <Item>
              <strong>Na seção 5, o risco elevado:</strong> a pergunta do SISCAN, com as três
              respostas dele e o quadro “Risco elevado são:” com o texto de lá.
            </Item>
          </Lista>
          <Callout tipo="regra" titulo="Por que os tipos de cirurgia são os do SISCAN">
            O formulário de papel falava em “retirada de nódulo”, “cirurgia plástica”… O SISCAN não
            aceita esses nomes; aceita os dele. Perguntar do jeito do papel obrigaria alguém a
            traduzir depois — e “retirada de nódulo” não tem par exato lá. Uma resposta só, no
            formato que vai para o Ministério.
          </Callout>
          <Callout tipo="regra" titulo="Risco elevado: a pergunta do SISCAN vence">
            A avaliação de risco da seção 5 continua — é a nossa régua. Mas quando a pergunta do
            SISCAN é respondida, <strong>é ela que vai</strong> para a requisição: quem respondeu
            olhando o quadro deles respondeu a pergunta certa. Só quando ela fica em branco o
            sistema deduz pela nossa régua (Moderado ou Alto viram Sim, Baixo vira Não; sem
            classificação, valem os quatro critérios; sem nada, “Não sabe”).
          </Callout>
          <Sub>Por que responder aqui muda o que vai para o Ministério</Sub>
          <P>
            Quando essas perguntas ficam em branco, elas saem para o SISCAN como{' '}
            <strong>“Não sabe”</strong>. Isso é honesto — é uma resposta do próprio SISCAN, e não um
            campo inventado —, mas é informação perdida justamente com a paciente ali. Responder
            leva segundos e é o que faz o dado federal valer alguma coisa.
          </P>
          <Callout tipo="atencao" titulo="Cirurgia não tem “não sabe”">
            O SISCAN aceita só Sim ou Não em “fez cirurgia de mama”. Se a seção 3 não responder
            essa pergunta, a requisição <strong>não é gerada</strong> — o sistema avisa o que falta
            em vez de afirmar por você que ela nunca operou.
          </Callout>
        </>
      ),
    },
    {
      id: 'gerar-requisicao',
      titulo: 'Gerar a requisição no SISCAN',
      busca: 'gerar requisição botão senha login entrar sessão confirmar salvar criar data da solicitação data do exame data de atendimento DICOM desconectou sessão expirou reconectar inatividade GERENCIAR EXAME autocompletar',
      conteudo: (
        <>
          <P>
            O botão <BotaoRef variante="outline">Gerar Requisição SISCAN</BotaoRef> fica no topo da
            tela, ao lado de Salvar, e some depois que a requisição existe.
          </P>
          <Passos
            itens={[
              {
                titulo: 'Entrar no SISCAN com o SEU login',
                detalhe:
                  'Na primeira vez da sessão, o sistema pede o seu e-mail e a sua senha do SISCAN. Não é o login do sistema — é o seu, porque a requisição fica registrada lá com quem a criou.',
              },
              {
                titulo: 'Conferir o que será enviado',
                detalhe:
                  'O sistema percorre o SISCAN sem gravar nada e mostra, pergunta a pergunta, o que vai ser afirmado sobre a paciente. Leia: depois de gravado não há desfazer.',
              },
              {
                titulo: 'Escolher o responsável',
                detalhe:
                  'A lista vem do próprio SISCAN, com os profissionais daquela unidade. Quem pediu o exame no SISREG aparece pré-selecionado quando o nome casa — confira antes de confirmar.',
              },
              {
                titulo: 'Confirmar',
                detalhe:
                  'A requisição nasce no SISCAN em nome da unidade que solicitou o exame, e os números voltam carimbados no pedido.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="A data que vai no SISCAN é a do exame">
            O campo deles se chama <strong>Data da Solicitação</strong>, mas o que gravamos ali é a
            data em que o <strong>exame foi feito</strong> — lida do próprio aparelho, pelo DICOM.
            Quando o aparelho ainda não mandou as imagens, vale a{' '}
            <strong>data em que a anamnese foi preenchida</strong>, que é o mesmo dia em 99% das
            vezes. O que nunca vai ali é a data em que a unidade pediu o exame no SISREG, que
            costuma ser semanas ou meses antes.
            <br />
            O SISCAN não tem um campo separado de “data de atendimento”: a data do atendimento é
            justamente esta. Na conferência ela aparece como{' '}
            <strong>Data da Solicitação — vai a data do atendimento (exame)</strong>.
          </Callout>
          <Callout tipo="dica" titulo="O SISCAN desconecta por inatividade — e reconecta sozinho">
            O SISCAN derruba a sessão depois de um tempo parado. O sistema percebe e entra de novo
            por você, com a senha que você já deu. Se a queda acontecer no meio de uma operação,
            aparece um aviso dizendo que <strong>nada foi gravado</strong> e o botão{' '}
            <BotaoRef variante="outline">Tentar de novo</BotaoRef> — a segunda tentativa já sai
            conectada. Se a sua sessão aqui tiver acabado (por exemplo, depois de muitas horas
            parada), a tela pede o login do SISCAN de novo e, ao entrar, volta para a conferência.
          </Callout>
          <Callout tipo="lgpd" titulo="A sua senha do SISCAN não é guardada">
            Ela fica na memória do servidor presa à sua sessão e morre quando você sai do sistema.
            Não existe tabela para ela, e ninguém além de você a usa. A tela de login do SISCAN
            também não deixa o navegador guardar nem sugerir o seu e-mail e a sua senha — o
            computador da recepção é de todo mundo.
          </Callout>
        </>
      ),
    },
    {
      id: 'dois-numeros',
      titulo: 'São dois números, e os dois importam',
      busca: 'protocolo número do exame carimbo zeros à esquerda pesquisar laudar resultado travada somente leitura enviada cores do ícone fila requisição antiga lançada à mão pareamento anamnese travada sem eu ter gerado',
      conteudo: (
        <>
          <P>
            Depois de gerar, o pedido passa a exibir <SeloRef>SISCAN</SeloRef> com dois números
            diferentes:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Protocolo',
                descricao:
                  'O número que o SISCAN anuncia ao salvar. É por ele que a requisição é encontrada na tela de pesquisa deles.',
              },
              {
                termo: 'Nº do exame',
                descricao:
                  'Outro número, do mesmo registro. É o que abre o “Incluir Resultado do Exame” — o caminho da médica para lançar o laudo.',
              },
            ]}
          />
          <P>
            Não são o mesmo número escrito de dois jeitos: são registros distintos do SISCAN, e a
            tela deles pesquisa pelos dois. Por isso guardamos ambos.
          </P>
          <Callout tipo="regra" titulo="Depois de enviada, a anamnese trava">
            Gerada a requisição, o questionário passa a abrir <strong>somente leitura</strong>, com
            uma tarja no topo mostrando o protocolo e o Nº do exame. Não é capricho: aquelas
            respostas viraram uma requisição numa base do Ministério, e mudá-las aqui criaria duas
            verdades para o mesmo exame sem ninguém saber qual vale. Precisa corrigir? É na própria
            requisição do SISCAN, que abre editável para quem a criou. Anexar documento continua
            liberado — não é alterar o questionário.
          </Callout>
          <Callout tipo="dica" titulo="Dá para ver pela fila quais já foram">
            Na lista de Solicitações, o ícone da anamnese tem três cores: <strong>azul</strong> sem
            anamnese, <strong>verde</strong> preenchida e <strong>verde-azulado</strong> já enviada
            ao SISCAN. Passando o mouse, o protocolo aparece.
          </Callout>
          <Callout tipo="dica" titulo="O caminho de volta">
            O número do nosso pedido é gravado no campo <strong>Nº do Prontuário</strong> da
            requisição. Assim dá para achar, lá dentro, qual exame nosso deu origem a cada
            requisição — e é isso que impede o sistema de criar duas para a mesma paciente.
          </Callout>
          <Callout tipo="regra" titulo="Anamnese antiga que já aparece travada">
            Antes deste botão existir, a requisição era digitada direto no SISCAN pela unidade.
            Essas requisições foram <strong>pareadas com as anamneses e carimbadas</strong> — pela
            paciente (Cartão SUS) e pelo dia em que a anamnese foi preenchida. Por isso uma
            anamnese de meses atrás pode abrir somente leitura, com um protocolo que ninguém gerou
            por aqui: ela já tinha requisição, e agora o sistema sabe disso. O efeito prático é o
            que importa — o botão não cria uma segunda, e a médica enxerga o número.
            <br />
            Onde o pareamento ficou em dúvida (duas requisições no mesmo dia, ou nenhuma naquele
            dia), <strong>nada foi carimbado</strong>: a anamnese continua editável e o botão
            continua disponível. Se você gerar e o SISCAN já tiver uma, a crítica avisa antes de
            criar.
          </Callout>
        </>
      ),
    },
    {
      id: 'casos-chatos',
      titulo: 'Os casos chatos',
      busca:
        'rastreamento diagnóstica idade 36 anos responsável não aparece lista vazia duplicada '
        + 'duplicidade já tem requisição vincular ao pedido prontuário cartão SUS um ano erro falta responder',
      conteudo: (
        <>
          <Sub>Rastreamento ou diagnóstica — quem decide é a idade</Sub>
          <P>
            A partir de <strong>36 anos</strong> a mamografia entra como{' '}
            <strong>rastreamento</strong>; abaixo disso, como <strong>diagnóstica</strong>. Isso não
            é escolha da tela e tem uma consequência que surpreende: a lista de responsáveis do
            SISCAN <strong>muda</strong> entre os dois tipos. Ou seja, a idade da paciente também
            decide quem pode assinar a requisição.
          </P>

          <Sub>“Este pedido já tem requisição no SISCAN”</Sub>
          <P>
            Antes de criar qualquer coisa, o sistema pergunta ao SISCAN se já existe requisição com
            o <strong>Nº do Prontuário deste pedido</strong>. Se existir, ele não cria outra: mostra
            os números e oferece <BotaoRef>Vincular ao pedido</BotaoRef>, que traz o protocolo e o
            nº do exame para cá. É o que acontece com requisições que nasceram fora do painel —
            digitadas direto no SISCAN, por exemplo.
          </P>

          <Sub>“Esta paciente já tem requisição de mamografia”</Sub>
          <P>
            A segunda pergunta é pelo <strong>Cartão SUS</strong>, olhando de um ano atrás até dez
            dias à frente. Se aparecer alguma requisição que <strong>não é deste pedido</strong>, o
            sistema para e lista o que encontrou — protocolo, unidade e status. Não há botão para
            seguir: qual das duas vale, e o que fazer com a outra, é decisão de gente, e se resolve
            no SISCAN.
          </P>
          <Callout tipo="regra" titulo="Por que o sistema não escolhe">
            Duas requisições abertas para a mesma mulher viram dois exames, duas filas e dois
            laudos possíveis. Criar a segunda em silêncio empurraria para a frente um problema que
            só quem conhece o caso sabe resolver.
          </Callout>

          <Sub>O responsável não está na lista</Sub>
          <P>
            A lista é a do SISCAN daquela unidade, não a nossa. Se quem pediu o exame não aparece,
            é porque essa pessoa não está cadastrada no SISCAN naquela unidade para aquele tipo de
            mamografia — e não há o que adivinhar. Escolha outro profissional da lista ou trate o
            cadastro lá.
          </P>

          <Sub>“Falta responder na anamnese”</Sub>
          <P>
            O modal mostra em amarelo o que impede a geração e o botão fica desabilitado. Feche,
            responda na seção indicada (em geral a 3), salve e volte.
          </P>

          <Sub>Já existe requisição</Sub>
          <P>
            O botão some e o cabeçalho passa a mostrar os números. Gerar de novo criaria uma
            requisição duplicada para a mesma paciente no sistema federal — por isso o sistema
            recusa, mesmo que você tente.
          </P>

          <Callout tipo="atencao" titulo="Salvou e não gerou?">
            Ao salvar e ao sair, se ainda não houver requisição, o sistema pergunta se você quer
            gerar agora. Não é insistência: é o último momento em que você ainda está na tela com o
            caso na cabeça. Depois, vira uma pendência que ninguém vê.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil solicitações de exame consulta edição',
      conteudo: (
        <>
          <P>
            Tudo aqui é governado pelo módulo <strong>Solicitações de Exame</strong> do perfil:
          </P>
          <Lista>
            <Item>
              <strong>Consulta</strong> — abrir e ler a anamnese, e ver se já há requisição no
              SISCAN.
            </Item>
            <Item>
              <strong>Edição</strong> — preencher o questionário, anexar documentos e gerar a
              requisição.
            </Item>
          </Lista>
          <P>
            Além da permissão, gerar exige a sua credencial do SISCAN — ter permissão no nosso
            sistema não dá acesso ao do Ministério.
          </P>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'dúvidas perguntas frequentes corrigir alterar depois errado responsável trocar desconectou menu GERENCIAR EXAME não existe',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Respondi errado e já gerei. E agora?',
              descricao:
                'A requisição criada por nós abre editável no próprio SISCAN, pelo caminho Alterar/Visualizar Requisição do Exame. A correção é feita lá.',
            },
            {
              termo: 'O exame já foi feito. Ainda dá para gerar a requisição?',
              descricao:
                'Dá. O que vai no campo Data da Solicitação é a data em que o exame foi feito (não a do pedido no SISREG), e o SISCAN aceita data retroativa — o caso comum é justamente registrar um exame que já aconteceu.',
            },
            {
              termo: 'Apareceu “O item de menu GERENCIAR EXAME não existe para esta conta”. E agora?',
              descricao:
                'Era a sessão do SISCAN que tinha caído por inatividade — a mensagem culpava a conta sem motivo. Desde 25/09/2026 o sistema reconecta sozinho nesse caso; se ainda aparecer, clique em Tentar de novo e, persistindo, abra um ticket.',
            },
            {
              termo: 'Preciso digitar minha senha do SISCAN toda vez?',
              descricao:
                'Não. Uma vez por sessão. Ela vale enquanto você estiver no sistema e cai quando você sai.',
            },
            {
              termo: 'A anamnese antiga, de antes das perguntas do SISCAN, ainda abre?',
              descricao:
                'Abre normalmente, com as perguntas novas em branco (seção 7, tabela de cirurgias e risco elevado do SISCAN). Preencha antes de gerar a requisição, para não mandar “Não sabe” à toa.',
            },
            {
              termo: 'Por que o conselho do responsável não aparece para eu preencher?',
              descricao:
                'Porque o SISCAN o deriva sozinho a partir do profissional escolhido. Não é campo de digitar, nem lá nem aqui.',
            },
          ]}
        />
      ),
    },
  ],
};
