import { DatabaseZap } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Sistema → Fontes de dados (`/app/fontes-dados`, permissão Fontes de dados — módulo 82, ADR-0077).
 *
 * Conferido no código em 21/10/2026: `features/fontes-dados/pages/FontesDadosPage`,
 * `components/FonteDadosModal`, `NovoAgenteModal`, `FonteDoRecurso`; no servidor,
 * `Core/Fontes/FontesDadosService` (só chaves de conexão; login do SER/SERNIT/ESUS SG passa pelo
 * motor, que testa antes de gravar) e `SistemasFonte` (os seis sistemas fixos, quem tem teste e por
 * que não). A cadência do Klinikos Web está em `pep-sincronizacao/components/SecaoKlinikosWeb`.
 */
export const artigoFontesDados: Artigo = {
  slug: 'fontes-de-dados',
  titulo: 'Fontes de dados',
  resumo:
    'O cadastro único de onde a plataforma lê dado: bancos, agentes, Klinikos pela web e os logins do SISREG, SER, SERNIT, ESUS São Gonçalo e e-SUS PEC.',
  grupo: 'sistema',
  icone: DatabaseZap,
  rota: '/app/fontes-dados',
  publico: 'Quem administra as conexões da plataforma (permissão Fontes de dados)',
  atualizadoEm: '2026-10-21',
  palavrasChave: [
    'fontes de dados',
    'fonte de dado',
    'base de dados',
    'banco de dados',
    'conexão',
    'connection string',
    'host',
    'porta',
    'agente',
    'token do agente',
    'novo agente',
    'sql server',
    'oracle',
    'postgres',
    'salux',
    'klinikos',
    'klinikos web',
    'credencial',
    'login',
    'senha',
    'trocar senha',
    'sisreg',
    'api do sisreg',
    'ser',
    'sernit',
    'esus são gonçalo',
    'e-sus pec',
    'endereço do sisreg',
    'testar conexão',
    'usada por',
    'limpar login',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'fonte de dado onde a plataforma lê conexão uso recurso escolhe um lugar só',
      conteudo: (
        <>
          <P>
            Toda vez que a plataforma lê dado de outro sistema — o prontuário de um hospital, o SISREG, o SER —
            ela usa uma <strong>fonte de dados</strong>. Esta tela é o único lugar onde se cadastra e se mexe na
            <strong> conexão</strong> de cada fonte: endereço, login, senha, agente.
          </P>
          <P>
            O que cada parte do sistema <strong>faz</strong> com a fonte fica na tela dela: a Consulta Inteligente
            escolhe a base e guarda o conhecimento; Importar Prontuários escolhe a base e define a agenda; o SER
            tem a varredura na própria configuração. Assim, trocar uma senha nunca mexe na agenda de ninguém, e
            mexer numa agenda nunca derruba um login.
          </P>
          <Callout tipo="regra" titulo="A senha nunca aparece">
            Senha, token e o login dos operadores dos sistemas externos são gravados cifrados e não voltam para a
            tela. Campo vazio ao salvar quer dizer “mantém o que está gravado”.
          </Callout>
        </>
      ),
    },
    {
      id: 'o-que-aparece',
      titulo: 'O que a tela mostra',
      busca: 'bancos e agentes prontuários pela web regulação e cadastro banco do próprio sistema ativa inativa sem login agente conectado desconectado treinamento usada por',
      conteudo: (
        <>
          <P>As fontes vêm separadas em quatro grupos:</P>
          <ListaDefinicoes
            itens={[
              { termo: 'Bancos e agentes', descricao: 'Bancos de prontuário lidos direto (Oracle, PostgreSQL) ou por um agente instalado no servidor da unidade (SQL Server).' },
              { termo: 'Prontuários pela web', descricao: 'Sistemas lidos “como usuário”, pelo login e pelos relatórios da tela — hoje, o Klinikos.' },
              { termo: 'Regulação e cadastro', descricao: 'SISREG (login do robô e API), SER, SERNIT, ESUS São Gonçalo e e-SUS PEC. Já vêm na lista mesmo antes de configurados.' },
              { termo: 'Banco do próprio sistema', descricao: 'Recortes só de leitura do banco da plataforma, para a Consulta Inteligente.' },
            ]}
          />
          <P>Em cada fonte:</P>
          <ListaDefinicoes
            itens={[
              { termo: 'Ativa / Inativa', descricao: 'Inativa, quem usa a fonte para de ler dela.' },
              { termo: 'Sem login', descricao: 'Sistema externo sem usuário e senha gravados — os motores que dependem dele não rodam.' },
              { termo: 'Agente conectado / desconectado', descricao: 'Se o agente do servidor da unidade está falando com a plataforma agora.' },
              { termo: 'Usada por', descricao: 'Quem lê desta fonte. Olhe antes de desativar ou trocar uma senha.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'editar',
      titulo: 'Editar uma fonte',
      busca: 'editar trocar senha login usuário testar e salvar testar o login digitado testar conexão sem teste sessão única tipo dialeto fixo url padrão',
      conteudo: (
        <>
          <P>
            <BotaoRef>Editar</BotaoRef> abre só os campos de conexão daquele tipo. Tipo e dialeto ficam fixos
            depois que a base é criada.
          </P>
          <Lista>
            <Item>
              <strong>SER, SERNIT e ESUS São Gonçalo:</strong> para trocar o login, informe usuário <em>e</em> senha.
              O botão vira <BotaoRef>Testar e salvar</BotaoRef>: o sistema entra com o login novo e só grava se
              entrar — um login que não funciona faria o motor falhar de madrugada, sem ninguém por perto.
              <BotaoRef variante="outline">Testar o login digitado</BotaoRef> entra sem gravar.
            </Item>
            <Item>
              <strong>SISREG III e e-SUS PEC não têm botão de teste.</strong> Os dois aceitam uma sessão por login:
              testar entraria com o mesmo usuário do robô (ou da servidora que cedeu o login do e-SUS) e derrubaria
              a sessão dela.
            </Item>
            <Item>
              <strong>URL vazia</strong> nos sistemas externos quer dizer “use o endereço padrão”, que aparece
              escrito embaixo do campo.
            </Item>
            <Item>
              <strong>Bancos e API do SISREG</strong> têm <BotaoRef variante="outline">Testar conexão</BotaoRef>, que
              usa o que está gravado.
            </Item>
          </Lista>
          <P>
            Na fonte <strong>SISREG III</strong> fica também o bloco <strong>Endereço do SISREG</strong>: o IP em que
            o SISREG responde, se o acesso está saindo pelo túnel e o histórico das trocas. Se o SISREG passar a
            recusar o login de repente, olhe esse bloco antes de trocar a senha — o detalhe está no artigo
            <strong> Configuração do SISREG</strong>.
          </P>
        </>
      ),
    },
    {
      id: 'bases-e-agentes',
      titulo: 'Nova base e novo agente',
      busca: 'nova base cadastrar banco oracle postgres fhir klinikos web slug família novo agente token copiar env gerar novo token desconecta',
      conteudo: (
        <>
          <P>
            <BotaoRef>Nova base</BotaoRef> cadastra um banco (Oracle, PostgreSQL), uma API FHIR, um Klinikos pela web
            ou um recorte do próprio banco. O <strong>slug</strong> marca a origem de tudo o que for importado dela:
            escolha um curto e não mude depois. A <strong>família</strong> diz que bases têm a mesma estrutura — a
            importação reconhece a base pelo tipo junto com a família.
          </P>
          <P>
            Banco SQL Server de uma unidade é lido por um <strong>agente</strong> instalado no servidor dela, e as
            credenciais do banco ficam lá, não aqui. <BotaoRef variante="outline">Novo agente</BotaoRef> reserva o slug
            e gera o token que o agente usa para se conectar.
          </P>
          <Callout tipo="atencao" titulo="O token aparece uma vez só">
            Copie o token e o endereço e cole no arquivo de configuração do agente, no servidor da unidade.
            <BotaoRef variante="outline">Gerar novo token</BotaoRef>, na edição do agente, desconecta o agente atual até o
            token novo ser colado lá.
          </Callout>
        </>
      ),
    },
    {
      id: 'remover',
      titulo: 'Remover uma base ou limpar um login',
      busca: 'remover base limpar login desativar quem usa para agenda mantida',
      conteudo: (
        <P>
          <BotaoRef variante="outline">Remover</BotaoRef> tira uma base da lista, e quem a usava para de ler dela.
          Nos sistemas externos o botão é <BotaoRef variante="outline">Limpar login</BotaoRef>: apaga usuário e senha
          e desativa a fonte, mas a agenda dos motores fica guardada — gravando um login novo, tudo volta como
          estava. A pergunta de confirmação lista quem usa a fonte. A API do SISREG não se remove: desmarque
          “Ativa”.
        </P>
      ),
    },
    {
      id: 'onde-fica-o-uso',
      titulo: 'Onde fica o uso de cada fonte',
      busca: 'agenda cadência varredura conhecimento onde configurar consulta inteligente importar prontuários regulação configuração sisreg janela madrugada',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta Inteligente', descricao: 'Inteligência → Configuração IA: o conhecimento de cada base. A base é escolhida ao abrir a conversa.' },
              { termo: 'Importar Prontuários', descricao: 'A base de origem, a agenda do sincronismo e a cadência do Klinikos pela web.' },
              { termo: 'SER, SERNIT e ESUS São Gonçalo', descricao: 'Regulação → Configuração de cada um: a varredura e o horário.' },
              { termo: 'SISREG', descricao: 'Configuração do SISREG: escopo, sincronismo, escalas, fila e indicadores.' },
              { termo: 'e-SUS PEC', descricao: 'A janela da madrugada fica aqui mesmo, na fonte, porque a rotina de correção de telefone não tem tela própria.' },
            ]}
          />
          <P>Cada uma dessas telas mostra no topo a linha “Fonte”, que diz se o login está configurado e leva até aqui.</P>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão módulo fontes de dados consulta inclusão edição exclusão administrador',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Ver as fontes, a situação e quem usa cada uma.' },
              { termo: 'Inclusão', descricao: 'Cadastrar base.' },
              { termo: 'Edição', descricao: 'Editar conexão, trocar login e senha, testar e gerar token de agente.' },
              { termo: 'Exclusão', descricao: 'Remover base ou limpar o login de um sistema.' },
            ]}
          />
          <P>
            O módulo nasce só no perfil de administrador. Quem trocava a senha do SER, SERNIT ou ESUS pela
            configuração da Regulação precisa receber o módulo <strong>Fontes de dados</strong> em Perfis.
          </P>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'dúvidas trocar senha do ser motor parou sem login onde está credencial integrações aba configuração',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Onde foi parar o login do SER (ou do SISREG) que ficava na configuração / em Integrações?',
              descricao: 'Aqui. A configuração de cada sistema mostra a linha “Fonte” com o atalho; Integrações ficou só com os serviços que a plataforma usa (armazenamento, mapas, WhatsApp, voz).',
            },
            {
              termo: 'Troquei a senha do SER e a varredura continua ligada?',
              descricao: 'Sim. Trocar o login não mexe na agenda nem no horário da varredura.',
            },
            {
              termo: 'A fonte mostra “Sem login”, mas eu cadastrei.',
              descricao: 'Usuário e senha precisam estar os dois gravados. Edite a fonte e informe os dois.',
            },
          ]}
        />
      ),
    },
  ],
};
