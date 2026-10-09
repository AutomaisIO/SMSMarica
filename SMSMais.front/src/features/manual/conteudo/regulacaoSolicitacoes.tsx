import { ClipboardList } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Regulação → Solicitações (ADR-0052), o lado de QUEM PEDE: a unidade abre a
 * solicitação, acompanha a pré-regulação e corrige o que voltar. O lado de quem avalia e regula
 * (assumir, aceitar, devolver, recusar) está em `regulacaoGestaoFila`.
 *
 * Conferido no código em 05/10/2026 (regras no envio e wizard/VisaoRegras em 09/10/2026): `features/regulacao` (MinhaFilaPage, NovaSolicitacaoPage,
 * SolicitacaoDetalhePage, NotificacoesRegulacaoPage, AbasFilaRegulacao, TabelaSolicitacoes,
 * StatusRegulacaoBadge, BuscaProcedimento, wizard/PassoPaciente, wizard/PassoRegras,
 * SeletorCidRegulacao, CabecalhoSolicitacao, AnexosSolicitacao, ModalMotivo, LinhaDoTempo) e no backend
 * (`MaquinaDeEstadosRegulacao`, `RegulacaoSolicitacaoService`, `RegulacaoFormularioService`,
 * `RegulacaoCidService`, `RegulacaoNotificacaoService`, `ModuloPermissao` 47/48/51).
 */
