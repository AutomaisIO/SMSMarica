import { Users } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo do cadastro de Pacientes: a lista (busca, sem CPF, excluir) e a ficha, com foco na aba
 * Agendamentos — de onde vem cada linha e o que cada selo de comparecimento quer dizer — e na aba
 * Exames anexados (o acervo do paciente).
 *
 * Conferido no código: `features/pacientes` (PacientesPage, PacienteDetalhePage — abas e
 * SecaoAgendamentos, SecaoExamesAnexados, types.ts), `shared/acervo` (tipos, DialogoDocumento,
 * VisualizadorArquivo) e no backend `AgendamentosPacienteService` (fontes, próximos × histórico,
 * regra dos selos), `VarreduraAgendaService.ConferirChegadasAsync` (chegadas dos últimos 31 dias
 * toda noite), o coletor `FaltasRecentes` (lista de faltas de hora em hora) e
 * `DocumentosPacienteService` (o que entra no acervo, dedup por conteúdo, teto de 10 pendentes,
 * 25 MB, exclusão) + `DocumentosPacienteController` (permissões).
 */
export const artigoPacientes: Artigo = {
  slug: 'pacientes',
  titulo: 'Pacientes (cadastro e ficha)',
  resumo:
    'Buscar e cadastrar pacientes e ler a ficha — a aba Agendamentos, que junta SISREG, SER, SERNIT e ESUS de São Gonçalo e diz se o paciente compareceu, e a aba Exames anexados, onde ficam para sempre os documentos, laudos e imagens dele.',
  grupo: 'cadastros',
  icone: Users,
  rota: '/app/pacientes',
  publico: 'Quem atende, regula ou acompanha o paciente e precisa da ficha dele',
  atualizadoEm: '2026-10-09',
  palavrasChave: [
    'paciente',
    'pacientes',
    'cadastro',
    'ficha',
    'buscar',
    'CPF',
    'sem CPF',
    'nome',
    'novo paciente',
    'excluir',
    'reativar',
    'agendamentos',
    'histórico de acesso',
    'entrar como paciente',
    'histórico de agendamentos',
    'próximos agendamentos',
    'comparecimento',
    'compareceu',
    'faltou',
    'falta',
    'em aberto',
    'chegada',
    'chegada não confirmada',
    'sem registro de chegada',
    'saiu da fila',
    'pendente',
    'em fila',
    '0000',
    'SISREG',
    'SER',
    'SERNIT',
    'ESUS SG',
    'São Gonçalo',
    'absenteísmo',
    'unidade executante',
    'exames anexados',
    'documentos',
    'documento',
    'anexar documento',
    'anexo',
    'acervo',
    'laudo',
    'laudo assinado',
    'imagens do exame',
    'PDF das imagens',
    'anamnese',
    'aguardando conferência',
    'conferência',
    'aceitar',
    'enviado pelo paciente',
    'app do cidadão',
    'WhatsApp',
    'idade',
    'bonequinho',
    'resumo do paciente',
    'falar no WhatsApp',
    'nome do documento',
    'descrição',
    'visualizar',
    'excluir documento',
    'limite de 10',
    '25 MB',
    'duplicado',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'cadastro paciente ficha prontuário onde fica para que serve',
      conteudo: (
        <>
          <P>
            É o cadastro único do cidadão na plataforma. Tudo o que acontece com ele — solicitações de
            regulação, exames, transporte, conversas pelo WhatsApp — se pendura nesta ficha. A lista
            serve para achar a pessoa; a ficha, para entender a situação dela sem abrir outro sistema.
          </P>
        </>
      ),
    },
    {
      id: 'nome-nas-telas',
      titulo: 'O nome do paciente em qualquer tela: idade, resumo e WhatsApp',
      busca:
        'nome do paciente idade 54a anos meses bebê bonequinho ícone resumo do paciente whatsapp falar conversar atalho passar o mouse todas as telas',
      conteudo: (
        <>
          <P>
            Em todo o sistema — filas, listas, cabeçalhos e detalhes —, o nome do paciente aparece do mesmo
            jeito, seguido de três coisas:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <strong>54a</strong>,
                descricao:
                  'A idade, só em anos. Passe o mouse em cima para ver anos e meses. Bebê com menos de um ano aparece em meses (“8m”). Se o cadastro não tem data de nascimento, a idade não aparece.',
              },
              {
                termo: 'Bonequinho',
                descricao:
                  'Abre o resumo do cadastro (CPF, CNS, nascimento, telefone, nome da mãe), sem sair da tela — com atalhos para ver ou editar a ficha.',
              },
              {
                termo: 'WhatsApp',
                descricao:
                  'Abre a conversa com o paciente: se ele escreveu nas últimas 24 horas, cai direto na conversa; senão, abre a nova conversa já com ele escolhido, para mandar o modelo de mensagem.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="O WhatsApp depende do perfil">
            O ícone do WhatsApp só aparece para quem tem, no perfil, o módulo{' '}
            <strong>Central de Atendimento (chat)</strong> — é onde a resposta do paciente chega. Sem o
            módulo, aparecem só o nome, a idade e o bonequinho.
          </Callout>
        </>
      ),
    },
    {
      id: 'buscar',
      titulo: 'Achar um paciente',
      busca: 'buscar procurar nome CPF dez últimos cadastros sem CPF identidade incompleta ver editar',
      conteudo: (
        <>
          <P>
            Sem nada digitado, a lista mostra os <strong>10 últimos cadastros</strong>. Para procurar,
            digite qualquer parte do <strong>nome</strong> (pedaços separados por espaço) ou o{' '}
            <strong>CPF</strong>, com ou sem pontos — a busca devolve até 10 resultados. Clique no nome
            (ou em <BotaoRef variante="ghost">Ver</BotaoRef>) para abrir a ficha.
          </P>
          <Callout tipo="atencao" titulo="O selo “sem CPF”">
            Aparece no cadastro que veio de um prontuário de origem sem CPF. Sem CPF não há como unir
            esse cadastro ao do mesmo cidadão em outras bases, então a mesma pessoa pode aparecer mais
            de uma vez. Confirme a identidade antes de usar o cadastro para algo definitivo.
          </Callout>
          <P>
            <BotaoRef variante="ghost">Excluir</BotaoRef> tira o paciente das buscas, mas o histórico
            é preservado e o cadastro pode ser reativado entrando de novo com o CPF.
          </P>
        </>
      ),
    },
    {
      id: 'ficha',
      titulo: 'As abas da ficha',
      busca: 'abas resumo atendimentos agendamentos transporte exames anexados documentos laudos imagens conversas somente leitura histórico de acesso equipe sandbox entrar como paciente histórico de alterações dados pessoais',
      conteudo: (
        <>
          <P>A ficha abre no Resumo. As outras abas, na ordem da tela:</P>
          <Lista>
            <Item>
              <AbaRef>Atendimentos</AbaRef>, <AbaRef>Agendamentos</AbaRef> e{' '}
              <AbaRef>Transporte</AbaRef> — o que o paciente teve e tem marcado;
            </Item>
            <Item>
              <AbaRef>Exames anexados</AbaRef> — os documentos, laudos assinados e imagens de exame
              que ficam guardados no cadastro (ver abaixo);
            </Item>
            <Item>
              <AbaRef>Conversas</AbaRef> — o WhatsApp com ele, só para leitura (decidir o que fazer com
              um arquivo que ele mandou é na tela de Conversas);
            </Item>
            <Item>
              <AbaRef>Histórico de Acesso</AbaRef> — as entradas no aplicativo. Quando quem entrou foi
              alguém da equipe, testando o app como o paciente pelo Sandbox, a linha diz{' '}
              <strong>Equipe (Sandbox)</strong> e o nome de quem entrou;
            </Item>
            <Item>
              <AbaRef>Histórico de alterações</AbaRef> — quem mudou o cadastro, e quando;
            </Item>
            <Item>
              <AbaRef>Dados pessoais</AbaRef> — identificação, filiação, endereço, contatos, saúde,
              documentos e origem.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'agendamentos',
      titulo: 'A aba Agendamentos: de onde vem cada linha',
      busca: 'agendamentos próximos histórico SISREG SER SERNIT ESUS SG São Gonçalo regulação estadual municipal origem fonte detalhe abrir linha',
      conteudo: (
        <>
          <P>
            A aba junta, num lugar só, o que o paciente tem nos sistemas de regulação: o{' '}
            <strong>SISREG</strong> (regulação municipal), o <strong>SER</strong> (regulação
            estadual), o <strong>SERNIT</strong> (regulação de Niterói) e o{' '}
            <strong>ESUS de São Gonçalo</strong>. A coluna Origem diz de qual sistema veio cada linha.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Próximos agendamentos',
                descricao:
                  'O que ainda vai acontecer e o que está esperando regulação (em fila ou pendente), do mais próximo para o mais distante. Pedido na fila fica aqui mesmo que tenha data antiga — é trabalho em aberto.',
              },
              {
                termo: 'Histórico de agendamentos',
                descricao:
                  'O que já passou ou já se encerrou (cancelado, concluído, saiu da fila), do mais recente para o mais antigo. É aqui que aparece se o paciente veio.',
              },
            ]}
          />
          <P>
            Clicar numa linha do SER, do SERNIT, do ESUS SG ou de exame de imagem abre o detalhe da
            solicitação — desde que o seu perfil tenha permissão de consulta naquele módulo. Consulta
            no SISREG que não é exame de imagem não tem detalhe.
          </P>
        </>
      ),
    },
    {
      id: 'selos',
      titulo: 'Os selos: compareceu, faltou, em aberto',
      busca: 'selo situação compareceu faltou falta em aberto chegada confirmada chegada não confirmada sem registro de chegada saiu da fila concluído cancelado comparecimento absenteísmo',
      conteudo: (
        <>
          <P>
            No histórico, o selo responde “o paciente veio?” — e só afirma com registro de quem
            atendeu. Passe o mouse no selo para ver de onde veio a informação e quando foi lida.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <SeloRef cor="sucesso">Compareceu</SeloRef>,
                descricao:
                  'A unidade executante confirmou a chegada no sistema de regulação, ou a recepção registrou a chegada aqui.',
              },
              {
                termo: <SeloRef cor="erro">Faltou</SeloRef>,
                descricao: 'A unidade executante registrou a falta.',
              },
              {
                termo: <SeloRef cor="alerta">Em aberto</SeloRef>,
                descricao: (
                  <>
                    SISREG e ESUS de São Gonçalo: a data passou e a unidade ainda{' '}
                    <strong>não apontou</strong> nem a chegada nem a falta. <strong>Não é falta</strong> — é uma pendência da unidade, não do
                    paciente.
                  </>
                ),
              },
              {
                termo: <SeloRef cor="alerta">Chegada não confirmada</SeloRef>,
                descricao:
                  'SER e SERNIT: é a situação que o próprio sistema estadual mostra para o agendamento cuja chegada a unidade não confirmou.',
              },
              {
                termo: <SeloRef>Sem registro de chegada</SeloRef>,
                descricao:
                  'A data passou e não há informação: ninguém conferiu o sistema de origem depois do dia. Some na próxima leitura.',
              },
              {
                termo: <SeloRef>Saiu da fila</SeloRef>,
                descricao:
                  'ESUS de São Gonçalo: o pedido não consta mais na fila nem nos agendados. O motivo não é informado — e não é o mesmo que cancelado.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="No SISREG e no ESUS, a unidade escolhe entre três respostas">
            Para cada agendamento que já passou, a unidade executante aponta uma de três situações: no
            SISREG, <strong>Confirmado</strong>, <strong>Falta</strong> ou{' '}
            <strong>Pendente de confirmação</strong>; no ESUS de São Gonçalo, <strong>Efetivado</strong>,{' '}
            <strong>Não efetivado</strong> (com o motivo, em geral “Não Compareceu”) ou nada. A ficha mostra as três
            separadas — Compareceu, Faltou e Em aberto. Agendamento em aberto nunca vira falta sozinho:
            há unidades que deixam de apontar, e tratar isso como ausência do paciente seria injusto
            com ele.
          </Callout>
        </>
      ),
    },
    {
      id: 'atualizacao',
      titulo: 'Quando o selo muda',
      busca: 'atualização quando muda de hora em hora toda noite varredura 31 dias lista de faltas absenteísmo confirmação atrasada SER SERNIT base inteira',
      conteudo: (
        <>
          <Lista>
            <Item>
              <strong>Faltas do SISREG</strong>: a lista de faltas das últimas semanas é relida de hora
              em hora, das 01:20 às 18:00. A falta que a unidade registrou aparece na ficha na rodada
              seguinte.
            </Item>
            <Item>
              <strong>Chegadas do SISREG</strong>: a sincronização noturna de cada unidade relê os
              últimos 31 dias. A chegada confirmada hoje aparece amanhã.
            </Item>
            <Item>
              <strong>SER e SERNIT</strong>: a sincronização da madrugada relê a base inteira — o selo
              acompanha o que estiver lá na manhã seguinte.
            </Item>
            <Item>
              <strong>ESUS de São Gonçalo</strong>: a sincronização da madrugada lê, no histórico de cada
              paciente, a efetivação dos exames agendados nos últimos 31 dias. Exame efetivado não é
              relido.
            </Item>
          </Lista>
          <Callout tipo="dica" titulo="Por que um atendimento recente pode estar “Em aberto”">
            As unidades costumam apontar chegadas e faltas com dias — às vezes semanas — de atraso. Um
            “Em aberto” de ontem é normal; um de dois meses atrás quer dizer que a unidade não apontou.
          </Callout>
        </>
      ),
    },
    {
      id: 'solicitacao-0000',
      titulo: 'Linhas “Pendente” com número 0000',
      busca: '0000 sem número pendente cadastrada à mão manual recepção duplicada cancelar solicitação de exame',
      conteudo: (
        <>
          <P>
            <strong>0000</strong> no número da solicitação quer dizer que ela foi cadastrada à mão no
            painel, sem o número do SISREG. Sem número e sem data, ela fica como{' '}
            <SeloRef cor="alerta">Pendente</SeloRef> em Próximos agendamentos — e{' '}
            <strong>continua lá até alguém cancelar</strong>, porque não há sistema de origem para dizer
            que acabou.
          </P>
          <Callout tipo="atencao" titulo="Antes de cadastrar à mão, procure a do SISREG">
            Se o paciente já tem a solicitação do SISREG para o mesmo exame, use aquela. Cadastrar outra
            sem número cria uma linha duplicada que ninguém fecha.
          </Callout>
        </>
      ),
    },
    {
      id: 'exames-anexados',
      titulo: 'A aba Exames anexados: o que fica guardado do paciente',
      busca:
        'exames anexados documentos acervo laudo assinado imagens do exame pdf das imagens anamnese origem cadastro solicitação whatsapp enviado pelo paciente filtro todos documentos laudos imagens de exame visualizar abrir zoom baixar',
      conteudo: (
        <>
          <P>
            É a pasta do paciente. O que entra aqui fica <strong>para sempre</strong> no cadastro, venha
            de onde vier — por isso é daqui que a regulação e a anamnese puxam documento sem pedir para
            enviar de novo. Numa lista só, do mais recente para o mais antigo, aparecem:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Documentos',
                descricao: (
                  <>
                    Arquivos anexados por alguém. A linha diz a origem: <strong>Cadastro</strong>{' '}
                    (anexado aqui mesmo), <strong>Solicitação</strong> (anexado num pedido de
                    regulação), <strong>WhatsApp</strong> (o paciente mandou na conversa e alguém da
                    equipe guardou) ou <strong>Enviado pelo paciente</strong> (pelo app do cidadão).
                  </>
                ),
              },
              {
                termo: 'Laudos',
                descricao:
                  'Só os laudos ASSINADOS. Rascunho e laudo esperando assinatura não aparecem: o que está aqui pode ser mostrado e anexado como documento definitivo.',
              },
              {
                termo: 'Imagens de exame',
                descricao: 'Um PDF com as imagens de cada exame de imagem do paciente, montado pelo sistema.',
              },
              {
                termo: 'Anamnese',
                descricao:
                  'Os documentos digitalizados pelo celular (QR) e salvos na anamnese de um exame.',
              },
            ]}
          />
          <P>
            Os botões em cima da lista (<AbaRef>Todos</AbaRef> <AbaRef>Documentos</AbaRef>{' '}
            <AbaRef>Laudos</AbaRef> <AbaRef>Imagens de exame</AbaRef> <AbaRef>Anamnese</AbaRef>) só
            filtram o que se vê. Clique no nome (ou no olho) para abrir o arquivo no visualizador do
            sistema, sem sair da ficha: PDF abre no leitor, imagem abre com zoom, e os dois podem ser
            baixados.
          </P>
          <Callout tipo="dica" titulo="O mesmo arquivo não aparece duas vezes">
            O sistema reconhece o arquivo pelo conteúdo, não pelo nome. O mesmo PDF anexado em três
            solicitações vira <strong>um</strong> documento só no cadastro — a lista não enche de cópias.
          </Callout>
        </>
      ),
    },
    {
      id: 'anexar-documento',
      titulo: 'Anexar, renomear e excluir um documento',
      busca:
        'anexar documento enviar arquivo upload nome do documento descrição obrigatório pdf jpg png webp gif 25 MB tamanho tipo editar lápis renomear excluir apagar solicitação cópia',
      conteudo: (
        <>
          <P>
            <BotaoRef variante="outline">Anexar documento</BotaoRef> abre a escolha do arquivo: PDF ou
            imagem (JPG, PNG, WEBP, GIF), até <strong>25 MB</strong>. Antes de enviar, o sistema pede:
          </P>
          <Lista>
            <Item>
              <strong>Nome do documento</strong> (obrigatório) — vem sugerido a partir do nome do
              arquivo; troque por algo que diga o que é.
            </Item>
            <Item>
              <strong>Descrição</strong> (opcional) — de quando é, quem pediu, o que mostra.
            </Item>
          </Lista>
          <Callout tipo="regra" titulo="Por que o nome é obrigatório">
            “IMG_20260930.jpg” não diz a ninguém o que é. Daqui a três meses, quem procurar o resultado
            da biópsia numa solicitação vai achar pelo nome e pela descrição — é a busca do “Anexar do
            cadastro” que lê esses dois campos.
          </Callout>
          <P>
            O lápis corrige o nome e a descrição; <BotaoRef variante="ghost">Excluir</BotaoRef> tira o
            documento do cadastro e <strong>apaga o arquivo</strong>. As solicitações que já usaram o
            documento não perdem nada: cada uma guardou a própria cópia.
          </P>
          <P>
            Laudos, imagens de exame e documentos da anamnese aparecem aqui, mas não se editam nem se
            excluem por esta aba — eles pertencem ao laudo, ao exame e à anamnese de onde vieram.
          </P>
        </>
      ),
    },
    {
      id: 'aguardando-conferencia',
      titulo: '“Aguardando conferência”: o que o paciente mandou pelo app',
      busca:
        'aguardando conferência pendente enviado pelo paciente app do cidadão documentos aceitar conferir confirmar nome limite 10 dez pendentes recusa excluir retirar em conferência',
      conteudo: (
        <>
          <P>
            No app do cidadão, o menu <strong>Documentos</strong> mostra ao paciente tudo o que está
            nesta aba e deixa ele mesmo enviar um exame. O que ele envia chega aqui em cima, num quadro
            separado: <SeloRef cor="alerta">Aguardando conferência</SeloRef>.
          </P>
          <P>
            Documento nessa situação ainda <strong>não vale</strong>: não aparece para anexar em
            solicitação nem na anamnese. Alguém com permissão de edição abre, confere e clica em{' '}
            <BotaoRef>Aceitar</BotaoRef> — o sistema pede para confirmar o nome e a descrição (o
            paciente costuma mandar “foto.jpg”). Se o arquivo não serve (foto ilegível, documento de
            outra pessoa), use <BotaoRef variante="ghost">Excluir</BotaoRef>.
          </P>
          <Callout tipo="regra" titulo="Por que o que vem do paciente espera alguém olhar">
            O que a equipe anexa já passou por alguém que sabe o que é. O que chega de fora pode ser
            qualquer coisa — e, depois de anexado numa solicitação, vai para o regulador como documento
            do paciente. A conferência é o momento de barrar o engano antes que ele viaje.
          </Callout>
          <Callout tipo="atencao" titulo="No máximo 10 esperando por paciente">
            Com 10 documentos aguardando conferência, o app recusa novos envios até a equipe decidir
            (aceitar ou excluir). É a trava contra quem manda arquivo em massa — e é também o aviso de
            que tem trabalho parado nesta ficha.
          </Callout>
          <P>
            Enquanto ninguém conferiu, o próprio paciente pode retirar o que mandou pelo app — por isso
            um item pendente pode sumir da lista sem que ninguém da equipe tenha mexido.
          </P>
        </>
      ),
    },
    {
      id: 'documentos-permissoes',
      titulo: 'Quem pode o quê nos documentos',
      busca: 'permissão perfil pacientes consulta edição exclusão anexar aceitar excluir ver documentos',
      conteudo: (
        <>
          <P>Tudo nesta aba é governado pelo módulo <strong>Pacientes</strong> do perfil:</P>
          <Lista>
            <Item>
              <strong>Consulta</strong> — ver a lista e abrir os arquivos;
            </Item>
            <Item>
              <strong>Edição</strong> — anexar documento, corrigir nome e descrição e aceitar o que o
              paciente enviou;
            </Item>
            <Item>
              <strong>Exclusão</strong> — excluir documento do cadastro.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca:
        'não aparece agendamento paciente diz que foi aparece faltou em aberto antigo sem registro de chegada não abre detalhe permissão laudo não aparece documento não aparece para anexar anexei duas vezes excluí documento solicitação perdeu anexo whatsapp arquivo',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'O paciente diz que foi, mas aparece “Faltou”.',
              descricao:
                'A falta foi registrada pela unidade executante no sistema de regulação. Só ela pode corrigir lá; quando corrigir, a ficha acompanha na próxima leitura.',
            },
            {
              termo: 'Um atendimento de meses atrás está “Em aberto”.',
              descricao:
                'A unidade executante não apontou o resultado no SISREG. Não conte como falta; se precisar saber, pergunte à unidade.',
            },
            {
              termo: 'Um exame antigo do ESUS de São Gonçalo está “Sem registro de chegada”.',
              descricao:
                'A efetivação é lida só para os agendamentos dos últimos 31 dias (a partir de 01/10/2026). Os mais antigos ficam sem registro.',
            },
            {
              termo: 'Cliquei na linha e o detalhe não abriu.',
              descricao:
                'Ou a linha não tem detalhe (agenda local), ou o seu perfil não tem permissão de consulta no módulo daquele sistema (SER, SERNIT, ESUS SG ou Exames e consultas).',
            },
            {
              termo: 'O laudo do exame não aparece em Exames anexados.',
              descricao:
                'Só entra laudo assinado. Enquanto o médico não assinar, o laudo não está pronto para circular como documento.',
            },
            {
              termo: 'O paciente mandou um exame pelo app e ele não aparece para anexar na solicitação.',
              descricao:
                'Está em “Aguardando conferência”. Aceite na aba Exames anexados; a partir daí ele aparece no “Anexar do cadastro”.',
            },
            {
              termo: 'O paciente mandou uma foto pelo WhatsApp. Ela vem para cá sozinha?',
              descricao:
                'Não. Alguém com acesso a Conversas abre o arquivo na conversa e escolhe “Adicionar ao cadastro do paciente” (ou descarta). Só então ele aparece aqui, com origem WhatsApp.',
            },
            {
              termo: 'Anexei o mesmo arquivo de novo e a lista não mudou.',
              descricao:
                'É de propósito: o sistema reconhece o arquivo pelo conteúdo e mantém um documento só.',
            },
            {
              termo: 'Se eu excluir um documento, a solicitação que o usou fica sem anexo?',
              descricao:
                'Não. Cada solicitação guardou a própria cópia. Excluir aqui tira o documento só do cadastro.',
            },
          ]}
        />
      ),
    },
  ],
};
