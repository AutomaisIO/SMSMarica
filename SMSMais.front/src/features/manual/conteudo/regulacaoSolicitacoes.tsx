import { ClipboardList } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Regulação → Solicitações (ADR-0052): a unidade abre a solicitação, ela passa pela
 * pré-regulação e o agente regulador leva ao sistema de destino.
 *
 * Conferido no código em 01/10/2026: `features/regulacao` (MinhaFilaPage, FilaRegulacaoPage,
 * NovaSolicitacaoPage, SolicitacaoDetalhePage, NotificacoesRegulacaoPage, AbasFilaRegulacao,
 * TabelaSolicitacoes, StatusRegulacaoBadge, BuscaProcedimento, wizard/PassoPaciente,
 * wizard/PassoRegras, SeletorCidRegulacao, ModalRegistrarEnvio, LinhaDoTempo) e no backend
 * (`MaquinaDeEstadosRegulacao`, `RegulacaoSolicitacaoService`, `RegulacaoFormularioService`,
 * `RegulacaoCidService`, `RegulacaoNotificacaoService`, `ModuloPermissao` 47/48/51).
 */
export const artigoRegulacaoSolicitacoes: Artigo = {
  slug: 'regulacao-solicitacoes',
  titulo: 'Solicitações de regulação',
  resumo:
    'Como a unidade abre um pedido de consulta ou exame, como ele passa pela pré-regulação e como o agente regulador o leva ao SISREG, ao SER, ao SERNIT ou ao ESUS de São Gonçalo.',
  grupo: 'regulacao',
  icone: ClipboardList,
  rota: '/app/regulacao/solicitacoes',
  publico: 'Quem abre solicitações na unidade e quem trabalha na pré-regulação (agente regulador)',
  atualizadoEm: '2026-10-02',
  palavrasChave: [
    'solicitação',
    'solicitações',
    'nova solicitação',
    'pedido',
    'regulação',
    'pré-regulação',
    'fila da regulação',
    'minha fila',
    'agente regulador',
    'triagem',
    'rascunho',
    'devolvida',
    'corrigir e reenviar',
    'recusada',
    'cancelar',
    'assumir',
    'registrar envio',
    'número do sistema',
    'OK interno',
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
    'hipótese',
    'CID',
    'unidade de origem',
    'queixa principal',
    'resultado de exames',
    'observações',
    'anexo',
    'anexos',
    'pendências',
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
      busca: 'permissão perfil módulo 47 48 51 unidade solicitante agente regulador configuração escopo unidade topo',
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
                  'Trabalha na “Fila da regulação”: a unidade escolhida no topo da tela, ou o município inteiro com “todas”. Assume o pedido, devolve à unidade, recusa, registra o envio ao sistema e dá o “OK” nos pedidos internos. O agente também pode abrir solicitações.',
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
      busca: 'nova solicitação assistente passos procedimento destino paciente regras formulário anexos revisão avançar rascunho criado unidade',
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
      busca: 'regras elegibilidade manual perguntas sim não não sei ressalva destino bloqueado documentos exigidos salvar respostas lista condições marcar caixas basta uma nenhuma destas',
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
        'formulário campos médico solicitante busca nome abreviado duplicado incluir médico pendente aguardando cadastro lista classificação de risco prioridade hipótese cid lista do recurso ao vivo unidade de origem fixa queixa principal resultado de exames observações obrigatório exigido só pelo',
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
                descricao: 'Prioridade 1 a 4, como o próprio sistema escreve.',
              },
              {
                termo: 'Hipótese (CID) *',
                descricao:
                  'Não é texto livre: escolha o CID na caixa. A lista é a que o destino aceita para AQUELE procedimento — um oncológico aceita só códigos de neoplasia, uma consulta comum aceita o CID-10 inteiro. Caixa vazia lista todos; a busca casa código e nome, sem exigir acento.',
              },
            ]}
          />
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
        </>
      ),
    },
    {
      id: 'anexos',
      titulo: 'Anexos',
      busca: 'anexo anexos caixinha documento exigido anexos gerais pdf imagem tamanho limite usar este exame da rede remover',
      conteudo: (
        <Lista>
          <Item>Cada documento exigido pelo manual vira uma caixinha; toda solicitação tem também “Anexos gerais”.</Item>
          <Item>Os tipos e o tamanho máximo são os da configuração (normalmente PDF e imagens, até 15 MB).</Item>
          <Item>
            Quando a rede já tem um exame do paciente que serve para a caixinha, aparece a sugestão com{' '}
            <BotaoRef>Usar este</BotaoRef>.
          </Item>
          <Item>Arquivo que já foi enviado ao sistema de destino não pode mais ser removido.</Item>
        </Lista>
      ),
    },
    {
      id: 'revisao',
      titulo: 'Revisão e envio para a pré-regulação',
      busca: 'revisão ainda falta pendências tudo certo enviar para a pré-regulação reenviar cpf destino preencha anexe',
      conteudo: (
        <>
          <P>A revisão lista o que impede o envio:</P>
          <Lista>
            <Item>paciente sem CPF (quando a exigência está ligada);</Item>
            <Item>destino não escolhido;</Item>
            <Item>campo obrigatório vazio — “Preencha …”;</Item>
            <Item>documento obrigatório faltando ou criticado — “Anexe: …”.</Item>
          </Lista>
          <P>
            Sem pendências, aparece “Tudo certo” e <BotaoRef>Enviar para a pré-regulação</BotaoRef> libera.
            O pedido sai da sua aba de Rascunhos e vai para <AbaRef>Pré-regulação</AbaRef>.
          </P>
        </>
      ),
    },
    {
      id: 'filas',
      titulo: 'As filas e as situações',
      busca:
        'minha fila fila da regulação abas rascunhos pré-regulação em análise devolvidas enviadas no sistema encerradas situação status busca número PR fluxo destino agente filtro unidade topo todas município',
      conteudo: (
        <>
          <P>
            <strong>Minha fila</strong> mostra os pedidos das suas unidades; a <strong>Fila da regulação</strong>{' '}
            (agente) tem ainda os filtros de fluxo e de destino e a coluna do agente que assumiu. As duas
            seguem a <strong>unidade escolhida no topo da tela</strong> — em todas as abas, de Pré-regulação
            a Encerradas; o agente vê o município inteiro escolhendo “todas” lá em cima. As abas são as
            mesmas nas duas:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: <AbaRef>Rascunhos</AbaRef>, descricao: 'Só os que você abriu.' },
              { termo: <AbaRef>Pré-regulação</AbaRef>, descricao: <SeloRef>Na pré-regulação</SeloRef> },
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
      titulo: 'O detalhe e as ações',
      busca:
        'detalhe ações assumir devolver à unidade recusar registrar envio número gerado ok já está no sisreg cancelar solicitação motivo continuar rascunho corrigir e reenviar linha do tempo regras do manual respostas condições marcadas médico novo a cadastrar pendente cadastrei já existia',
      conteudo: (
        <>
          <P>
            O detalhe mostra o cabeçalho do pedido (fluxo, destino, data, CPF), o motivo da situação atual
            quando existe e a <strong>linha do tempo</strong>: quem fez o quê, de qual situação para qual,
            com o motivo e o que mudou campo a campo.
          </P>
          <P>
            Quando o procedimento tem regras, o cartão <strong>Regras do manual</strong> mostra o que foi
            respondido e o que o sistema deduziu (idade, sexo), com o resultado de cada uma: atende,
            ressalva, bloqueia ou em aberto. Nas perguntas de lista aparecem as condições marcadas. O
            cartão mostra o que ficou gravado — não refaz a conta.
          </P>
          <P>
            Quando a unidade pediu um médico que não está na lista do sistema, aparece o cartão amarelo{' '}
            <strong>Médico novo a cadastrar</strong>, com nome, documento e especialidade. O agente cadastra no
            SER (ícone “Adicionar médico” ao lado de “Médico responsável”) e resolve aqui:{' '}
            <BotaoRef>Cadastrei no SER</BotaoRef>, <BotaoRef>Já existia no SER</BotaoRef> (escolhe o cadastro
            que já estava lá — a solicitação passa a usar esse nome) ou <BotaoRef>Recusar</BotaoRef>, com o
            motivo. O <BotaoRef>Registrar envio</BotaoRef> só libera depois disso.
          </P>
          <Sub>Unidade</Sub>
          <Lista>
            <Item>
              <BotaoRef>Continuar rascunho</BotaoRef> / <BotaoRef>Corrigir e reenviar</BotaoRef> — volta ao
              assistente.
            </Item>
            <Item>
              <BotaoRef>Cancelar solicitação</BotaoRef> — só enquanto está em rascunho ou na pré-regulação,
              antes de um agente assumir. Pede o motivo.
            </Item>
          </Lista>
          <Sub>Agente regulador</Sub>
          <Lista>
            <Item>
              <BotaoRef>Assumir</BotaoRef> — o pedido passa para <SeloRef>Em análise</SeloRef> no seu nome. Se
              outro agente assumiu antes, a tela avisa.
            </Item>
            <Item>
              <BotaoRef>Devolver à unidade</BotaoRef> — com o que precisa ser corrigido (obrigatório).
            </Item>
            <Item>
              <BotaoRef>Recusar</BotaoRef> — com o motivo (obrigatório; é o que a unidade lê).
            </Item>
            <Item>
              <BotaoRef>Registrar envio</BotaoRef> — depois de incluir o pedido na tela do sistema, informe
              o número que ele gerou. O número passa a identificar o caso; número repetido no mesmo sistema
              é recusado (sinal de pedido lançado duas vezes).
            </Item>
            <Item>
              <BotaoRef>OK — já está no SISREG</BotaoRef> — só no Interno, para o pedido que já está na fila
              do SISREG.
            </Item>
          </Lista>
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
            <AbaRef>Todas as unidades</AbaRef>.
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
