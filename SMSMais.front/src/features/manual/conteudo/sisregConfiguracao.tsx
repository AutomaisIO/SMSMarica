import { Settings2 } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Regulação → SISREG → Configuração. Conferido no código em 06/10/2026:
 * `features/sisreg/pages/SisregConfiguracaoPage` e as seções `SincronismoAutomaticoSecao`,
 * `SincronizarTudoSecao`, `SincronismoEscalasSecao`, `FilaEsperaSecao` e `IndicadoresColetaSecao`;
 * no servidor, `VarreduraAgendaService` (janela, dias não lidos, chegadas), `DecididorVarreduraSisreg`
 * (nova tentativa) e `SisregConfiguracaoController` (Testar conexão, permissões).
 * Seção "endereco" conferida em 09/10/2026: `features/integracoes/components/EnderecoSisregStatus`
 * (cartão do SISREG em Integrações) e, no servidor, `VerificadorEnderecoSisregWorker` (verificação a
 * cada 2 min, avisos) — o conserto da rota é o timer do servidor, docs/sisreg-egress.md.
 */
export const artigoSisregConfiguracao: Artigo = {
  slug: 'sisreg-configuracao',
  titulo: 'Configuração do SISREG',
  resumo:
    'Credenciais da integração, o interruptor do sincronismo automático e os motores que leem o SISREG sozinhos: unidades e médicos, agenda de cada unidade, escalas, fila de espera e a coleta dos indicadores.',
  grupo: 'regulacao',
  icone: Settings2,
  rota: '/app/sisreg/configuracao',
  publico: 'Quem cuida da integração com o SISREG (não quem está regulando)',
  atualizadoEm: '2026-10-10',
  palavrasChave: [
    'configuração sisreg',
    'endereço do sisreg',
    'ip do sisreg',
    'trocou de ip',
    'túnel',
    'fora do túnel',
    'bloqueio',
    'não foi possível autenticar',
    'credenciais',
    'senha',
    'testar conexão',
    'sincronismo automático',
    'desligar sincronismo',
    'chave mestra',
    'sincronizar tudo',
    'carga inicial',
    'programar todas as unidades',
    'habilitar todas',
    'desabilitar todas',
    'importação diária',
    'varredura',
    'escalas',
    'grade de vagas',
    'completar executante',
    'fila de espera',
    'reler a fila',
    'coletor',
    'indicadores',
    'tentar todas de novo',
    'leitura parada',
    'login recusado',
    'captcha',
    'orçamento',
    'requisições',
    'sessão do operador',
    'cadsus',
    'parcial',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve esta tela',
      busca: 'integração leitura sisreg operador credencial requisições orçamento captcha anti-robô sessão única',
      conteudo: (
        <>
          <P>
            A plataforma <strong>só lê</strong> o SISREG: agenda das unidades, escalas, fila de espera e as telas
            dos indicadores. Esta tela guarda a credencial que faz essa leitura e controla os motores que leem
            sozinhos, cada um com o seu horário.
          </P>
          <P>Duas regras do SISREG explicam quase tudo o que está aqui:</P>
          <Lista>
            <Item>
              <strong>Uma sessão por operador.</strong> Enquanto um motor lê, ele derruba quem estiver usando a
              mesma credencial no navegador, e é derrubado por ela. Por isso os motores rodam de madrugada e um
              espera o outro terminar.
            </Item>
            <Item>
              <strong>Limite de acessos por hora.</strong> Passou de cerca de 500 requisições na hora, o SISREG
              pede CAPTCHA. Cada seção diz quanto custa, e os motores param sozinhos antes de chegar no limite.
            </Item>
          </Lista>
          <Callout tipo="atencao" titulo="CAPTCHA">
            Quando o SISREG pede CAPTCHA, a unidade ou o coletor fica pausado por um dia, e o aviso chega no
            celular. Relogar não resolve: alguém precisa abrir o SISREG no navegador com o usuário da integração,
            resolver o CAPTCHA e só então retomar.
          </Callout>
        </>
      ),
    },
    {
      id: 'interruptor',
      titulo: 'Sincronismo automático: a chave mestra',
      busca: 'desligar religar sincronismo automático chave mestra pausa temporária não apaga programação',
      conteudo: (
        <>
          <P>
            É o primeiro quadro da tela, de propósito: é o que se procura com pressa. <BotaoRef variante="outline">
            Desligar sincronismo</BotaoRef> faz com que nada rode sozinho: importação diária das unidades, escalas,
            fila de espera e coletor. Nenhum horário configurado é apagado, e <BotaoRef>Religar sincronismo</BotaoRef>{' '}
            devolve tudo como estava. Os botões manuais da tela continuam funcionando.
          </P>
          <P>
            Use para trabalhar direto no SISREG com o mesmo login da integração sem ser derrubado. Enquanto estiver
            desligado, o quadro fica amarelo e avisa que agendamentos novos só entram por ação manual. É o estado
            que se esquece ligado e vira “por que parou de importar?”.
          </P>
          <Callout tipo="regra">
            Não confunda com <BotaoRef variante="ghost">Desabilitar todas</BotaoRef>, mais abaixo. Ele reescreve a
            agenda de cada unidade como inativa e perde quem estava ligado. Para uma pausa, a chave mestra é o
            caminho.
          </Callout>
        </>
      ),
    },
    {
      id: 'credenciais',
      titulo: 'Credenciais e escopo',
      busca: 'url base escopo municipal nacional uf município ibge centrais reguladoras login senha token testar conexão integração ativa cadastro do paciente cadsus ser consultas simultâneas',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              { termo: 'URL base, Escopo, UF, Município e Centrais reguladoras', descricao: 'De onde e de quem a integração lê. As centrais vão separadas por vírgula.' },
              {
                termo: 'Login, Senha e Token',
                descricao:
                  'A senha e o token são guardados de forma segura e nunca aparecem de volta. Quando já existem, o campo diz “Já definida”: preencha só para trocar.',
              },
              {
                termo: 'Consulta de cadastro do paciente',
                descricao:
                  'Onde a importação busca o cadastro (CADSUS) de um paciente novo. Pelo SISREG, cada consulta gasta do mesmo limite por hora. Para importação grande, prefira o SER, que consulta o mesmo cadastro sem esse limite, ou o SER com retorno ao SISREG se o SER falhar.',
              },
              {
                termo: 'Consultas simultâneas no SER',
                descricao: 'De 1 a 8 consultas de cadastro ao mesmo tempo, cada uma na sua sessão do SER. Só vale quando a fonte é o SER.',
              },
              { termo: 'Integração ativa', descricao: 'Desmarcada, a integração não é usada.' },
            ]}
          />
          <P>
            <BotaoRef variante="outline">Testar conexão</BotaoRef> faz uma consulta mínima à fila com as credenciais{' '}
            <strong>salvas</strong> e responde “Conexão OK” com o total da fila, ou o motivo da falha. Salve antes
            de testar uma senha nova.
          </P>
        </>
      ),
    },
    {
      id: 'rede-inteira',
      titulo: 'Unidades, médicos e procedimentos da rede inteira',
      busca: 'sincronizar tudo agora rede inteira unidades criadas mapeadas médicos procedimentos carga inicial programar sincronismo diário intervalo prévia habilitar todas desabilitar todas sincronizações recentes',
      conteudo: (
        <>
          <P>
            <BotaoRef>Sincronizar tudo agora</BotaoRef> lê no SISREG todas as unidades que a credencial enxerga,
            cadastra aqui as que faltam e atualiza os médicos e procedimentos de cada uma. Para caber no limite por
            hora, cada rodada começa pelas unidades mais desatualizadas: em algumas rodadas, a rede inteira fica
            coberta. Durante a rodada aparecem o progresso e quantas requisições ainda cabem na hora.
          </P>
          <Lista>
            <Item>
              <strong>Carga inicial</strong>: só aparece enquanto falta unidade sem mapeamento. Ligada, ela dispara
              uma rodada atrás da outra quando o limite da hora reabre, mesmo com a aba fechada, e se desliga quando
              termina.
            </Item>
            <Item>
              <strong>Programar o sincronismo diário</strong>: liga a importação diária de cada unidade, uma a cada
              tantos minutos a partir do horário escolhido, sempre fora da faixa de 8h às 15h em que o SISREG
              bloqueia a exportação. A prévia mostra onde a última unidade termina antes de você confirmar em{' '}
              <BotaoRef variante="outline">Programar todas as unidades</BotaoRef>. O aviso por WhatsApp ao paciente
              nasce desligado em todas.
            </Item>
            <Item>
              <BotaoRef variante="outline">Habilitar todas</BotaoRef> e <BotaoRef variante="ghost">Desabilitar todas</BotaoRef>{' '}
              reescrevem a agenda de cada unidade. Para pausar, use a chave mestra.
            </Item>
            <Item>
              Em <strong>Sincronizações recentes</strong>, clique numa linha para ver o detalhe por unidade.
              “Já atualizadas” é economia, não falha: a unidade estava em dia e não custou acesso.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'importacao-diaria',
      titulo: 'O que a importação diária de cada unidade faz',
      busca: 'importação diária varredura agenda da unidade hoje última escala parcial dia não lido cancelamento suspenso nova tentativa 15 minutos 07:30 chegadas 31 dias comparecimento',
      conteudo: (
        <>
          <P>Toda noite, no horário programado, cada unidade tem a agenda lida:</P>
          <Lista>
            <Item>
              Lê <strong>de hoje até a última escala</strong> da unidade, de uma vez, e importa ou atualiza cada
              agendamento pelo número do SISREG. Ler de novo não duplica nada.
            </Item>
            <Item>
              Se o SISREG não entrega o arquivo de algum dia (às vezes ele responde com página de erro), esse dia
              fica <strong>fora</strong> da corrida e ela termina como <strong>Parcial</strong>, dizendo quais
              dias. Nesses dias, a checagem de “sumiu do SISREG” fica suspensa: sem o arquivo, não dá para saber se
              o agendamento foi cancelado ou se a leitura falhou.
            </Item>
            <Item>
              Se a corrida inteira falha (o SISREG fora do ar, conexão cortada), ela é tentada de novo em 15
              minutos, depois em 30, 60…, sem passar do horário do dia seguinte. Nenhuma corrida começa entre 07:30
              e 15:00.
            </Item>
            <Item>
              Por último, relê os <strong>últimos 31 dias</strong> só para gravar a chegada do paciente: se a
              unidade confirmou que ele veio. Na noite em que a agenda já teve dia não lido, essa releitura fica
              para a próxima.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'escalas',
      titulo: 'Sincronismo de escalas',
      busca: 'escalas grade de vagas oferta profissional procedimento dia da semana primeira vez retorno reserva uma requisição horários completar executante',
      conteudo: (
        <>
          <P>
            Traz a <strong>grade de vagas</strong>: quem atende, onde, qual procedimento, em que dia e horário, e
            quantas vagas de primeira vez, retorno e reserva. É a oferta; os agendamentos são quem ocupa. Custa uma
            requisição para a rede inteira e não sofre o bloqueio do expediente, então pode ter vários horários por
            dia (<strong>+ horário</strong>). Escalas vencidas vêm junto de propósito: são o denominador da análise
            do passado.
          </P>
          <P>
            <BotaoRef variante="outline">Completar executante</BotaoRef> preenche o profissional executante das
            solicitações já importadas, relendo o que já está guardado aqui. Não fala com o SISREG e pode ser rodado
            de novo, porque só mexe no que está vazio.
          </P>
        </>
      ),
    },
    {
      id: 'fila-espera',
      titulo: 'Fila de espera',
      busca: 'fila de espera quem espera ofertas reler a fila inteira últimos 31 dias 88 requisições 20 minutos horário 03:00 cancelados devolvidos',
      conteudo: (
        <>
          <P>
            É quem pediu no SISREG e ainda não foi agendado, a lista que a tela de Ofertas mostra em “Quem espera”.
            É relida <strong>inteira</strong> todo dia, no horário escolhido: só assim aparecem os cancelados e
            devolvidos, os pedidos antigos reenviados e as trocas de risco ou de procedimento.
          </P>
          <Lista>
            <Item>
              <BotaoRef>Reler a fila inteira agora</BotaoRef>: cerca de 88 requisições e uns 20 minutos.
            </Item>
            <Item>
              <BotaoRef variante="outline">Só os últimos 31 dias</BotaoRef>: 2 requisições.
            </Item>
            <Item>
              Se outro motor estiver usando a sessão no horário, a leitura espera e começa em até 2 horas. Quando a
              sessão cai no meio, o período é relido, e a seção diz quantos precisaram disso.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'coletor',
      titulo: 'Coleta dos indicadores',
      busca: 'coletor indicadores faltas cotas ppi canceladas desfechos lidas na fila com falha tentar todas de novo de volta na fila 01:20 18:00 login recusado aguardando interrompida',
      conteudo: (
        <>
          <P>
            O último bloco lê as telas do SISREG que alimentam os <strong>Indicadores de Regulação</strong>:
            faltas, cotas PPI, canceladas e devolvidas ou negadas. Ele lê devagar, uma requisição a cada 30
            segundos, só entre 01:20 e 18:00 e sempre cedendo a vez aos outros motores.
          </P>
          <P>
            A tabela mostra, por tipo de leitura, quantas estão Lidas, Na fila e Com falha. Leitura que não fecha
            aparece na lista vermelha. <BotaoRef variante="outline">Tentar todas de novo</BotaoRef> devolve todas para
            a fila: não lê na hora e não apaga nada, e a seção passa a dizer quantas voltaram e quando serão lidas.
            O detalhe, e o que cada falha deixa de fora, está no artigo <strong>Indicadores de Regulação</strong>,
            seção “Quando uma leitura do coletor não fecha”.
          </P>
          <P>
            Quando o SISREG recusa o login, a seção mostra “<strong>Aguardando: o SISREG recusou o login</strong>”.
            Nenhuma leitura é gasta com isso: o coletor espera alguns minutos e tenta de novo sozinho. Se a
            recusa continuar, olhe o Endereço do SISREG em Integrações antes de mexer na senha.
          </P>
        </>
      ),
    },
    {
      id: 'endereco',
      titulo: 'Endereço do SISREG (IP e túnel)',
      busca: 'endereço ip do sisreg trocou de ip túnel saída brasil bloqueio bloqueado histórico integrações não foi possível autenticar',
      conteudo: (
        <>
          <P>
            O SISREG só responde a acessos vindos do Brasil. Quando o servidor da plataforma fica fora do país, ele
            fala com o SISREG por um <strong>túnel</strong> que sai pelo Brasil. O túnel só leva o endereço (IP)
            exato do SISREG, e o SISREG <strong>troca de IP sem avisar</strong>. Se o túnel continuasse com o IP
            antigo, o acesso sairia pelo exterior e o SISREG pararia de responder. Para quem olha de fora, isso
            parece “nosso acesso foi bloqueado”, mas não é.
          </P>
          <P>
            Por isso o servidor confere o IP do SISREG <strong>sozinho, a cada poucos minutos</strong>. Quando o IP
            muda, ele mesmo põe o novo no túnel, em até um minuto. O resultado aparece em{' '}
            <strong>Integrações</strong>, no cartão do SISREG, no bloco <strong>Endereço do SISREG</strong>:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'IP e “desde”', descricao: 'O endereço em que o SISREG responde agora e desde quando.' },
              {
                termo: 'Saindo pelo túnel',
                descricao:
                  'Está tudo certo. Num servidor que já fica no Brasil, aparece “Saída direta”, que também está certo.',
              },
              {
                termo: 'Fora do túnel',
                descricao:
                  'O acesso está saindo pelo exterior e o SISREG não responde. Costuma se corrigir sozinho em até um minuto. Se persistir, chame o suporte técnico.',
              },
              { termo: 'Histórico', descricao: 'Cada IP em que o SISREG já respondeu, de quando a quando.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'avisos',
      titulo: 'Avisos de falha',
      busca: 'avisos celular whatsapp captcha credencial derrubada unidade com erro trocou de ip fora do túnel coletor login recusado leitura parada',
      conteudo: (
        <>
          <P>
            CAPTCHA, credencial derrubada e unidade com erro chegam no WhatsApp de quem está em Sistema → Avisos no
            celular. Não há lista própria por integração: cadastre ou tire telefones lá.
          </P>
          <P>
            Do coletor dos indicadores vêm dois avisos. “<strong>O SISREG recusou o login</strong>” chega uma vez
            só, no começo da recusa; o coletor segue tentando sozinho. “<strong>Leitura parada</strong>” chega
            quando uma leitura falhou em todas as tentativas: ela só volta pelo botão Tentar todas de novo.
          </P>
          <P>
            Do endereço do SISREG vêm três avisos. “<strong>SISREG trocou de IP</strong>” é só informativo, porque o
            servidor já cuida do túnel. “<strong>SISREG fora do túnel</strong>” só chega se o problema durar duas
            conferências seguidas, e aí o login no SISREG está falhando. “<strong>SISREG de volta ao túnel</strong>”
            avisa quando o problema se resolve.
          </P>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão módulo configuração sisreg consulta edição',
      conteudo: (
        <P>
          Ver a tela exige o módulo <strong>Configuração SISREG</strong> com consulta. Salvar, testar a conexão,
          ligar ou desligar o sincronismo e qualquer botão que dispare leitura exigem a mesma permissão com{' '}
          <strong>edição</strong>, porque gastam o limite de acessos do operador da integração.
        </P>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'dúvidas parou de importar desliguei unidade parcial cliquei tentar de novo e agora',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Parou de importar. O que olho primeiro?',
              descricao:
                'A chave mestra no topo: se estiver amarela, está desligada. Depois, se houve aviso de CAPTCHA no celular.',
            },
            {
              termo: 'De repente tudo falha com “Não foi possível autenticar no SISREG”. É a senha?',
              descricao:
                'Antes de trocar a senha, abra Integrações e olhe o bloco Endereço do SISREG no cartão do SISREG. Se estiver “Fora do túnel”, o problema é o caminho até o SISREG, não a senha. Foi o que aconteceu quando o SISREG trocou de IP.',
            },
            {
              termo: 'Uma unidade terminou Parcial. Perdi dados?',
              descricao:
                'Não do que veio: tudo o que o SISREG entregou foi importado. Os dias que ele não entregou ficaram fora da checagem de cancelamento. Os que ainda não passaram são lidos de novo na próxima corrida, que relê de hoje em diante. Um dia que já passou não volta a ser lido pela importação: se for o caso, rode a unidade de novo no mesmo dia.',
            },
            {
              termo: 'Cliquei em “Tentar todas de novo”. E agora?',
              descricao:
                'As leituras voltaram para a fila. O aviso verde diz quantas e quando serão lidas, e a lista amarela “De volta na fila” mostra cada uma até ser tentada. Se falharem de novo, voltam para a lista vermelha.',
            },
          ]}
        />
      ),
    },
  ],
};
