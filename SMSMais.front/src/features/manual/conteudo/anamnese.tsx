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
  atualizadoEm: '2026-09-22',
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
                  'Dez perguntas de Sim/Não sobre o passado dela — mamografia e ultrassom anteriores, prótese, cirurgia, gestação, hormônios, tabagismo e histórico familiar. O "Sim" abre um campo de observação.',
              },
              {
                termo: '4 · Queixas referidas',
                descricao:
                  'O que ela sente, marcado por mama: dor, nódulo palpável, secreção, alteração na pele, vermelhidão, retração, edema.',
              },
              {
                termo: '5 · Avaliação de risco',
                descricao:
                  'Quatro critérios objetivos (familiar de 1º grau, câncer antes dos 50 na família, histórico pessoal, mutação genética) e a classificação Baixo, Moderado ou Alto.',
              },
              {
                termo: '6 · Saúde reprodutiva',
                descricao:
                  'Anticoncepcional, se ainda menstrua (com a data da última menstruação) e número de filhos.',
              },
              {
                termo: '7 · Requisição do SISCAN',
                descricao:
                  'As perguntas que existem porque o SISCAN as exige — e que o formulário de papel não tinha. Ver a seção abaixo.',
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
        'mamas examinadas antes radioterapia plastrão ano última mamografia cirurgia tipo lado implante prótese',
      conteudo: (
        <>
          <P>
            A requisição do SISCAN tem seis perguntas obrigatórias. Quatro já estavam no nosso
            questionário; três chegaram na seção 7 porque o papel não perguntava:
          </P>
          <Lista>
            <Item>
              <strong>Antes desta consulta, teve as mamas examinadas por um profissional de
              saúde?</strong> — Sim, Nunca foram examinadas anteriormente, ou Não sabe.
            </Item>
            <Item>
              <strong>Fez radioterapia na mama ou no plastrão?</strong> — o “Sim” abre em qual mama,
              e a mama abre o ano de cada lado.
            </Item>
            <Item>
              <strong>Ano da última mamografia</strong> e <strong>as cirurgias</strong> (tipo, mama
              e ano). Só aparecem quando a seção 3 já disse que houve — perguntar ano de coisa que
              não aconteceu é ruído.
            </Item>
          </Lista>
          <Sub>Por que responder aqui muda o que vai para o Ministério</Sub>
          <P>
            Quando a seção 7 fica em branco, essas perguntas saem para o SISCAN como{' '}
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
      busca: 'gerar requisição botão senha login entrar sessão confirmar salvar criar',
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
          <Callout tipo="lgpd" titulo="A sua senha do SISCAN não é guardada">
            Ela fica na memória do servidor presa à sua sessão e morre quando você sai do sistema.
            Não existe tabela para ela, e ninguém além de você a usa.
          </Callout>
        </>
      ),
    },
    {
      id: 'dois-numeros',
      titulo: 'São dois números, e os dois importam',
      busca: 'protocolo número do exame carimbo zeros à esquerda pesquisar laudar resultado',
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
          <Callout tipo="dica" titulo="O caminho de volta">
            O número do nosso pedido é gravado no campo <strong>Nº do Prontuário</strong> da
            requisição. Assim dá para achar, lá dentro, qual exame nosso deu origem a cada
            requisição — e é isso que impede o sistema de criar duas para a mesma paciente.
          </Callout>
        </>
      ),
    },
    {
      id: 'casos-chatos',
      titulo: 'Os casos chatos',
      busca:
        'rastreamento diagnóstica idade 36 anos responsável não aparece lista vazia duplicada erro falta responder',
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
            responda na seção 3 ou 7, salve e volte.
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
      busca: 'dúvidas perguntas frequentes corrigir alterar depois errado responsável trocar',
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
                'Dá. A data da solicitação enviada é a do pedido original, e o SISCAN aceita data retroativa — o caso comum é justamente registrar um exame que já aconteceu.',
            },
            {
              termo: 'Preciso digitar minha senha do SISCAN toda vez?',
              descricao:
                'Não. Uma vez por sessão. Ela vale enquanto você estiver no sistema e cai quando você sai.',
            },
            {
              termo: 'A anamnese antiga, de antes da seção 7, ainda abre?',
              descricao:
                'Abre normalmente, com as perguntas novas em branco. Preencha antes de gerar a requisição, para não mandar “Não sabe” à toa.',
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