export const artigoRegulacaoSolicitacoes: Artigo = {
  slug: 'regulacao-solicitacoes',
  titulo: 'Solicitações de regulação',
  resumo:
    'Como a unidade abre um pedido de consulta ou exame, acompanha a passagem pela pré-regulação e corrige o que voltar — até o pedido chegar ao SISREG, ao SER, ao SERNIT ou ao ESUS de São Gonçalo.',
  grupo: 'regulacao',
  icone: ClipboardList,
  rota: '/app/regulacao/solicitacoes',
  publico: 'Quem abre e acompanha solicitações na unidade',
  atualizadoEm: '2026-10-09',
  palavrasChave: [
    'médico do SISREG',
    'profissional solicitante',
    'solicitação',
    'solicitações',
    'nova solicitação',
    'anexar do cadastro',
    'exames anexados',
    'nome do documento',
    'visualizar anexo',
    'pedido',
    'regulação',
    'pré-regulação',
    'minha fila',
    'agente regulador',
    'gestão de fila',
    'rascunho',
    'devolvida',
    'corrigir e reenviar',
    'recusada',
    'cancelar',
    'número do sistema',
    'interno',
    'externo',
    'NAR',
    'destino',
    'SISREG',
    'SER',
    'SERNIT',
    'ESUS São Gonçalo',
    'procedimento',
    'paciente',
    'CPF pendente',
    'CADSUS',
    'regras',
    'elegibilidade',
    'não sei',
    'ressalva',
    'formulário',
    'médico solicitante',
    'classificação de risco',
    'prioridade',
    'emergência',
    'urgência',
    'hipótese',
    'CID',
    'unidade de origem',
    'queixa principal',
    'resultado de exames',
    'observações',
    'anexo',
    'anexos',
    'pendências',
    'responda no passo regras',
    'pergunta sem resposta',
    'envio travado',
    'linha do tempo',
    'notificações',
    'PR-',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O que é esta tela',
      busca: 'solicitação pedido consulta exame unidade pré-regulação agente regulador sistema de destino caminho',
      conteudo: (
        <>
          <P>
            É por aqui que a unidade pede uma consulta ou um exame para o paciente. O pedido nasce na
            unidade, passa pela <strong>pré-regulação</strong> — onde o agente regulador confere se está
            completo e se cabe no destino — e só então é levado ao sistema que vai agendar: o{' '}
            <strong>SISREG</strong> (oferta do próprio município), o <strong>SER</strong> (Estado), o{' '}
            <strong>SERNIT</strong> (Niterói) ou o <strong>ESUS de São Gonçalo</strong>.
          </P>
          <P>
            A vantagem de passar por aqui, e não direto no sistema, é que o pedido chega ao destino
            completo: com as perguntas do manual respondidas, os documentos anexados e os campos que o
            destino exige preenchidos — e com a história inteira registrada.
          </P>
          <Callout tipo="regra" titulo="A tela não escreve nos sistemas de destino">
            Nada aqui grava no SISREG, no SER, no SERNIT ou no ESUS. O agente regulador inclui o pedido
            na tela do próprio sistema e depois <strong>registra o número</strong> que o sistema gerou.
            A partir daí, o pedido é acompanhado pelo espelho daquele sistema.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-faz-o-que',
      titulo: 'Quem faz o quê',
      busca: 'permissão perfil módulo 47 48 51 unidade solicitante agente regulador gestão de fila configuração escopo unidade topo',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Unidade solicitante',
                descricao:
                  'Abre, edita, anexa, responde as regras, envia para a pré-regulação e cancela enquanto ninguém assumiu. Vê só os pedidos das suas unidades — a escolhida no topo da tela.',
              },
              {
                termo: 'Agente regulador',
                descricao:
                  'Trabalha numa tela à parte, Regulação → Gestão de fila: recebe o que as unidades enviaram, assume, aceita (registra o envio ao sistema), devolve à unidade ou recusa. Tem artigo próprio neste manual — “Gestão de fila (regulação)”. O agente também pode abrir solicitações, por aqui.',
              },
              {
                termo: 'Configuração da regulação',
                descricao:
                  'Mantém as regras de elegibilidade, o catálogo de procedimentos e os limites (tipos e tamanho de anexo, exigir CPF, permitir externo quando há oferta interna).',
              },
            ]}
          />
          <Callout tipo="dica" titulo="Rascunho é de quem abriu">
            Um rascunho só aparece para quem o abriu — nem o agente vê o rascunho de outra pessoa.
            Ele passa a ser visível quando é enviado para a pré-regulação.
          </Callout>
        </>
      ),
    },
    {
      id: 'abrir',
      titulo: 'Abrir uma solicitação (o assistente)',
      busca: 'nova solicitação assistente passos procedimento destino paciente regras formulário anexos revisão avançar rascunho criado unidade salvar rascunho continuar depois',
      conteudo: (
        <>
          <P>
            Em <strong>Regulação → Solicitações → Nova solicitação</strong>, o assistente tem seis passos.
            O botão <BotaoRef>Avançar</BotaoRef> só libera quando o passo está completo.
          </P>
          <Passos
            itens={[
              {
                titulo: 'Procedimento',
                detalhe:
                  'Digite ao menos 3 letras do procedimento, da especialidade ou do exame. Cada resultado mostra onde ele existe: oferta interna no SISREG (e quantas vagas) e em quais sistemas externos.',
              },
              {
                titulo: 'Destino',
                detalhe: 'Interno, Externo (e qual sistema) ou NAR — ver a seção “Para onde vai”.',
              },
              {
                titulo: 'Paciente',
                detalhe:
                  'Busque por CPF, CNS ou nome. Ao sair deste passo, o pedido é salvo como rascunho (aparece o número PR-…).',
              },
              {
                titulo: 'Regras',
                detalhe: 'As perguntas do manual daquele procedimento e os documentos que ele exige.',
              },
              {
                titulo: 'Formulário e anexos',
                detalhe: 'Os campos que o sistema de destino pede e as caixinhas para anexar documentos.',
              },
              {
                titulo: 'Revisão',
                detalhe:
                  'Lista o que ainda falta. Sem pendências, o botão “Enviar para a pré-regulação” libera.',
              },
            ]}
          />
          <P>
            Depois que o rascunho existe, a barra de passos fica clicável: dá para voltar e trocar o
            procedimento, o destino ou o paciente — a troca é gravada ao avançar de novo.
          </P>
          <P>
            Precisa parar no meio? <BotaoRef>Salvar rascunho</BotaoRef> (no rodapé, a partir do momento em
            que procedimento e paciente estão escolhidos) grava o que está na tela e leva ao detalhe. Para
            continuar depois, abra o rascunho em <strong>Minha fila</strong> e clique em{' '}
            <BotaoRef>Continuar rascunho</BotaoRef>. O rascunho é só seu — ninguém mais o vê na fila.
          </P>
          <Callout tipo="dica" titulo="“De qual unidade é esta solicitação?”">
            Se você tem mais de uma unidade e nenhuma está escolhida no topo, o sistema pergunta antes
            de criar o rascunho. A unidade escolhida passa a ficar selecionada no topo.
          </Callout>
        </>
      ),
    },
    {
      id: 'destino',
      titulo: 'Para onde vai: Interno, Externo ou NAR',
      busca: 'destino interno externo nar sisreg ser sernit esus oferta interna permitir externo sistema de destino agendamento indireto',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Interno (SISREG)',
                descricao:
                  'Aparece quando o procedimento tem oferta no SISREG do município. É o caminho normal quando há vaga dentro de casa.',
              },
              {
                termo: 'Externo',
                descricao:
                  'Executado pelo Estado ou por outro município. Escolha o sistema: SER, SERNIT ou ESUS São Gonçalo — só aparecem os que oferecem o procedimento. Havendo oferta interna, o Externo só aparece se a configuração permitir.',
              },
              {
                termo: 'NAR (agendamento indireto)',
                descricao:
                  'Sempre SISREG, em nome de outra unidade. Ainda não está liberado: a tela avisa e pede para usar Interno ou Externo.',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'paciente',
      titulo: 'O paciente',
      busca: 'paciente buscar cpf cns nome cadsus confirmar e usar cpf pendente informar cpf sem cpf cadastro pacientes',
      conteudo: (
        <>
          <Lista>
            <Item>A busca é na nossa base, por CPF, CNS ou nome.</Item>
            <Item>
              Não achou e você digitou um CPF ou CNS completo? Aparece <BotaoRef>Buscar no CADSUS</BotaoRef>.
              Confira o cartão que volta e clique em <BotaoRef>Confirmar e usar</BotaoRef>.
            </Item>
            <Item>
              Nem no CADSUS? Cadastre o paciente pela tela de Pacientes antes de abrir a solicitação.
            </Item>
            <Item>
              Paciente <SeloRef cor="alerta">CPF pendente</SeloRef>: use <BotaoRef>Informar CPF</BotaoRef>.
              Sem CPF o pedido fica salvo como rascunho, mas (com a exigência ligada) não vai para a fila.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'regras',
      titulo: 'As regras do manual (Sim, Não, Não sei)',
      busca: 'regras elegibilidade manual perguntas sim não não sei ressalva destino bloqueado documentos exigidos salvar respostas lista condições marcar caixas basta uma nenhuma destas sem resposta trava envio aviso amarelo',
      conteudo: (
        <>
          <P>
            O passo <strong>Regras</strong> traz o que o manual do procedimento exige: perguntas sobre o
            paciente e os documentos obrigatórios. Responda e clique em <BotaoRef>Salvar respostas</BotaoRef>.
          </P>
          <Lista>
            <Item>
              <strong>Destinos bloqueados</strong>: quando uma resposta impede um sistema, a tela diz por
              qual motivo e por onde o pedido ainda pode seguir.
            </Item>
            <Item>
              <strong>Ressalva</strong>: o pedido passa, mas o agente regulador decide na triagem.
            </Item>
            <Item>
              <strong>“Não sei” é uma resposta válida</strong>: em vez de travar você, ela marca o pedido
              para o agente conferir.
            </Item>
            <Item>Os documentos exigidos viram caixinhas de anexo no passo seguinte.</Item>
          </Lista>
          <Sub>O que trava o envio</Sub>
          <P>
            O aviso amarelo no alto do passo diz o que vai segurar o{' '}
            <BotaoRef>Enviar para a pré-regulação</BotaoRef>: <strong>pergunta sem resposta</strong> e{' '}
            <strong>documento obrigatório sem anexo</strong>. Dá para avançar e voltar depois, mas o pedido só
            sai com tudo respondido — se não souber a resposta, marque <BotaoRef>Não sei</BotaoRef> e o pedido
            segue para o regulador conferir.
          </P>
          <Callout tipo="regra" titulo="Por que o envio confere as regras">
            É o que poupa o técnico regulador de conferir o manual de novo, pedido por pedido: o que chega à
            pré-regulação já passou pelas perguntas e pelos documentos que o manual exige, e as respostas
            vão junto para ele ler.
          </Callout>
          <Sub>Perguntas de lista: basta uma</Sub>
          <P>
            Quando o manual traz condições alternativas (“portadores das seguintes condições: …”), a
            pergunta aparece com <strong>caixas de marcar</strong>. Marque as que o paciente tem —{' '}
            <strong>basta uma</strong> para atender. As marcadas vão junto com o pedido: é por elas que o
            agente regulador sabe por que o caso cabe naquele procedimento.
          </P>
          <Lista>
            <Item>
              <BotaoRef>Nenhuma destas</BotaoRef> — o paciente não tem nenhuma das condições. Num critério de
              inclusão, isso barra o destino.
            </Item>
            <Item>
              <BotaoRef>Não sei</BotaoRef> — vale o mesmo que nas outras perguntas: o agente confere.
            </Item>
          </Lista>
          <P>
            Não responda “Não sei” só para passar: se o paciente tem uma das condições, marque qual. É a
            informação que o regulador mais usa.
          </P>
          <P>Procedimento sem regras cadastradas segue direto para o formulário.</P>
        </>
      ),
    },
    {
      id: 'formulario',
      titulo: 'O formulário do destino Externo (SER e SERNIT)',
      busca:
        'formulário campos médico solicitante busca nome abreviado duplicado incluir médico pendente procurar médico já usado no sisreg cpf do profissional nome do profissional cadastro de médicos do sisreg aguardando cadastro cid secundário cids secundários observações lista classificação de risco prioridade p1 p2 p3 p4 emergência urgência prioridade não urgente baixa complexidade não classificado cor badge hipótese cid lista do recurso ao vivo unidade de origem fixa queixa principal resultado de exames observações obrigatório exigido só pelo',
      conteudo: (
        <>
          <P>
            O formulário reúne os campos que o SER e o SERNIT pedem para aquele procedimento. Campo
            obrigatório em qualquer um dos dois é obrigatório aqui; quando um campo só existe num deles,
            aparece embaixo “exigido só pelo …”.
          </P>
          <Sub>O bloco fixo (todo pedido tem)</Sub>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Médico solicitante *',
                descricao:
                  'Escolhido da lista de médicos do próprio sistema de destino — a lista é longa, então tem busca. Procure por palavras, em qualquer ordem: o SER abrevia muito ("LAURA BEATRIZ A. RODRIGUES"), e a busca acha o médico mesmo digitando o nome completo ("andrade" acha o "A."). Não achou? Use “Não achou? Incluir médico”: o sistema confere se ele já existe com outro nome (no sistema ou já pedido por outra unidade) e, se não existir, o médico fica pendente — aguardando cadastro no SER, feito pela regulação. Você segue com a solicitação normalmente. No SER não dá para apagar cadastro duplicado: por isso quem cadastra lá é a regulação. Só aparecem os médicos do destino escolhido: o médico cadastrado no SER não existe no SERNIT, e vice-versa. A especialidade e o telefone do médico não são perguntados: o próprio sistema preenche a partir do cadastro dele.',
              },
              {
                termo: 'Classificação de risco *',
                descricao:
                  'Um botão colorido por nível: P1 · Emergência (vermelho), P2 · Urgência (amarelo), P3 · Prioridade não urgente (verde) e P4 (azul): “Baixa complexidade” no SER, “Não urgente” no SERNIT. O SERNIT tem ainda “Não classificado” (cinza); o SER não tem essa opção e a tela não a inventa. As cores são as mesmas da fila do SISREG, para a régua ser uma só. Só muda o jeito de mostrar: o que vai para o sistema é a opção dele (no SER, “Prioridade 1” é a P1). Rotina é P4; suba só quando o caso clínico justificar, porque a regulação reclassifica — e a troca fica na linha do tempo do pedido, com o nome de quem trocou.',
              },
              {
                termo: 'Hipótese (CID) *',
                descricao:
                  'Não é texto livre: escolha o CID na caixa. A lista é a que o destino aceita para AQUELE procedimento — um oncológico aceita só códigos de neoplasia, uma consulta comum aceita o CID-10 inteiro. Caixa vazia lista todos; a busca casa código e nome, sem exigir acento. A regulação pode trocar o CID antes de enviar (por exemplo, quando o SER não aceita o código); a troca aparece na linha do tempo.',
              },
            ]}
          />
          <P>
            <strong>CIDs secundários:</strong> abaixo do CID principal, <BotaoRef>Adicionar CID secundário</BotaoRef>{' '}
            abre a mesma caixa de busca; cada CID escolhido vira uma etiqueta (o “x” tira). Os sistemas de
            destino só têm UM CID na tela — os secundários vão, no lançamento, no fim das{' '}
            <strong>Observações</strong> (“CID(s) secundário(s): …”).
          </P>
          <Callout tipo="atencao" titulo="Por que não dá para digitar o CID">
            O SER guarda a hipótese pelo CID escolhido na lista e descarta o texto digitado — o pedido
            voltaria sem hipótese. Se aparecer um texto em amarelo no campo (rascunho antigo), procure o
            CID de novo.
          </Callout>
          <P>
            A <strong>unidade de origem não é perguntada</strong>: vai sempre como “não identificada”, com
            o nome da central reguladora do município, fixo.
          </P>
          <Sub>Sempre obrigatórios</Sub>
          <P>
            <strong>Queixa principal</strong>, <strong>Resultado de exames</strong> e{' '}
            <strong>Observações</strong> são exigidos em todo pedido externo, mesmo nos procedimentos em
            que o próprio sistema os deixa opcionais: é o que o regulador lê para decidir.
          </P>
          <Callout tipo="dica" titulo="Campo de lista desabilitado">
            “Sem as opções deste campo. Recopie o catálogo…” quer dizer que a lista daquele sistema ainda
            não foi copiada. Quem configura a regulação resolve em Configurações.
          </Callout>
          <Sub>Interno (SISREG)</Sub>
          <P>
            O formulário do Interno é o do SISREG: CPF e nome do profissional solicitante (obrigatórios),
            CID, “É retorno?” e observação.
          </P>
          <P>
            O SISREG não tem lista de médicos: o CPF e o nome são digitados. Para não redigitar, use{' '}
            <strong>Procurar médico já usado no SISREG</strong>, logo abaixo do CPF: procure por palavras do
            nome (acha abreviado) ou pelo começo do CPF, e escolher preenche os dois campos. A busca usa o
            nosso cadastro de médicos do SISREG, montado das fichas já importadas, sem repetição. Se o
            médico não tiver CPF no cadastro, só o nome é preenchido e o CPF você digita.
          </P>
          <P>
            Não achou? <BotaoRef>Não achou? Incluir médico</BotaoRef>: nome completo, CPF se tiver,
            conselho. <BotaoRef>Conferir se já existe</BotaoRef> mostra quem parece o mesmo (CPF igual ou
            nome abreviado). Se não for nenhum, <BotaoRef>Incluir</BotaoRef> grava direto no nosso
            cadastro e já preenche a solicitação. Aqui não há pendência, porque no SISREG não há cadastro
            de médico para fazer.
          </P>
        </>
      ),
    },
    {
      id: 'anexos',
      titulo: 'Anexos',
      busca:
        'anexo anexos caixinha documento exigido anexos gerais pdf imagem tamanho limite usar este exame da rede remover nome do documento descrição visualizar abrir anexar do cadastro exames anexados laudo assinado imagens do exame cadastro do paciente',
      conteudo: (
        <>
          <Lista>
            <Item>Cada documento exigido pelo manual vira uma caixinha; toda solicitação tem também “Anexos gerais”.</Item>
            <Item>
              <BotaoRef variante="outline">Anexar arquivo</BotaoRef> pede, antes de enviar, o{' '}
              <strong>nome do documento</strong> (obrigatório) e uma <strong>descrição</strong>. Um
              arquivo por vez — cada um com o seu nome.
            </Item>
            <Item>
              O arquivo anexado <strong>também fica no cadastro do paciente</strong> (aba Exames anexados,
              origem “Solicitação”). Da próxima vez que ele precisar do mesmo documento, ninguém pede de novo.
            </Item>
            <Item>Clique no nome do anexo para abri-lo no visualizador do sistema (PDF ou imagem com zoom).</Item>
            <Item>
              <BotaoRef variante="outline">Anexar do cadastro</BotaoRef> lista o que o paciente já tem
              guardado — documentos, laudos <strong>assinados</strong> e o PDF das imagens dos exames —
              com busca, “olho” para visualizar e <BotaoRef>Anexar</BotaoRef>. O escolhido entra na
              caixinha sem novo upload.
            </Item>
            <Item>Os tipos e o tamanho máximo do upload são os da configuração (normalmente PDF e imagens, até 15 MB).</Item>
            <Item>
              Quando a rede já tem um exame do paciente que serve para a caixinha, aparece a sugestão com{' '}
              <BotaoRef>Usar este</BotaoRef>.
            </Item>
            <Item>Arquivo que já foi enviado ao sistema de destino não pode mais ser removido.</Item>
          </Lista>
          <Callout tipo="dica" titulo="Não aparece o exame que o paciente mandou pelo app?">
            O que o paciente envia pelo app (ou pelo WhatsApp) só entra no “Anexar do cadastro” depois
            que alguém da equipe aceita — na ficha do paciente, aba Exames anexados. É a conferência que
            impede foto ilegível ou documento de outra pessoa de ir para o regulador.
          </Callout>
        </>
      ),
    },
    {
      id: 'revisao',
      titulo: 'Revisão e envio para a pré-regulação',
      busca: 'revisão ainda falta pendências tudo certo enviar para a pré-regulação reenviar cpf destino preencha anexe responda no passo regras regras do manual não pode ir ir para o passo regras',
      conteudo: (
        <>
          <P>A revisão lista o que impede o envio:</P>
          <Lista>
            <Item>paciente sem CPF (quando a exigência está ligada);</Item>
            <Item>destino não escolhido;</Item>
            <Item>campo obrigatório vazio — “Preencha …”;</Item>
            <Item>documento obrigatório faltando ou criticado — “Anexe: …”;</Item>
            <Item>
              pergunta das regras sem resposta — “Responda no passo Regras: …”;
            </Item>
            <Item>
              destino que as regras do manual barraram — “Pelas regras do manual, este pedido não pode ir para
              o …”, com o motivo.
            </Item>
          </Lista>
          <P>
            Quando a pendência é de regra, o botão <BotaoRef>Ir para o passo Regras</BotaoRef> leva direto ao
            lugar de resolver. Os documentos das regras viram caixinha mesmo que o passo Regras tenha sido
            pulado: a conferência do envio avalia as regras de novo e cria o que faltar.
          </P>
          <P>
            Sem pendências, aparece “Tudo certo” e <BotaoRef>Enviar para a pré-regulação</BotaoRef> libera.
            O pedido sai da sua aba de Rascunhos e vai para <AbaRef>Pré-regulação</AbaRef>.
          </P>
        </>
      ),
    },
    {
      id: 'filas',
      titulo: 'Minha fila e as situações',
      busca:
        'minha fila abas rascunhos pré-regulação em análise devolvidas enviadas no sistema encerradas situação status busca número PR unidade topo gestão de fila',
      conteudo: (
        <>
          <P>
            <strong>Minha fila</strong> mostra os pedidos das suas unidades e em que pé está cada um. Ela
            segue a <strong>unidade escolhida no topo da tela</strong> — em todas as abas, de Pré-regulação
            a Encerradas. (A regulação trabalha em outra tela, a <strong>Gestão de fila</strong>; o que você
            vê aqui é só o lado de quem pediu.)
          </P>
          <ListaDefinicoes
            itens={[
              { termo: <AbaRef>Rascunhos</AbaRef>, descricao: 'Só os que você abriu.' },
              {
                termo: <AbaRef>Pré-regulação</AbaRef>,
                descricao: <><SeloRef>Na pré-regulação</SeloRef> — enviado, aguardando um agente assumir.</>,
              },
              { termo: <AbaRef>Em análise</AbaRef>, descricao: <><SeloRef>Em análise</SeloRef> — um agente assumiu.</> },
              { termo: <AbaRef>Devolvidas</AbaRef>, descricao: <><SeloRef>Devolvida à unidade</SeloRef> — a unidade precisa corrigir.</> },
              {
                termo: <AbaRef>Enviadas</AbaRef>,
                descricao: (
                  <>
                    <SeloRef>Enviada ao sistema</SeloRef> e <SeloRef cor="erro">Falha no envio</SeloRef>.
                  </>
                ),
              },
              {
                termo: <AbaRef>No sistema</AbaRef>,
                descricao: (
                  <>
                    <SeloRef>Na fila do sistema</SeloRef> e <SeloRef>Agendada</SeloRef> — o espelho do sistema
                    de destino é que traz essas mudanças.
                  </>
                ),
              },
              {
                termo: <AbaRef>Encerradas</AbaRef>,
                descricao: (
                  <>
                    <SeloRef>Concluída</SeloRef>, <SeloRef>Cancelada</SeloRef> e <SeloRef>Recusada</SeloRef>.
                  </>
                ),
              },
            ]}
          />
          <P>
            A coluna <strong>Número</strong> mostra o número do sistema de destino quando ele existe; antes
            disso, o número interno <strong>PR-…</strong> em cinza. A busca aceita nome, CPF ou número. Um
            rascunho abre direto no assistente; os demais abrem o detalhe.
          </P>
        </>
      ),
    },
    {
      id: 'detalhe',
      titulo: 'O detalhe do pedido',
      busca:
        'detalhe ações cancelar solicitação motivo continuar rascunho corrigir e reenviar linha do tempo ajustada pela regulação risco cid alterado regras do manual respostas condições marcadas anexos visualizar arquivo médico novo a cadastrar pendente formulário preenchido campos abrir na gestão de fila',
      conteudo: (
        <>
          <P>
            O detalhe mostra o cabeçalho do pedido (fluxo, destino, data, CPF), o motivo da situação atual
            quando existe — é onde aparece o que a regulação pediu para corrigir, ou por que recusou — e a{' '}
            <strong>linha do tempo</strong>: quem fez o quê, de qual situação para qual, com o motivo e o
            que mudou campo a campo. Se a regulação alterar a classificação de risco ou o CID antes de
            enviar, aparece “Ajustada pela regulação”, com o nome de quem alterou e o antes → depois.
          </P>
          <P>
            O quadro <strong>Para lançar no SER</strong> (ou SERNIT, SISREG — o destino da solicitação)
            mostra o formulário campo a campo, como foi preenchido. É o que a regulação vai digitar no
            sistema de destino.
          </P>
          <P>
            Quando o procedimento tem regras, o cartão <strong>Regras do manual</strong> mostra o que foi
            respondido e o que o sistema deduziu (idade, sexo), com o resultado de cada uma: atende,
            ressalva, bloqueia ou em aberto. Nas perguntas de lista aparecem as condições marcadas. O
            cartão mostra o que ficou gravado — não refaz a conta.
          </P>
          <P>
            O cartão <strong>Anexos</strong> lista os arquivos que foram com o pedido, caixinha por
            caixinha. Clique no nome para abrir no visualizador (PDF ou imagem com zoom). Aqui é só
            conferência: para anexar ou trocar um arquivo, o pedido precisa estar em rascunho ou devolvido
            — aí é pelo assistente.
          </P>
          <P>
            Quando você pediu um médico que não está na lista do sistema de destino, aparece o cartão
            amarelo <strong>Médico novo a cadastrar</strong>, com nome, documento e especialidade. Quem
            cadastra o médico no sistema e resolve o cartão é a regulação; por aqui você só acompanha — se
            o médico for recusado, o cartão diz o motivo.
          </P>
          <Sub>O que dá para fazer</Sub>
          <Lista>
            <Item>
              <BotaoRef>Continuar rascunho</BotaoRef> / <BotaoRef>Corrigir e reenviar</BotaoRef> — volta ao
              assistente.
            </Item>
            <Item>
              <BotaoRef variante="outline">Cancelar solicitação</BotaoRef> — só enquanto está em rascunho ou
              na pré-regulação, antes de um agente assumir. Abre uma janela que pede o motivo e mostra o
              resultado; cancelamento não tem volta.
            </Item>
          </Lista>
          <Callout tipo="dica" titulo="Assumir, devolver e recusar não ficam aqui">
            As decisões da regulação ficam na tela de análise, em <strong>Regulação → Gestão de fila</strong>.
            Quem também é agente regulador vê neste detalhe o botão{' '}
            <BotaoRef variante="outline">Abrir na Gestão de fila</BotaoRef>, que leva o pedido para lá.
          </Callout>
        </>
      ),
    },
    {
      id: 'devolvida',
      titulo: 'Quando o pedido volta devolvido',
      busca: 'devolvida devolvido corrigir reenviar motivo regulação trocar paciente anexos respostas descartadas',
      conteudo: (
        <>
          <P>
            O pedido aparece em <AbaRef>Devolvidas</AbaRef> com o motivo em destaque. Abra e use{' '}
            <BotaoRef>Corrigir e reenviar</BotaoRef>: tudo pode ser corrigido — procedimento, destino,
            paciente, regras, formulário e anexos. O assistente abre no formulário, que é onde costuma estar
            o que foi pedido.
          </P>
          <Callout tipo="atencao" titulo="Trocar o paciente leva os exames dele">
            Ao trocar o paciente, os exames do paciente anterior saem dos anexos — confira as caixinhas
            antes de reenviar. Trocar o procedimento pode mudar o formulário: o que não existe no novo cai,
            e fica registrado na linha do tempo.
          </Callout>
        </>
      ),
    },
    {
      id: 'notificacoes',
      titulo: 'Notificações',
      busca: 'notificações número registrado situação mudou devolvida recusada falha no envio pendência aberta marcar como vista minhas unidades todas',
      conteudo: (
        <>
          <P>Avisam o que mudou e ainda não foi visto:</P>
          <Lista>
            <Item>número do sistema registrado;</Item>
            <Item>situação mudou no sistema de destino;</Item>
            <Item>devolvida à unidade ou recusada pela regulação;</Item>
            <Item>falha no envio;</Item>
            <Item>pendência aberta.</Item>
          </Lista>
          <P>
            Clique na linha para abrir o pedido, ou em “marcar como vista”. Por padrão aparecem só as não
            vistas. O agente pode alternar entre <AbaRef>Minhas unidades</AbaRef> e{' '}
            <AbaRef>Todas as unidades</AbaRef> — em “Todas”, o clique abre a análise do pedido na Gestão
            de fila.
          </P>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'dúvidas não acho cid não aparece externo nar não avança não consigo cancelar não acho o médico rascunho sumiu',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Não acho o CID que quero.',
              descricao:
                'A lista é a que o destino aceita para aquele procedimento. Um procedimento oncológico, por exemplo, aceita só códigos de neoplasia — o SER recusaria qualquer outro.',
            },
            {
              termo: 'O médico não está na lista.',
              descricao:
                'A lista é a do sistema de destino. Médico que não está cadastrado lá precisa ser cadastrado no próprio sistema antes.',
            },
            {
              termo: 'A opção Externo não aparece.',
              descricao:
                'Ou o procedimento não tem oferta externa, ou tem oferta interna e a configuração não permite mandar para fora.',
            },
            {
              termo: 'O NAR não deixa avançar.',
              descricao: 'O passo da unidade “em nome de” ainda não existe. Use Interno ou Externo por ora.',
            },
            {
              termo: 'Não consigo mais cancelar.',
              descricao: 'Depois que um agente assume, quem decide é ele: peça a devolução ou a recusa.',
            },
            {
              termo: 'Meu colega não vê o meu rascunho.',
              descricao: 'Rascunho é só de quem abriu. Ele aparece para os outros depois de enviado para a pré-regulação.',
            },
          ]}
        />
      ),
    },
  ],
};
